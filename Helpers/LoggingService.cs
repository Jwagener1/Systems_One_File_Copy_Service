using Serilog;
using Serilog.Core;

// Disambiguate from Microsoft.Extensions.Logging.ILogger
using ILogger = Serilog.ILogger;

namespace SystemsOne.FileCopyService.Helpers;

public static class LoggingService
{
    private static ILogger _upload = Logger.None;
    private static ILogger _files = Logger.None;
    private static System.Threading.Timer? _rolloverTimer;
    private static readonly object _lock = new();

    /// <summary>All operational events: connections, copies, retries, errors, DB ops.</summary>
    public static ILogger Upload => _upload;

    /// <summary>CSV filename + full file contents, one entry per file sent.</summary>
    public static ILogger Files => _files;

    public static void Initialize()
    {
        CreateLoggers();
        ScheduleMidnightRollover();
    }

    private static void CreateLoggers()
    {
        lock (_lock)
        {
            var logBase = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                "Systems_One_Logs",
                DateTime.Now.ToString("yyyy-MM-dd"));

            Directory.CreateDirectory(logBase);

            const string template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

            _upload = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logBase, "upload.log"), outputTemplate: template, shared: true)
                .CreateLogger();

            _files = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(logBase, "files.log"), outputTemplate: template, shared: true)
                .CreateLogger();
        }
    }

    private static void ScheduleMidnightRollover()
    {
        var delay = DateTime.Now.Date.AddDays(1) - DateTime.Now;

        _rolloverTimer?.Dispose();
        _rolloverTimer = new System.Threading.Timer(_ =>
        {
            CreateLoggers();
            DeleteOldLogs();
            ScheduleMidnightRollover();
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private static void DeleteOldLogs()
    {
        try
        {
            var logsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                "Systems_One_Logs");

            if (!Directory.Exists(logsRoot)) return;

            var cutoff = DateTime.Now.AddDays(-30);
            foreach (var dir in Directory.GetDirectories(logsRoot))
            {
                if (DateTime.TryParse(Path.GetFileName(dir), out var dirDate) && dirDate < cutoff)
                    Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _upload.Warning(ex, "Failed to clean up old log directories.");
        }
    }
}
