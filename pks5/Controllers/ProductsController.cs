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

        // Для проверки "товар с таким названием уже есть" не используем отфильтрованный список,
        // иначе модалка не появится при активных фильтрах/поиске.
        var allForMismatch = await db.Products.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                category = p.Category,
                time = p.ProductionTimePerUnit,
                min = p.MinimalStock
            })
            .ToListAsync();
        ViewBag.ProductsForMismatchJson = System.Text.Json.JsonSerializer.Serialize(allForMismatch);

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string name,
        int productionTimePerUnit,
        string? category,
        int minimalStock,
        string? description,
        string? specificationsJson)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Dialog"] = "Название продукта обязательно.";
            return RedirectToAction(nameof(Index));
        }

        if (productionTimePerUnit <= 0)
        {
            TempData["Dialog"] = "Время производства (мин/шт) должно быть > 0.";
            return RedirectToAction(nameof(Index));
        }

        if (minimalStock < 0)
        {
            TempData["Dialog"] = "Минимальный запас не может быть отрицательным.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedName = name.Trim();
        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        // Для кириллицы не полагаемся на SQL lower()/NOCASE.
        var allNames = await db.Products.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync();
        var existingId = allNames
            .Where(p => string.Equals(p.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            .Select(p => (int?)p.Id)
            .FirstOrDefault();

        var existing = existingId is null
            ? null
            : await db.Products.OrderBy(p => p.Id).FirstOrDefaultAsync(p => p.Id == existingId.Value);

        if (existing is null)
        {
            db.Products.Add(new Product
            {
                Name = normalizedName,
                Category = normalizedCategory,
                ProductionTimePerUnit = productionTimePerUnit,
                MinimalStock = minimalStock,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                SpecificationsJson = string.IsNullOrWhiteSpace(specificationsJson) ? null : specificationsJson.Trim()
            });

            await db.SaveChangesAsync();
            TempData["Dialog"] = $"Продукт \"{normalizedName}\" добавлен.";
            return RedirectToAction(nameof(Index));
        }

        var sameCategory = string.Equals(existing.Category ?? string.Empty, normalizedCategory ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var sameTime = existing.ProductionTimePerUnit == productionTimePerUnit;

        if (sameCategory && sameTime)
        {
            existing.MinimalStock = Math.Max(existing.MinimalStock, minimalStock);
            if (!string.IsNullOrWhiteSpace(description))
                existing.Description = description.Trim();
            if (!string.IsNullOrWhiteSpace(specificationsJson))
                existing.SpecificationsJson = specificationsJson.Trim();

            await db.SaveChangesAsync();
            TempData["Dialog"] = $"Продукт \"{existing.Name}\" обновлён (минимальный запас/описание).";
            return RedirectToAction(nameof(Index));
        }

        TempData["Dialog"] = $"Продукт \"{normalizedName}\" уже существует с другими параметрами. Выберите действие в окне подтверждения.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateForce(
        string name,
        int productionTimePerUnit,
        string? category,
        int minimalStock,
        string? description,
        string? specificationsJson)
    {
        if (string.IsNullOrWhiteSpace(name) || productionTimePerUnit <= 0 || minimalStock < 0)
            return RedirectToAction(nameof(Index));

        db.Products.Add(new Product
        {
            Name = name.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            ProductionTimePerUnit = productionTimePerUnit,
            MinimalStock = minimalStock,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            SpecificationsJson = string.IsNullOrWhiteSpace(specificationsJson) ? null : specificationsJson.Trim()
        });

        await db.SaveChangesAsync();
        TempData["Dialog"] = $"Продукт \"{name.Trim()}\" добавлен отдельно.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateExisting(
        int existingId,
        int productionTimePerUnit,
        string? category,
        int minimalStock,
        string? description,
        string? specificationsJson)
    {
        if (existingId <= 0 || productionTimePerUnit <= 0 || minimalStock < 0)
            return RedirectToAction(nameof(Index));

        var existing = await db.Products.FirstOrDefaultAsync(p => p.Id == existingId);
        if (existing is null)
            return RedirectToAction(nameof(Index));

        existing.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        existing.ProductionTimePerUnit = productionTimePerUnit;
        existing.MinimalStock = minimalStock;
        existing.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        existing.SpecificationsJson = string.IsNullOrWhiteSpace(specificationsJson) ? null : specificationsJson.Trim();

        await db.SaveChangesAsync();
        TempData["Dialog"] = $"Продукт \"{existing.Name}\" обновлён.";
        return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> SaveMaterials(int id, Dictionary<int, string> qtyNeeded)
    {
        var product = await db.Products
            .Include(p => p.ProductMaterials)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null) return NotFound();

        if (!ModelState.IsValid)
        {
            TempData["Dialog"] = "Не удалось сохранить материалы: проверьте введённые значения (используйте число, например 1 или 1.5).";
            return RedirectToAction(nameof(EditMaterials), new { id });
        }

        if (qtyNeeded.Count == 0)
        {
            TempData["Dialog"] = "Не удалось сохранить материалы: форма не передала значения.";
            return RedirectToAction(nameof(EditMaterials), new { id });
        }

        static bool TryParseDecimal(string? s, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s))
                return true;

            s = s.Trim();
            return decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value)
                   || decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), out value);
        }

        var existing = product.ProductMaterials.ToDictionary(pm => pm.MaterialId, pm => pm);

        foreach (var (materialId, qtyStr) in qtyNeeded)
        {
            if (!TryParseDecimal(qtyStr, out var qty))
            {
                TempData["Dialog"] = "Не удалось сохранить материалы: одно из значений не число. Используйте формат 1.5 (или 1,5).";
                return RedirectToAction(nameof(EditMaterials), new { id });
            }

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
            .Where(pm =>
            {
                if (!qtyNeeded.TryGetValue(pm.MaterialId, out var qtyStr))
                    return true;
                if (!TryParseDecimal(qtyStr, out var qty))
                    return false; // keep on parse errors
                return qty <= 0;
            })
            .ToList();
        db.ProductMaterials.RemoveRange(toRemove);

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public sealed record EditProductMaterialsVm(Product Product, IReadOnlyList<Material> Materials);
}
