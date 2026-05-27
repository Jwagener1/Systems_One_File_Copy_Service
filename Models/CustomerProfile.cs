namespace SystemsOne.FileCopyService.Models;

public class CustomerProfile
{
    public int ProfileVersion { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FileNameProfile FileName { get; set; } = new();
    public CsvProfile Csv { get; set; } = new();
    public List<ColumnProfile> Columns { get; set; } = new();
}

public class FileNameProfile
{
    public string Prefix { get; set; } = string.Empty;
    public bool IncludeBarcodeInFileName { get; set; }
    public string DateTimeSource { get; set; } = "ItemDateTime"; // "ItemDateTime" | "Now"
    public string DateTimeFormat { get; set; } = "yyyyMMddHHmmss";
    public string Separator { get; set; } = "_";
    public string Extension { get; set; } = ".csv";
}

public class CsvProfile
{
    public bool IncludeHeaders { get; set; }
    public string Delimiter { get; set; } = ",";
    public string Quote { get; set; } = "Always"; // "Always" | "Never" | "Minimal"
    public string QuoteCharacter { get; set; } = "\"";
    public string DecimalSeparator { get; set; } = ".";
    public string NumberFormat { get; set; } = "F2";
    public string DateFormat { get; set; } = "yyyyMMdd";
    public string TimeFormat { get; set; } = "HHmmss";
    public string NullLiteral { get; set; } = "";
    public string Encoding { get; set; } = "UTF-8"; // "UTF-8" | "UTF-8-BOM" | "ASCII" | "Windows-1252"
    public string LineEnding { get; set; } = "CRLF"; // "CRLF" | "LF"
    public string HubCode { get; set; } = string.Empty;
}

public class ColumnProfile
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Constant { get; set; }
    public decimal? DimWeightFactor { get; set; }
}

public enum ColumnSource
{
    Barcode,
    ItemDateTime,
    DateOnly,
    TimeOnly,
    Length,
    Width,
    Height,
    Weight,
    BoxVolume,
    LiquidVolume,
    ItemCount,
    ItemSpec,
    DateTimeBarcode,
    HubCode,
    Constant,
    Null,
    ComputedVolumeLxWxH,
    ComputedDimensionalWeight
}
