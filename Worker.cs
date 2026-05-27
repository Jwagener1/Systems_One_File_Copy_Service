using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;
using SystemsOne.FileCopyService.Services;

namespace SystemsOne.FileCopyService;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppSettings _settings;

    public Worker(IServiceScopeFactory scopeFactory, AppSettings settings)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LoggingService.Upload.Information("Systems One File Copy Service starting.");

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // One-time startup connection test (non-fatal if it fails)
        using (var scope = _scopeFactory.CreateScope())
        {
            var transfer = scope.ServiceProvider.GetRequiredService<IFileTransferService>();
            await transfer.TestConnectionAsync();
        }

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.General.UploadInterval_ms));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestration = scope.ServiceProvider.GetRequiredService<IUploadOrchestrationService>();
                await orchestration.ProcessRecordsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LoggingService.Upload.Error(ex, "Unhandled error in processing tick.");
            }
        }

        LoggingService.Upload.Information("Systems One File Copy Service stopped.");
    }
}
