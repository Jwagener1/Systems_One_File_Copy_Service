namespace SystemsOne.FileCopyService.Models;

public class UploadRecord
{
    public int Id { get; set; }
    public DateTime ItemDateTime { get; set; }
    public string? Barcode { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
    public long? BoxVolume { get; set; }
    public long? LiquidVolume { get; set; }
    public bool? NoDimension { get; set; }
    public bool? NoWeight { get; set; }
    public bool? Sent { get; set; }
    public bool? ImageSent { get; set; }
    public bool? Valid { get; set; }
    public bool? Complete { get; set; }
    public short? ItemSpec { get; set; }
    public short? ItemCount { get; set; }
    public string? StoreId { get; set; }
    public string? StoreName { get; set; }
    public bool NoData { get; set; }
    public string? ErrorDescription { get; set; }
    public string Direction { get; set; } = "Forward";
    public string? TransactionType { get; set; }
}
