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

        LoggingService.Upload.Debug("Processing {Count} unsent record(s).", records.Count);

        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessOneAsync(record, ct);
        }
    }

    private async Task ProcessOneAsync(UploadRecord record, CancellationToken ct)
    {
        try
        {
            // ── CSV pipeline ──────────────────────────────────────────────────
            var csvPath = await _fileBuilder.BuildAsync(record, ct);
            await _pipeline.ExecuteAsync(async token =>
                await _transfer.CopyFileAsync(csvPath, _settings.WindowsShare.DataRemoteDirectory, token), ct);
            await _db.MarkCsvSentAsync(record.Id, ct);
            LoggingService.Upload.Information("Record {Id} ({Barcode}): CSV sent.", record.Id, record.Barcode);

            // ── Image pipeline (errors are logged, never fail the record) ─────
            if (_settings.FileSettings.Image.EnableUpload)
                await ProcessImageAsync(record, ct);

            await _db.MarkCompleteAsync(record.Id, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "Record {Id}: processing failed.", record.Id);
        }
    }

    private async Task ProcessImageAsync(UploadRecord record, CancellationToken ct)
    {
        try
        {
            var source = await _fileService.FindImageAsync(record, ct);
            if (source is null)
            {
                LoggingService.Upload.Debug("Record {Id}: no image found, skipping.", record.Id);
                return;
            }

            var archived = await _fileService.CopyImageToArchiveAsync(source, Path.GetFileName(source), ct);
            await _pipeline.ExecuteAsync(async token =>
                await _transfer.CopyFileAsync(archived, _settings.WindowsShare.ImageRemoteDirectory, token), ct);
            _fileService.DeleteSourceFile(source);
            LoggingService.Upload.Information("Record {Id}: image sent.", record.Id);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LoggingService.Upload.Warning(ex, "Record {Id}: image processing failed — skipping.", record.Id);
        }
    }

    private static ResiliencePipeline BuildPipeline(int maxRetries) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                // Retry transient IO failures; do not retry auth or missing-path errors
                ShouldHandle = new PredicateBuilder()
                    .Handle<IOException>(ex => ex is not FileNotFoundException and not DirectoryNotFoundException)
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    LoggingService.Upload.Warning(
                        "Retry {Attempt} after: {Error}",
                        args.AttemptNumber + 1,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
}
