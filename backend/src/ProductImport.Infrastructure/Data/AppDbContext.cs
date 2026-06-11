using Microsoft.EntityFrameworkCore;
using ProductImport.Core.Entities;

namespace ProductImport.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ImportJob configuration
        modelBuilder.Entity<ImportJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.ExchangeRatesJson).IsRequired();
            entity.Property(e => e.StartedAt).IsRequired();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
        });

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.OriginalPrice).IsRequired().HasPrecision(18, 2);
            entity.Property(e => e.ConvertedPricesJson).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ImportJobId).IsRequired();
            
            entity.HasOne(e => e.ImportJob)
                  .WithMany(i => i.Products)
                  .HasForeignKey(e => e.ImportJobId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.ImportJobId);
            entity.HasIndex(e => e.OriginalPrice);
            entity.HasIndex(e => e.ExpirationDate);
        });

        // PostgreSQL-specific: Use JSONB for the JSON columns
        modelBuilder.Entity<ImportJob>()
            .Property(e => e.ExchangeRatesJson)
            .HasColumnType("jsonb");

        modelBuilder.Entity<Product>()
            .Property(e => e.ConvertedPricesJson)
            .HasColumnType("jsonb");
    }
}
