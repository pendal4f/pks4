using Microsoft.EntityFrameworkCore;
using pks5.Models;

namespace pks5.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<ProductionLine> ProductionLines => Set<ProductionLine>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<ProductMaterial> ProductMaterials => Set<ProductMaterial>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProductMaterial>()
            .HasKey(pm => new { pm.ProductId, pm.MaterialId });

        modelBuilder.Entity<ProductMaterial>()
            .Property(pm => pm.QuantityNeeded)
            .HasPrecision(18, 3);

        modelBuilder.Entity<Material>()
            .Property(m => m.Quantity)
            .HasPrecision(18, 3);

        modelBuilder.Entity<Material>()
            .Property(m => m.MinimalStock)
            .HasPrecision(18, 3);

        modelBuilder.Entity<ProductionLine>()
            .Property(l => l.EfficiencyFactor)
            .HasPrecision(4, 2);

        modelBuilder.Entity<WorkOrder>()
            .Property(w => w.ProgressPercent)
            .HasPrecision(5, 2);

        modelBuilder.Entity<WorkOrder>()
            .HasIndex(w => w.Status);

        modelBuilder.Entity<WorkOrder>()
            .HasOne(w => w.ProductionLine)
            .WithMany(l => l.WorkOrders)
            .HasForeignKey(w => w.ProductionLineId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

