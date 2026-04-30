using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;
using pks5.Services;

namespace pks5.Controllers;

public sealed class MaterialsController(AppDbContext db) : Controller
{
    private static string Normalize(string s) => s.Trim().ToLowerInvariant();

    [HttpGet]
    public async Task<IActionResult> Index(bool lowStock = false)
    {
        await MaterialDeduplicator.MergeDuplicatesAsync(db);

        IQueryable<Material> query = db.Materials.AsNoTracking();
        if (lowStock)
            query = query.Where(m => m.Quantity <= m.MinimalStock);

        var items = await query.OrderBy(m => m.Name).ToListAsync();
        ViewBag.LowStock = lowStock;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, decimal quantity, string unitOfMeasure, decimal minimalStock)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unitOfMeasure) || quantity < 0 || minimalStock < 0)
            return RedirectToAction(nameof(Index));

        var normalizedName = name.Trim();
        var normalizedUnit = unitOfMeasure.Trim();

        await MaterialDeduplicator.MergeDuplicatesAsync(db);

        var sameName = await db.Materials
            .Where(m => m.Name.ToLower() == Normalize(normalizedName))
            .OrderBy(m => m.Id)
            .ToListAsync();

        var sameUnit = sameName.FirstOrDefault(m => string.Equals(m.UnitOfMeasure, normalizedUnit, StringComparison.OrdinalIgnoreCase));

        if (sameUnit is not null)
        {
            sameUnit.Quantity += quantity;
            sameUnit.MinimalStock = Math.Max(sameUnit.MinimalStock, minimalStock);
        }
        else if (sameName.Count > 0)
        {
            TempData["Dialog"] = $"Материал \"{normalizedName}\" уже существует, но в других единицах измерения. Выберите действие в окне подтверждения.";
            return RedirectToAction(nameof(Index));
        }
        else
        {
            db.Materials.Add(new Material
            {
                Name = normalizedName,
                Quantity = quantity,
                UnitOfMeasure = normalizedUnit,
                MinimalStock = minimalStock
            });
        }

        await db.SaveChangesAsync();

        await MaterialDeduplicator.MergeDuplicatesAsync(db);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateForce(string name, decimal quantity, string unitOfMeasure, decimal minimalStock)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unitOfMeasure) || quantity < 0 || minimalStock < 0)
            return RedirectToAction(nameof(Index));

        db.Materials.Add(new Material
        {
            Name = name.Trim(),
            Quantity = quantity,
            UnitOfMeasure = unitOfMeasure.Trim(),
            MinimalStock = minimalStock
        });

        await db.SaveChangesAsync();
        await MaterialDeduplicator.MergeDuplicatesAsync(db);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToExisting(int existingId, decimal quantity, decimal minimalStock)
    {
        if (existingId <= 0 || quantity < 0 || minimalStock < 0)
            return RedirectToAction(nameof(Index));

        await MaterialDeduplicator.MergeDuplicatesAsync(db);

        var existing = await db.Materials.FirstOrDefaultAsync(m => m.Id == existingId);
        if (existing is null)
            return RedirectToAction(nameof(Index));

        existing.Quantity += quantity;
        existing.MinimalStock = Math.Max(existing.MinimalStock, minimalStock);

        await db.SaveChangesAsync();
        await MaterialDeduplicator.MergeDuplicatesAsync(db);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refill(int id, decimal amount)
    {
        if (amount <= 0)
            return RedirectToAction(nameof(Index));

        var material = await db.Materials.FirstOrDefaultAsync(m => m.Id == id);
        if (material is null)
            return RedirectToAction(nameof(Index));

        material.Quantity += amount;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}