namespace SystemsOne.FileCopyService.Models;

public class UploadRecord
{
    public int Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public DateTime ItemDateTime { get; set; }
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Weight { get; set; }
    public decimal BoxVolume { get; set; }
    public decimal LiquidVolume { get; set; }
    public short ItemCount { get; set; }
    public string ItemSpec { get; set; } = string.Empty;
    public bool Valid { get; set; }
    public bool? Sent { get; set; }
    public bool? Complete { get; set; }
}
