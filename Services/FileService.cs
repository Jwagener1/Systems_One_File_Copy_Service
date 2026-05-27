using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;

namespace SystemsOne.FileCopyService.Services;

public interface IFileService
{
    Task<string?> FindImageAsync(UploadRecord record, CancellationToken ct);
    Task<string> CopyImageToArchiveAsync(string sourcePath, string fileName, CancellationToken ct);
    void DeleteSourceFile(string filePath);
}

public class FileService : IFileService
{
    private readonly AppSettings _settings;

    public FileService(AppSettings settings)
    {
        _settings = settings;
    }

    public Task<string?> FindImageAsync(UploadRecord record, CancellationToken ct)
    {
        var expected = Path.Combine(
            _settings.FileSettings.Image.SourceFolder,
            $"{SanitizeFileName(record.Barcode)}-{record.ItemDateTime:yyyyMMddHHmmss}.jpg");

        return Task.FromResult(File.Exists(expected) ? expected : null);
    }

    public async Task<string> CopyImageToArchiveAsync(string sourcePath, string fileName, CancellationToken ct)
    {
        var archiveDir = Path.Combine(_settings.FileSettings.Image.ArchiveFolder, DateTime.Now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(archiveDir);

        var dest = ResolveUniqueFilePath(Path.Combine(archiveDir, fileName));
        await Task.Run(() => File.Copy(sourcePath, dest), ct);
        LoggingService.Upload.Information("Image archived to: {Path}", dest);
        return dest;
    }

    public void DeleteSourceFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
            LoggingService.Upload.Information("Deleted source image: {Path}", filePath);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Warning(ex, "Could not delete source image: {Path}", filePath);
        }
    }

    private static string ResolveUniqueFilePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        return Path.Combine(dir, $"{name}_{DateTime.Now:HHmmss}{ext}");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
