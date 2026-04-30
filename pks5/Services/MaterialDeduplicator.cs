using pks5.Data;
using pks5.Models;

namespace pks5.Services;

public static class MaterialDeduplicator
{
    public static async Task MergeDuplicatesAsync(AppDbContext db)
    {
        var materials = db.Materials.ToList();
        if (materials.Count <= 1)
            return;

        static string Key(Material m)
            => $"{Normalize(m.Name)}|{Normalize(m.UnitOfMeasure)}";

        var groups = materials.GroupBy(Key).Where(g => g.Count() > 1).ToList();
        if (groups.Count == 0)
            return;

        foreach (var g in groups)
        {
            var keep = g.OrderBy(m => m.Id).First();
            var remove = g.Where(m => m.Id != keep.Id).ToList();

            keep.Quantity = g.Sum(m => m.Quantity);
            keep.MinimalStock = g.Max(m => m.MinimalStock);
            keep.Name = keep.Name.Trim();
            keep.UnitOfMeasure = keep.UnitOfMeasure.Trim();

            db.Materials.RemoveRange(remove);
        }

        await db.SaveChangesAsync();
    }

    private static string Normalize(string? s)
        => (s ?? string.Empty).Trim().ToLowerInvariant();
}

