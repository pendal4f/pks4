using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;

namespace pks5.Controllers;

public sealed class MaterialsController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(bool lowStock = false)
    {
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

        db.Materials.Add(new Material
        {
            Name = name.Trim(),
            Quantity = quantity,
            UnitOfMeasure = unitOfMeasure.Trim(),
            MinimalStock = minimalStock
        });

        await db.SaveChangesAsync();
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

