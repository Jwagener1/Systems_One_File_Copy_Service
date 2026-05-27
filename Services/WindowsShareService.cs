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

/// <summary>
/// Copies files to a Windows share (UNC path). Optionally connects with explicit
/// credentials via WNetAddConnection2 when the service account lacks direct access.
/// </summary>
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
    }

    public async Task CopyFileAsync(string localPath, string remoteDirectory, CancellationToken ct)
    {
        EnsureConnected();

        var destDir = BuildRemotePath(remoteDirectory);
        Directory.CreateDirectory(destDir);

        var fileName = Path.GetFileName(localPath);
        var finalPath = ResolveUniqueDestPath(destDir, fileName);
        var tempPath = finalPath + ".uploading";

        // Atomic: write to .uploading then rename to final name
        await Task.Run(() =>
        {
            File.Copy(localPath, tempPath, overwrite: true);
            File.Move(tempPath, finalPath, overwrite: false);
        }, ct);

        LoggingService.Upload.Information("Copied to share: {Dest}", finalPath);
    }

    public Task<bool> TestConnectionAsync()
    {
        try
        {
            EnsureConnected();
            var dataDir = BuildRemotePath(_cfg.DataRemoteDirectory);
            var ok = Directory.Exists(dataDir) || TryCreateDirectory(dataDir);
            if (ok)
                LoggingService.Upload.Information("Share connection test passed: {Path}", dataDir);
            else
                LoggingService.Upload.Warning("Share path not accessible: {Path}", dataDir);
            return Task.FromResult(ok);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Warning(ex, "Share connection test failed.");
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
                ConnectWithCredentials();
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

        if (result != 0 && result != ERROR_ALREADY_ASSIGNED && result != ERROR_SESSION_CREDENTIAL_CONFLICT)
            throw new InvalidOperationException(
                $"WNetAddConnection2 failed for '{_cfg.BaseSharePath}'. Win32 error: {result}");

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
        catch { return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connected && !string.IsNullOrWhiteSpace(_cfg.ShareUsername))
            WNetCancelConnection2(_cfg.BaseSharePath, 0, fForce: false);
    }

    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

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
