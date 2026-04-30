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

        var prevStatus = line.Status;
        if (status is LineStatus.Active or LineStatus.Stopped)
            line.Status = status;

        if (efficiencyFactor >= 0.5m && efficiencyFactor <= 2.0m)
            line.EfficiencyFactor = efficiencyFactor;

        // Если остановили линию — отменяем активный заказ и возвращаем неиспользованные материалы.
        if (prevStatus != LineStatus.Stopped && line.Status == LineStatus.Stopped)
        {
            var orders = await db.WorkOrders
                .Include(o => o.Product)
                .ThenInclude(p => p.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .Where(o =>
                    o.ProductionLineId == line.Id &&
                    (o.Status == WorkOrderStatus.Pending || o.Status == WorkOrderStatus.InProgress))
                .ToListAsync();

            foreach (var o in orders)
            {
                var progress = Math.Clamp(o.ProgressPercent, 0m, 100m);
                var remainingFactor = Math.Clamp(1m - (progress / 100m), 0m, 1m);
                if (remainingFactor > 0)
                {
                    foreach (var pm in o.Product.ProductMaterials)
                        pm.Material.Quantity += pm.QuantityNeeded * o.Quantity * remainingFactor;
                }

                o.Status = WorkOrderStatus.Cancelled;
            }

            line.CurrentWorkOrderId = null;
            if (orders.Count > 0)
                TempData["Dialog"] = $"Линия остановлена. Активные заказы отменены ({orders.Count}). Неиспользованные материалы возвращены на склад.";
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public sealed record LinesVm(IReadOnlyList<ProductionLine> Lines, IReadOnlyList<WorkOrder> ActiveOrders);
}
