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
        await UpdateOrderProgressAsync();

        IQueryable<WorkOrder> query = db.WorkOrders.AsNoTracking().Include(o => o.Product).Include(o => o.ProductionLine);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        var orders = await query.OrderByDescending(o => o.Id).ToListAsync();
        ViewBag.Status = status;
        ViewBag.Products = await db.Products.AsNoTracking().OrderBy(p => p.Name).Select(p => p.Name).ToListAsync();
        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string productName, int quantity, int? lineId)
    {
        if (quantity <= 0) return RedirectToAction(nameof(Index));

        if (string.IsNullOrWhiteSpace(productName))
            return RedirectToAction(nameof(Index));

        // SQLite lower()/NOCASE корректно работают не для всех символов (например, кириллицы),
        // поэтому делаем case-insensitive поиск в .NET.
        var inputName = productName.Trim();
        var productId = await db.Products.AsNoTracking()
            .Select(p => new { p.Id, p.Name })
            .ToListAsync();
        var matchedId = productId
            .Where(p => string.Equals(p.Name, inputName, StringComparison.OrdinalIgnoreCase))
            .Select(p => (int?)p.Id)
            .FirstOrDefault();

        if (matchedId is null) return RedirectToAction(nameof(Index));

        var product = await db.Products
            .Include(p => p.ProductMaterials)
            .ThenInclude(pm => pm.Material)
            .FirstOrDefaultAsync(p => p.Id == matchedId.Value);

        if (product is null) return RedirectToAction(nameof(Index));

        ProductionLine? line = null;
        if (lineId is not null)
        {
            line = await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == lineId.Value);
            if (line is null) return RedirectToAction(nameof(Index));
            if (line.Status != LineStatus.Active) return RedirectToAction(nameof(Index));
            if (line.CurrentWorkOrderId is not null) return RedirectToAction(nameof(Index));
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
        order.ProgressPercent = Math.Round(Math.Max(order.ProgressPercent, 1), 3);

        if (order.ProductionLineId is not null)
        {
            var line = await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == order.ProductionLineId.Value);
            if (line is not null && line.Status == LineStatus.Active)
                line.CurrentWorkOrderId = order.Id;
            else
            {
                TempData["Dialog"] = "Нельзя запустить заказ: линия остановлена.";
                order.Status = WorkOrderStatus.Pending;
                order.ProgressPercent = 0;
            }
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await db.WorkOrders
            .Include(o => o.Product)
            .ThenInclude(p => p.ProductMaterials)
            .ThenInclude(pm => pm.Material)
            .Include(o => o.ProductionLine)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return RedirectToAction(nameof(Index));

        if (order.Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled) return RedirectToAction(nameof(Index));

        var progress = Math.Clamp(order.ProgressPercent, 0m, 100m);
        var remainingFactor = Math.Clamp(1m - (progress / 100m), 0m, 1m);

        if (remainingFactor > 0)
        {
            foreach (var pm in order.Product.ProductMaterials)
                pm.Material.Quantity += pm.QuantityNeeded * order.Quantity * remainingFactor;
        }

        TempData["Dialog"] = progress <= 0
            ? "Заказ отменён до запуска. Резерв материалов возвращён на склад."
            : $"Заказ отменён на {progress:0.##}%. Возвращено материалов: {(remainingFactor * 100m):0.##}% от резерва.";

        if (order.ProductionLine?.CurrentWorkOrderId == order.Id)
            order.ProductionLine.CurrentWorkOrderId = null;

        order.Status = WorkOrderStatus.Cancelled;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task UpdateOrderProgressAsync()
    {
        var now = DateTime.Now;

        // Если линия остановлена во время выполнения заказа — отменяем заказ и возвращаем неиспользованные материалы.
        var stoppedLineOrders = await db.WorkOrders
            .Include(o => o.Product)
            .ThenInclude(p => p.ProductMaterials)
            .ThenInclude(pm => pm.Material)
            .Include(o => o.ProductionLine)
            .Where(o =>
                o.Status == WorkOrderStatus.InProgress &&
                o.ProductionLineId != null &&
                o.ProductionLine != null &&
                o.ProductionLine.Status == LineStatus.Stopped)
            .ToListAsync();

        if (stoppedLineOrders.Count > 0)
        {
            foreach (var o in stoppedLineOrders)
            {
                var progress = Math.Clamp(o.ProgressPercent, 0m, 100m);
                var remainingFactor = Math.Clamp(1m - (progress / 100m), 0m, 1m);
                if (remainingFactor > 0)
                {
                    foreach (var pm in o.Product.ProductMaterials)
                        pm.Material.Quantity += pm.QuantityNeeded * o.Quantity * remainingFactor;
                }

                o.Status = WorkOrderStatus.Cancelled;
                if (o.ProductionLine?.CurrentWorkOrderId == o.Id)
                    o.ProductionLine.CurrentWorkOrderId = null;
            }

            await db.SaveChangesAsync();
        }

        var inProgress = await db.WorkOrders
            .Include(o => o.ProductionLine)
            .Where(o => o.Status == WorkOrderStatus.InProgress)
            .ToListAsync();

        var changed = false;
        foreach (var o in inProgress)
        {
            var total = (o.EstimatedEndDate - o.StartDate).TotalSeconds;
            if (total > 0)
            {
                var elapsed = (now - o.StartDate).TotalSeconds;
                var pct = (decimal)(elapsed / total) * 100m;
                pct = Math.Clamp(pct, 0m, 100m);
                pct = Math.Round(pct, 3);
                if (pct > o.ProgressPercent)
                {
                    o.ProgressPercent = pct;
                    changed = true;
                }
            }

            if (now >= o.EstimatedEndDate)
            {
                o.ProgressPercent = 100;
                o.Status = WorkOrderStatus.Completed;
                if (o.ProductionLine?.CurrentWorkOrderId == o.Id)
                    o.ProductionLine.CurrentWorkOrderId = null;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }
}
