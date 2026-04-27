using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;

namespace pks5.Controllers;

public sealed class LinesController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var lines = await db.ProductionLines.AsNoTracking().OrderBy(l => l.Name).ToListAsync();
        var activeOrders = await db.WorkOrders.AsNoTracking()
            .Where(o => o.Status == WorkOrderStatus.InProgress)
            .Include(o => o.Product)
            .ToListAsync();

        return View(new LinesVm(lines, activeOrders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string status, decimal efficiencyFactor)
    {
        var line = await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == id);
        if (line is null) return RedirectToAction(nameof(Index));

        if (status is LineStatus.Active or LineStatus.Stopped)
            line.Status = status;

        if (efficiencyFactor >= 0.5m && efficiencyFactor <= 2.0m)
            line.EfficiencyFactor = efficiencyFactor;

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public sealed record LinesVm(IReadOnlyList<ProductionLine> Lines, IReadOnlyList<WorkOrder> ActiveOrders);
}

