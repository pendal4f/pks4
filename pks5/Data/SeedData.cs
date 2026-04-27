using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using pks5.Models;

namespace pks5.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

        if (!await db.Materials.AnyAsync())
        {
            db.Materials.AddRange(
                new Material { Name = "Сталь", Quantity = 450, UnitOfMeasure = "кг", MinimalStock = 120 },
                new Material { Name = "Пластик", Quantity = 180, UnitOfMeasure = "кг", MinimalStock = 80 },
                new Material { Name = "Микросхемы", Quantity = 900, UnitOfMeasure = "шт", MinimalStock = 300 },
                new Material { Name = "Краска", Quantity = 60, UnitOfMeasure = "л", MinimalStock = 25 }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.ProductionLines.AnyAsync())
        {
            db.ProductionLines.AddRange(
                new ProductionLine { Name = "Линия A", Status = LineStatus.Active, EfficiencyFactor = 1.0m },
                new ProductionLine { Name = "Линия B", Status = LineStatus.Active, EfficiencyFactor = 1.2m },
                new ProductionLine { Name = "Линия C", Status = LineStatus.Stopped, EfficiencyFactor = 0.9m }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product
                {
                    Name = "Насос",
                    Category = "Оборудование",
                    Description = "Промышленный насос для перекачки жидкостей.",
                    SpecificationsJson = "{\"power_kw\": 7.5, \"material\": \"steel\"}",
                    MinimalStock = 5,
                    ProductionTimePerUnit = 90
                },
                new Product
                {
                    Name = "Датчик давления",
                    Category = "Электроника",
                    Description = "Датчик для контроля давления в системе.",
                    SpecificationsJson = "{\"range_bar\": 0..16, \"output\": \"4-20mA\"}",
                    MinimalStock = 20,
                    ProductionTimePerUnit = 25
                },
                new Product
                {
                    Name = "Кронштейн",
                    Category = "Комплектующие",
                    Description = "Крепёжный кронштейн для монтажа.",
                    SpecificationsJson = "{\"thickness_mm\": 3, \"coating\": \"paint\"}",
                    MinimalStock = 50,
                    ProductionTimePerUnit = 8
                }
            );
            await db.SaveChangesAsync();
        }

        // Link product materials (idempotent)
        var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Name, p => p.Id);
        var materials = await db.Materials.AsNoTracking().ToDictionaryAsync(m => m.Name, m => m.Id);

        await UpsertProductMaterial(db, products["Насос"], materials["Сталь"], 12.5m);
        await UpsertProductMaterial(db, products["Насос"], materials["Краска"], 0.3m);

        await UpsertProductMaterial(db, products["Датчик давления"], materials["Пластик"], 0.2m);
        await UpsertProductMaterial(db, products["Датчик давления"], materials["Микросхемы"], 2m);

        await UpsertProductMaterial(db, products["Кронштейн"], materials["Сталь"], 0.8m);
        await UpsertProductMaterial(db, products["Кронштейн"], materials["Краска"], 0.05m);

        await db.SaveChangesAsync();

        if (!await db.WorkOrders.AnyAsync())
        {
            var lineA = await db.ProductionLines.AsNoTracking().FirstAsync(l => l.Name == "Линия A");
            var pumpId = products["Насос"];

            db.WorkOrders.Add(new WorkOrder
            {
                ProductId = pumpId,
                ProductionLineId = lineA.Id,
                Quantity = 3,
                StartDate = DateTime.Today,
                EstimatedEndDate = DateTime.Today.AddHours(6),
                Status = WorkOrderStatus.Pending,
                ProgressPercent = 0
            });

            await db.SaveChangesAsync();
        }
    }

    private static async Task UpsertProductMaterial(AppDbContext db, int productId, int materialId, decimal qtyNeeded)
    {
        var existing = await db.ProductMaterials.FirstOrDefaultAsync(pm =>
            pm.ProductId == productId && pm.MaterialId == materialId);

        if (existing is null)
        {
            db.ProductMaterials.Add(new ProductMaterial
            {
                ProductId = productId,
                MaterialId = materialId,
                QuantityNeeded = qtyNeeded
            });
        }
        else
        {
            existing.QuantityNeeded = qtyNeeded;
        }
    }
}

