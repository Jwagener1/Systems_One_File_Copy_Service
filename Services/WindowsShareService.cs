using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;

namespace SystemsOne.FileCopyService.Services;

public interface IFileTransferService
{
    Task CopyFileAsync(string localPath, string remoteDirectory, CancellationToken ct);
    Task<bool> TestConnectionAsync();
}

[SupportedOSPlatform("windows")]
public sealed class WindowsShareService : IFileTransferService, IDisposable
{
    private readonly WindowsShareSettings _cfg;
    private bool _connected;
    private bool _disposed;
    private readonly object _connectLock = new();

    public WindowsShareService(AppSettings settings)
    {
        _cfg = settings.WindowsShare;
        LoggingService.Upload.Information(
            "WindowsShareService initialised — Base: {Base} | Credentials: {Creds}",
            _cfg.BaseSharePath,
            string.IsNullOrWhiteSpace(_cfg.ShareUsername) ? "none (service account)" : $"user '{_cfg.ShareUsername}'");
    }

    public async Task CopyFileAsync(string localPath, string remoteDirectory, CancellationToken ct)
    {
        EnsureConnected();

        var destDir = BuildRemotePath(remoteDirectory);
        LoggingService.Upload.Debug("CopyFile — source: {Src} | destination directory: {Dir}", localPath, destDir);

        try
        {
            Directory.CreateDirectory(destDir);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "Failed to create destination directory on share: {Dir}", destDir);
            throw;
        }

        var fileName = Path.GetFileName(localPath);
        var finalPath = ResolveUniqueDestPath(destDir, fileName);
        var tempPath = finalPath + ".uploading";

        LoggingService.Upload.Debug("Copying to temp: {Temp}", tempPath);
        try
        {
            await Task.Run(() => File.Copy(localPath, tempPath, overwrite: true), ct);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "File.Copy failed: {Src} → {Temp}", localPath, tempPath);
            throw;
        }

        LoggingService.Upload.Debug("Renaming temp to final: {Temp} → {Final}", tempPath, finalPath);
        try
        {
            File.Move(tempPath, finalPath, overwrite: false);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "File.Move failed: {Temp} → {Final}", tempPath, finalPath);
            throw;
        }

        LoggingService.Upload.Information("File copied to share: {Final}", finalPath);
    }

    public Task<bool> TestConnectionAsync()
    {
        LoggingService.Upload.Debug("Testing share connection: {Base}", _cfg.BaseSharePath);
        try
        {
            EnsureConnected();
            var dataDir = BuildRemotePath(_cfg.DataRemoteDirectory);
            LoggingService.Upload.Debug("Checking data directory exists or can be created: {Dir}", dataDir);
            var ok = Directory.Exists(dataDir) || TryCreateDirectory(dataDir);
            if (ok)
                LoggingService.Upload.Information("Share connection test passed: {Dir}", dataDir);
            else
                LoggingService.Upload.Warning(
                    "Share data directory is not accessible and could not be created: {Dir}", dataDir);
            return Task.FromResult(ok);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Warning(ex,
                "Share connection test failed for: {Base}", _cfg.BaseSharePath);
            return Task.FromResult(false);
        }
    }

    private void EnsureConnected()
    {
        if (_connected) return;
        lock (_connectLock)
        {
            if (_connected) return;
            if (!string.IsNullOrWhiteSpace(_cfg.ShareUsername))
            {
                LoggingService.Upload.Debug(
                    "Connecting to share with credentials — share: {Share} | user: {User}",
                    _cfg.BaseSharePath,
                    string.IsNullOrEmpty(_cfg.ShareDomain)
                        ? _cfg.ShareUsername
                        : $"{_cfg.ShareDomain}\\{_cfg.ShareUsername}");
                ConnectWithCredentials();
            }
            else
            {
                LoggingService.Upload.Debug(
                    "Using service account credentials for share: {Share}", _cfg.BaseSharePath);
            }
            _connected = true;
        }
    }

    private void ConnectWithCredentials()
    {
        var resource = new NETRESOURCE
        {
            dwType = RESOURCETYPE_DISK,
            lpRemoteName = _cfg.BaseSharePath
        };

        var user = string.IsNullOrEmpty(_cfg.ShareDomain)
            ? _cfg.ShareUsername
            : $"{_cfg.ShareDomain}\\{_cfg.ShareUsername}";

        var result = WNetAddConnection2(ref resource, _cfg.SharePassword, user, 0);

        if (result == ERROR_ALREADY_ASSIGNED || result == ERROR_SESSION_CREDENTIAL_CONFLICT)
        {
            LoggingService.Upload.Debug(
                "Share already connected (Win32 code {Code}): {Share}", result, _cfg.BaseSharePath);
            return;
        }

        if (result != 0)
            throw new InvalidOperationException(
                $"WNetAddConnection2 failed for '{_cfg.BaseSharePath}'. Win32 error code: {result}. " +
                "Check that BaseSharePath, ShareUsername, SharePassword and ShareDomain are correct.");

        LoggingService.Upload.Information("Connected to share: {Share}", _cfg.BaseSharePath);
    }

    private string BuildRemotePath(string subdirectory) =>
        string.IsNullOrWhiteSpace(subdirectory)
            ? _cfg.BaseSharePath
            : Path.Combine(_cfg.BaseSharePath, subdirectory);

    private static string ResolveUniqueDestPath(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path)) return path;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        return Path.Combine(dir, $"{name}_{DateTime.Now:HHmmss}{ext}");
    }

    private static bool TryCreateDirectory(string path)
    {
        try { Directory.CreateDirectory(path); return true; }
        catch (Exception ex)
        {
            LoggingService.Upload.Debug("Could not create directory {Path}: {Error}", path, ex.Message);
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_connected && !string.IsNullOrWhiteSpace(_cfg.ShareUsername))
        {
            LoggingService.Upload.Debug("Disconnecting from share: {Share}", _cfg.BaseSharePath);
            WNetCancelConnection2(_cfg.BaseSharePath, 0, fForce: false);
        }
    }

    private const int RESOURCETYPE_DISK = 1;
    private const int ERROR_ALREADY_ASSIGNED = 85;
    private const int ERROR_SESSION_CREDENTIAL_CONFLICT = 1219;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NETRESOURCE
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(
        ref NETRESOURCE lpNetResource,
        string? lpPassword,
        string? lpUserName,
        uint dwFlags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(
        string lpName,
        uint dwFlags,
        bool fForce);
}
