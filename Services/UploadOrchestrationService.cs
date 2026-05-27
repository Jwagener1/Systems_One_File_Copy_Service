using Polly;
using Polly.Retry;
using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;

namespace SystemsOne.FileCopyService.Services;

public interface IUploadOrchestrationService
{
    Task ProcessRecordsAsync(CancellationToken ct);
}

public class UploadOrchestrationService : IUploadOrchestrationService
{
    private readonly IDatabaseService _db;
    private readonly IFileBuilder _fileBuilder;
    private readonly IFileService _fileService;
    private readonly IFileTransferService _transfer;
    private readonly AppSettings _settings;
    private readonly ResiliencePipeline _pipeline;

    public UploadOrchestrationService(
        IDatabaseService db,
        IFileBuilder fileBuilder,
        IFileService fileService,
        IFileTransferService transfer,
        AppSettings settings)
    {
        _db = db;
        _fileBuilder = fileBuilder;
        _fileService = fileService;
        _transfer = transfer;
        _settings = settings;
        _pipeline = BuildPipeline(settings.WindowsShare.MaxRetries);
    }

    public async Task ProcessRecordsAsync(CancellationToken ct)
    {
        var records = await _db.GetUnsentRecordsAsync(ct);
        if (records.Count == 0) return;

        LoggingService.Upload.Information("Processing {Count} unsent record(s).", records.Count);

        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessOneAsync(record, ct);
        }
    }

    private async Task ProcessOneAsync(UploadRecord record, CancellationToken ct)
    {
        LoggingService.Upload.Debug(
            "Record {Id} | Barcode: {Barcode} | DateTime: {DT} — starting.",
            record.Id, record.Barcode, record.ItemDateTime);
        try
        {
            // ── Step 1: build the CSV file ────────────────────────────────────
            LoggingService.Upload.Debug("Record {Id} — step 1/5: building CSV.", record.Id);
            var csvPath = await _fileBuilder.BuildAsync(record, ct);

            // ── Step 2: copy CSV to share ─────────────────────────────────────
            LoggingService.Upload.Debug(
                "Record {Id} — step 2/5: copying CSV to share directory '{Dir}'.",
                record.Id, _settings.WindowsShare.DataRemoteDirectory);
            await _pipeline.ExecuteAsync(async token =>
                await _transfer.CopyFileAsync(csvPath, _settings.WindowsShare.DataRemoteDirectory, token), ct);

            // ── Step 3: mark CSV sent in database ─────────────────────────────
            LoggingService.Upload.Debug("Record {Id} — step 3/5: marking CSV sent.", record.Id);
            await _db.MarkCsvSentAsync(record.Id, ct);
            LoggingService.Upload.Information("Record {Id} ({Barcode}): CSV sent.", record.Id, record.Barcode);

            // ── Step 4: process image (optional, never fails the record) ──────
            if (_settings.FileSettings.Image.EnableUpload)
            {
                LoggingService.Upload.Debug("Record {Id} — step 4/5: processing image.", record.Id);
                await ProcessImageAsync(record, ct);
            }
            else
            {
                LoggingService.Upload.Debug("Record {Id} — step 4/5: image upload disabled, skipping.", record.Id);
            }

            // ── Step 5: mark record complete ──────────────────────────────────
            LoggingService.Upload.Debug("Record {Id} — step 5/5: marking complete.", record.Id);
            await _db.MarkCompleteAsync(record.Id, ct);

            LoggingService.Upload.Information("Record {Id} ({Barcode}): done.", record.Id, record.Barcode);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex,
                "Record {Id} ({Barcode}): processing failed — {Error}",
                record.Id, record.Barcode, ex.Message);
        }
    }

    private async Task ProcessImageAsync(UploadRecord record, CancellationToken ct)
    {
        try
        {
            var source = await _fileService.FindImageAsync(record, ct);
            if (source is null)
            {
                LoggingService.Upload.Debug("Record {Id}: no image found — skipping.", record.Id);
                return;
            }

            LoggingService.Upload.Debug("Record {Id}: archiving image from: {Src}", record.Id, source);
            var archived = await _fileService.CopyImageToArchiveAsync(source, Path.GetFileName(source), ct);

            LoggingService.Upload.Debug(
                "Record {Id}: copying image to share directory '{Dir}'.",
                record.Id, _settings.WindowsShare.ImageRemoteDirectory);
            await _pipeline.ExecuteAsync(async token =>
                await _transfer.CopyFileAsync(archived, _settings.WindowsShare.ImageRemoteDirectory, token), ct);

            await _db.MarkImageSentAsync(record.Id, ct);
            _fileService.DeleteSourceFile(source);
            LoggingService.Upload.Information("Record {Id} ({Barcode}): image sent.", record.Id, record.Barcode);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LoggingService.Upload.Warning(ex,
                "Record {Id} ({Barcode}): image processing failed ({Error}) — record will still be marked complete.",
                record.Id, record.Barcode, ex.Message);
        }
    }

    private static ResiliencePipeline BuildPipeline(int maxRetries) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<IOException>(ex => ex is not FileNotFoundException and not DirectoryNotFoundException)
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    LoggingService.Upload.Warning(
                        "Retry {Attempt}/{Max} after error: {Error}",
                        args.AttemptNumber + 1,
                        args.AttemptNumber + 1,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
}
