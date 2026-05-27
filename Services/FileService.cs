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
        var sourceFolder = _settings.FileSettings.Image.SourceFolder;
        var expectedFileName = $"{SanitizeFileName(record.Barcode ?? string.Empty)}-{record.ItemDateTime:yyyyMMddHHmmss}.jpg";
        var expectedPath = Path.Combine(sourceFolder, expectedFileName);

        LoggingService.Upload.Debug("Looking for image: {Path}", expectedPath);

        if (File.Exists(expectedPath))
        {
            LoggingService.Upload.Debug("Image found: {Path}", expectedPath);
            return Task.FromResult<string?>(expectedPath);
        }

        LoggingService.Upload.Debug(
            "Image not found at: {Path} (source folder exists: {FolderExists})",
            expectedPath,
            Directory.Exists(sourceFolder));

        return Task.FromResult<string?>(null);
    }

    public async Task<string> CopyImageToArchiveAsync(string sourcePath, string fileName, CancellationToken ct)
    {
        var archiveDir = Path.Combine(_settings.FileSettings.Image.ArchiveFolder, DateTime.Now.ToString("yyyy-MM-dd"));
        LoggingService.Upload.Debug("Image archive directory: {Dir}", archiveDir);

        try
        {
            Directory.CreateDirectory(archiveDir);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "Failed to create image archive directory: {Dir}", archiveDir);
            throw;
        }

        var dest = ResolveUniqueFilePath(Path.Combine(archiveDir, fileName));
        LoggingService.Upload.Debug("Copying image to archive: {Src} → {Dest}", sourcePath, dest);

        try
        {
            await Task.Run(() => File.Copy(sourcePath, dest), ct);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "Failed to copy image to archive: {Src} → {Dest}", sourcePath, dest);
            throw;
        }

        LoggingService.Upload.Information("Image archived: {Path}", dest);
        return dest;
    }

    public void DeleteSourceFile(string filePath)
    {
        LoggingService.Upload.Debug("Deleting source image: {Path}", filePath);
        try
        {
            File.Delete(filePath);
            LoggingService.Upload.Information("Source image deleted: {Path}", filePath);
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
