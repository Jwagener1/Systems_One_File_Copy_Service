using Microsoft.EntityFrameworkCore;
using SystemsOne.FileCopyService.Data;
using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;

namespace SystemsOne.FileCopyService.Services;

public interface IDatabaseService
{
    Task<List<UploadRecord>> GetUnsentRecordsAsync(CancellationToken ct);
    Task MarkCsvSentAsync(int recordId, CancellationToken ct);
    Task MarkCompleteAsync(int recordId, CancellationToken ct);
}

public class DatabaseService : IDatabaseService
{
    private readonly AppSettings _settings;

    public DatabaseService(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<List<UploadRecord>> GetUnsentRecordsAsync(CancellationToken ct)
    {
        LoggingService.Upload.Debug(
            "Querying {Server}/{Database} table '{Table}' for unsent records (Valid=1, Sent=NULL/0)...",
            _settings.Database.Server, _settings.Database.DatabaseName, _settings.Database.TableName);

        try
        {
            await using var ctx = CreateContext();
            var records = await ctx.UploadRecords
                .Where(r => r.Valid && (r.Sent == null || r.Sent == false))
                .OrderBy(r => r.ItemDateTime)
                .ToListAsync(ct);

            if (records.Count > 0)
                LoggingService.Upload.Information(
                    "Found {Count} unsent record(s). First: Id={FirstId} at {FirstTime}.",
                    records.Count, records[0].Id, records[0].ItemDateTime);
            else
                LoggingService.Upload.Debug("No unsent records found.");

            return records;
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex,
                "Database query failed — Server: {Server} | Database: {Database} | Table: {Table}",
                _settings.Database.Server, _settings.Database.DatabaseName, _settings.Database.TableName);
            throw;
        }
    }

    public async Task MarkCsvSentAsync(int recordId, CancellationToken ct)
    {
        LoggingService.Upload.Debug("Marking record {Id} as CSV sent...", recordId);
        try
        {
            await using var ctx = CreateContext();
            var record = await ctx.UploadRecords.FindAsync(new object[] { recordId }, ct);
            if (record is null)
            {
                LoggingService.Upload.Warning("MarkCsvSentAsync: record {Id} not found in database.", recordId);
                return;
            }
            record.Sent = true;
            await ctx.SaveChangesAsync(ct);
            LoggingService.Upload.Debug("Record {Id} marked as CSV sent.", recordId);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "Failed to mark record {Id} as CSV sent.", recordId);
            throw;
        }
    }

    public async Task MarkCompleteAsync(int recordId, CancellationToken ct)
    {
        LoggingService.Upload.Debug("Marking record {Id} as complete...", recordId);
        try
        {
            await using var ctx = CreateContext();
            var record = await ctx.UploadRecords.FindAsync(new object[] { recordId }, ct);
            if (record is null)
            {
                LoggingService.Upload.Warning("MarkCompleteAsync: record {Id} not found in database.", recordId);
                return;
            }
            record.Complete = true;
            await ctx.SaveChangesAsync(ct);
            LoggingService.Upload.Debug("Record {Id} marked as complete.", recordId);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "Failed to mark record {Id} as complete.", recordId);
            throw;
        }
    }

    private ApplicationDbContext CreateContext() =>
        new(_settings.Database.BuildConnectionString(), _settings.Database.TableName);
}
