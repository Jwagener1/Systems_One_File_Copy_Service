using Microsoft.EntityFrameworkCore;
using SystemsOne.FileCopyService.Models;

namespace SystemsOne.FileCopyService.Data;

public class ApplicationDbContext : DbContext
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public ApplicationDbContext(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _tableName = tableName;
    }

    public DbSet<UploadRecord> UploadRecords { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UploadRecord>(entity =>
        {
            entity.ToTable(_tableName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Barcode).HasMaxLength(200);
            entity.Property(e => e.ItemSpec).HasMaxLength(500);
            entity.Property(e => e.Length).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Width).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Height).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Weight).HasColumnType("decimal(18,4)");
            entity.Property(e => e.BoxVolume).HasColumnType("decimal(18,4)");
            entity.Property(e => e.LiquidVolume).HasColumnType("decimal(18,4)");
        });
    }
}
