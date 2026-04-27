using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;
using pks5.Services;

namespace pks5.Controllers;

public sealed class OrdersController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? status = null)
    {
        IQueryable<WorkOrder> query = db.WorkOrders.AsNoTracking().Include(o => o.Product).Include(o => o.ProductionLine);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        var orders = await query.OrderByDescending(o => o.Id).ToListAsync();
        ViewBag.Status = status;
        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int productId, int quantity, int? lineId)
    {
        if (quantity <= 0) return RedirectToAction(nameof(Index));

        var product = await db.Products
            .Include(p => p.ProductMaterials)
            .ThenInclude(pm => pm.Material)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product is null) return RedirectToAction(nameof(Index));

        ProductionLine? line = null;
        if (lineId is not null)
        {
            line = await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == lineId.Value);
            if (line is null) return RedirectToAction(nameof(Index));
        }

        var shortages = new List<string>();
        foreach (var pm in product.ProductMaterials)
        {
            var need = pm.QuantityNeeded * quantity;
            if (pm.Material.Quantity < need)
                shortages.Add(pm.Material.Name);
        }
        if (shortages.Count > 0) return RedirectToAction(nameof(Index));

        var eff = line?.EfficiencyFactor ?? 1.0m;
        var minutes = ProductionCalculator.CalculateProductionMinutes(quantity, product.ProductionTimePerUnit, eff);

        var start = DateTime.Now;
        var order = new WorkOrder
        {
            ProductId = product.Id,
            ProductionLineId = line?.Id,
            Quantity = quantity,
            StartDate = start,
            EstimatedEndDate = start.AddMinutes(minutes),
            Status = WorkOrderStatus.Pending,
            ProgressPercent = 0
        };

        db.WorkOrders.Add(order);
        foreach (var pm in product.ProductMaterials)
            pm.Material.Quantity -= pm.QuantityNeeded * quantity;

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        var order = await db.WorkOrders.Include(o => o.ProductionLine).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return RedirectToAction(nameof(Index));

        if (order.Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
            return RedirectToAction(nameof(Index));

        order.Status = WorkOrderStatus.InProgress;
        order.ProgressPercent = Math.Max(order.ProgressPercent, 1);

        if (order.ProductionLineId is not null)
        {
            var line = await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == order.ProductionLineId.Value);
            if (line is not null)
                line.CurrentWorkOrderId = order.Id;
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await db.WorkOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return RedirectToAction(nameof(Index));

        if (order.Status == WorkOrderStatus.Completed) return RedirectToAction(nameof(Index));

        order.Status = WorkOrderStatus.Cancelled;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

