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
        await using var ctx = CreateContext();
        return await ctx.UploadRecords
            .Where(r => r.Valid && (r.Sent == null || r.Sent == false))
            .OrderBy(r => r.ItemDateTime)
            .ToListAsync(ct);
    }

    public async Task MarkCsvSentAsync(int recordId, CancellationToken ct)
    {
        await using var ctx = CreateContext();
        var record = await ctx.UploadRecords.FindAsync(new object[] { recordId }, ct);
        if (record is null) return;
        record.Sent = true;
        await ctx.SaveChangesAsync(ct);
        LoggingService.Upload.Debug("Record {Id} marked as CSV sent.", recordId);
    }

    public async Task MarkCompleteAsync(int recordId, CancellationToken ct)
    {
        await using var ctx = CreateContext();
        var record = await ctx.UploadRecords.FindAsync(new object[] { recordId }, ct);
        if (record is null) return;
        record.Complete = true;
        await ctx.SaveChangesAsync(ct);
        LoggingService.Upload.Debug("Record {Id} marked as complete.", recordId);
    }

    private ApplicationDbContext CreateContext() =>
        new(_settings.Database.BuildConnectionString(), _settings.Database.TableName);
}
