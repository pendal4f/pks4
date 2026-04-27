using Microsoft.EntityFrameworkCore;
using pks4.Models;

namespace pks4.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<Attraction> Attractions => Set<Attraction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<City>()
            .HasMany(c => c.Attractions)
            .WithOne(a => a.City)
            .HasForeignKey(a => a.CityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attraction>()
            .Property(a => a.VisitCost)
            .HasPrecision(18, 2);
    }
}
