using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;

namespace pks5.Controllers;

public sealed class ProductsController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? category = null, string? q = null)
    {
        IQueryable<Product> query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q));

        var items = await query.OrderBy(p => p.Name).ToListAsync();
        ViewBag.Category = category;
        ViewBag.Query = q;
        ViewBag.Categories = await db.Products.AsNoTracking()
            .Where(p => p.Category != null && p.Category != "")
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> EditMaterials(int id)
    {
        var product = await db.Products
            .Include(p => p.ProductMaterials)
            .ThenInclude(pm => pm.Material)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null) return NotFound();

        var materials = await db.Materials.AsNoTracking().OrderBy(m => m.Name).ToListAsync();
        return View(new EditProductMaterialsVm(product, materials));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMaterials(int id, Dictionary<int, decimal> qtyNeeded)
    {
        var product = await db.Products
            .Include(p => p.ProductMaterials)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null) return NotFound();

        var existing = product.ProductMaterials.ToDictionary(pm => pm.MaterialId, pm => pm);

        foreach (var (materialId, qty) in qtyNeeded)
        {
            if (qty <= 0)
                continue;

            if (existing.TryGetValue(materialId, out var pm))
            {
                pm.QuantityNeeded = qty;
            }
            else
            {
                product.ProductMaterials.Add(new ProductMaterial
                {
                    ProductId = product.Id,
                    MaterialId = materialId,
                    QuantityNeeded = qty
                });
            }
        }

        // Remove zeroed / missing
        var toRemove = product.ProductMaterials
            .Where(pm => !qtyNeeded.TryGetValue(pm.MaterialId, out var qty) || qty <= 0)
            .ToList();
        db.ProductMaterials.RemoveRange(toRemove);

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public sealed record EditProductMaterialsVm(Product Product, IReadOnlyList<Material> Materials);
}

