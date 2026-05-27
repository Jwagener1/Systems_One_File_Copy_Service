using System.Text;
using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;

namespace SystemsOne.FileCopyService.Services;

public interface IFileBuilder
{
    Task<string> BuildAsync(UploadRecord record, CancellationToken ct);
}

public class FileBuilder : IFileBuilder
{
    private readonly AppSettings _settings;
    private readonly ICustomerProfileService _profileService;

    public FileBuilder(AppSettings settings, ICustomerProfileService profileService)
    {
        _settings = settings;
        _profileService = profileService;
    }

    public async Task<string> BuildAsync(UploadRecord record, CancellationToken ct)
    {
        var profile = _profileService.GetProfile();

        var archiveFolder = _settings.FileSettings.Data.ArchiveFolder;
        if (string.IsNullOrWhiteSpace(archiveFolder))
            throw new InvalidOperationException("FileSettings.Data.ArchiveFolder is not configured.");

        var outputDir = Path.Combine(archiveFolder, DateTime.Now.ToString("yyyy-MM-dd"));
        LoggingService.Upload.Debug("CSV archive directory: {Dir}", outputDir);

        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "Failed to create CSV archive directory: {Dir}", outputDir);
            throw;
        }

        var fileName = BuildFileName(record, profile.FileName);
        var filePath = ResolveUniqueFilePath(Path.Combine(outputDir, fileName));
        LoggingService.Upload.Debug("CSV output path: {Path}", filePath);

        var lineEnding = profile.Csv.LineEnding == "LF" ? "\n" : "\r\n";
        var encoding = ResolveEncoding(profile.Csv.Encoding);
        var sb = new StringBuilder();

        if (profile.Csv.IncludeHeaders)
        {
            sb.Append(string.Join(profile.Csv.Delimiter, profile.Columns.Select(c => ApplyQuoting(c.Name, profile.Csv))));
            sb.Append(lineEnding);
        }

        sb.Append(string.Join(profile.Csv.Delimiter, profile.Columns.Select(col => RenderColumn(col, record, profile))));
        sb.Append(lineEnding);

        var content = sb.ToString();
        LoggingService.Upload.Debug(
            "Writing CSV: {Path} | {Cols} column(s) | encoding={Enc} | {Bytes} bytes",
            filePath, profile.Columns.Count, profile.Csv.Encoding, encoding.GetByteCount(content));

        try
        {
            await File.WriteAllTextAsync(filePath, content, encoding, ct);
        }
        catch (Exception ex)
        {
            LoggingService.Upload.Error(ex, "Failed to write CSV file: {Path}", filePath);
            throw;
        }

        LoggingService.Upload.Information("CSV written: {Path}", filePath);
        LoggingService.Files.Information("File: {Path}{NewLine}{Content}", filePath, Environment.NewLine, content);

        return filePath;
    }

    private static string BuildFileName(UploadRecord record, FileNameProfile fn)
    {
        var dateTime = fn.DateTimeSource == "Now" ? DateTime.Now : record.ItemDateTime;
        var parts = new List<string> { fn.Prefix };

        if (fn.IncludeBarcodeInFileName)
            parts.Add(SanitizeFileName(record.Barcode));

        parts.Add(dateTime.ToString(fn.DateTimeFormat));

        return string.Join(fn.Separator, parts) + fn.Extension;
    }

    private string RenderColumn(ColumnProfile col, UploadRecord record, CustomerProfile profile)
    {
        if (!Enum.TryParse<ColumnSource>(col.Source, ignoreCase: true, out var source))
        {
            LoggingService.Upload.Warning(
                "Column '{Name}' has unknown Source '{Source}' — writing null literal.",
                col.Name, col.Source);
            return ApplyQuoting(profile.Csv.NullLiteral, profile.Csv);
        }

        var value = ResolveValue(source, col, record, profile);
        return ApplyQuoting(value ?? profile.Csv.NullLiteral, profile.Csv);
    }

    private string? ResolveValue(ColumnSource source, ColumnProfile col, UploadRecord record, CustomerProfile profile)
    {
        var csv = profile.Csv;
        return source switch
        {
            ColumnSource.Barcode              => record.Barcode,
            ColumnSource.ItemDateTime         => record.ItemDateTime.ToString($"{csv.DateFormat} {csv.TimeFormat}"),
            ColumnSource.DateOnly             => record.ItemDateTime.ToString(csv.DateFormat),
            ColumnSource.TimeOnly             => record.ItemDateTime.ToString(csv.TimeFormat),
            ColumnSource.Length               => FormatDecimal(record.Length, csv),
            ColumnSource.Width                => FormatDecimal(record.Width, csv),
            ColumnSource.Height               => FormatDecimal(record.Height, csv),
            ColumnSource.Weight               => FormatDecimal(record.Weight, csv),
            ColumnSource.BoxVolume            => FormatDecimal(record.BoxVolume, csv),
            ColumnSource.LiquidVolume         => FormatDecimal(record.LiquidVolume, csv),
            ColumnSource.ItemCount            => record.ItemCount.ToString(),
            ColumnSource.ItemSpec             => record.ItemSpec,
            ColumnSource.DateTimeBarcode      => $"{record.ItemDateTime.ToString(csv.DateFormat + csv.TimeFormat)}|{record.Barcode}",
            ColumnSource.HubCode              => string.IsNullOrEmpty(csv.HubCode) ? _settings.WindowsShare.Hub_Code : csv.HubCode,
            ColumnSource.Constant             => col.Constant,
            ColumnSource.Null                 => null,
            ColumnSource.ComputedVolumeLxWxH  => FormatDecimal(record.Length * record.Width * record.Height, csv),
            ColumnSource.ComputedDimensionalWeight when col.DimWeightFactor is > 0
                                              => FormatDecimal(record.Length * record.Width * record.Height / col.DimWeightFactor!.Value, csv),
            _                                 => null
        };
    }

    private static string FormatDecimal(decimal value, CsvProfile csv)
    {
        var s = value.ToString(csv.NumberFormat);
        return csv.DecimalSeparator != "." ? s.Replace(".", csv.DecimalSeparator) : s;
    }

    private static string ApplyQuoting(string value, CsvProfile csv)
    {
        var q = csv.QuoteCharacter;
        return csv.Quote switch
        {
            "Always"  => $"{q}{value.Replace(q, q + q)}{q}",
            "Never"   => value,
            "Minimal" => NeedsQuoting(value, csv) ? $"{q}{value.Replace(q, q + q)}{q}" : value,
            _         => value
        };
    }

    private static bool NeedsQuoting(string value, CsvProfile csv) =>
        value.Contains(csv.Delimiter) || value.Contains(csv.QuoteCharacter) ||
        value.Contains('\n') || value.Contains('\r');

    private static string ResolveUniqueFilePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        return Path.Combine(dir, $"{name}_{DateTime.Now:HHmmss}{ext}");
    }

    private static Encoding ResolveEncoding(string name) => name.ToUpperInvariant() switch
    {
        "UTF-8-BOM"    => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        "ASCII"        => Encoding.ASCII,
        "WINDOWS-1252" => Encoding.GetEncoding(1252),
        _              => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
    };

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
