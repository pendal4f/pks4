using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;
using pks5.Services;
using System.Text.Json.Serialization;

namespace pks5.Controllers.Api;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(AppDbContext db) : ControllerBase
{
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

    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> Get(
        [FromQuery(Name = "status")] string? status = null,
        [FromQuery(Name = "date")] string? date = null)
    {
        await UpdateOrderProgressAsync();

        IQueryable<WorkOrder> query = db.WorkOrders.AsNoTracking().Include(o => o.Product).Include(o => o.ProductionLine);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status == WorkOrderStatus.Pending || o.Status == WorkOrderStatus.InProgress);
            }
            else
            {
                query = query.Where(o => o.Status == status);
            }
        }

        if (!string.IsNullOrWhiteSpace(date) && date.Equals("today", StringComparison.OrdinalIgnoreCase))
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            query = query.Where(o => o.StartDate >= today && o.StartDate < tomorrow);
        }

        var result = await query
            .OrderByDescending(o => o.Id)
            .Select(o => new OrderDto(
                o.Id,
                o.ProductId,
                o.Product.Name,
                o.ProductionLineId,
                o.ProductionLine != null ? o.ProductionLine.Name : null,
                o.Quantity,
                o.StartDate,
                o.EstimatedEndDate,
                o.Status,
                o.ProgressPercent))
            .ToListAsync();

        return result;
    }

    public sealed class CreateOrderRequest
    {
        public int? ProductId { get; init; }

        public string? ProductName { get; init; }

        [JsonPropertyName("product_name")]
        public string? ProductNameSnake { get; init; }

        public int Quantity { get; init; }

        public int? LineId { get; init; }
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<OrderDetailsDto>> Create([FromBody] CreateOrderRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest("quantity must be > 0");

        var productQuery = db.Products
            .Include(p => p.ProductMaterials)
            .ThenInclude(pm => pm.Material)
            .AsQueryable();

        Product? product = null;
        if (request.ProductId is not null && request.ProductId.Value > 0)
        {
            product = await productQuery.FirstOrDefaultAsync(p => p.Id == request.ProductId.Value);
        }
        else
        {
            var name = request.ProductName ?? request.ProductNameSnake;
            if (!string.IsNullOrWhiteSpace(name))
            {
                // Поиск по имени делаем в .NET из-за ограничений SQLite lower()/NOCASE для кириллицы.
                var inputName = name.Trim();
                var matchedId = await db.Products.AsNoTracking()
                    .Select(p => new { p.Id, p.Name })
                    .ToListAsync();

                var id = matchedId
                    .Where(p => string.Equals(p.Name, inputName, StringComparison.OrdinalIgnoreCase))
                    .Select(p => (int?)p.Id)
                    .FirstOrDefault();

                if (id is not null)
                    product = await productQuery.FirstOrDefaultAsync(p => p.Id == id.Value);
            }
        }

        if (product is null)
            return BadRequest("product not found (send productId or productName)");

        ProductionLine? line = null;
        if (request.LineId is not null)
        {
            line = await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == request.LineId.Value);
            if (line is null)
                return BadRequest("line_id not found");

            if (line.Status != LineStatus.Active)
                return BadRequest("line is not Active");

            if (line.CurrentWorkOrderId is not null)
                return BadRequest("line is not available");
        }

        // Materials check
        var shortages = new List<string>();
        foreach (var pm in product.ProductMaterials)
        {
            var need = pm.QuantityNeeded * request.Quantity;
            if (pm.Material.Quantity < need)
                shortages.Add($"{pm.Material.Name}: need {need} {pm.Material.UnitOfMeasure}, have {pm.Material.Quantity}");
        }

        if (shortages.Count > 0)
            return BadRequest(new { error = "insufficient_materials", details = shortages });

        var efficiency = line?.EfficiencyFactor ?? 1.0m;
        var minutes = ProductionCalculator.CalculateProductionMinutes(request.Quantity, product.ProductionTimePerUnit, efficiency);

        var start = DateTime.Now;
        var estimatedEnd = start.AddMinutes(minutes);

        var order = new WorkOrder
        {
            ProductId = product.Id,
            ProductionLineId = line?.Id,
            Quantity = request.Quantity,
            StartDate = start,
            EstimatedEndDate = estimatedEnd,
            Status = WorkOrderStatus.Pending,
            ProgressPercent = 0
        };

        db.WorkOrders.Add(order);

        // Reserve materials immediately
        foreach (var pm in product.ProductMaterials)
            pm.Material.Quantity -= pm.QuantityNeeded * request.Quantity;

        await db.SaveChangesAsync();

        var details = await BuildDetailsAsync(order.Id);
        if (details is null) return StatusCode(StatusCodes.Status500InternalServerError);

        return CreatedAtAction(nameof(GetDetails), new { id = order.Id }, details);
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<ActionResult<OrderDetailsDto>> CreateForm([FromForm] CreateOrderRequest request)
        => Create(request);

    public sealed record UpdateProgressRequest(decimal Percent);

    [HttpPut("{id:int}/progress")]
    [Consumes("application/json")]
    public async Task<ActionResult<OrderDetailsDto>> UpdateProgress(int id, [FromBody] UpdateProgressRequest request)
    {
        if (request.Percent < 0 || request.Percent > 100)
            return BadRequest("percent must be 0..100");

        var order = await db.WorkOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        if (order.Status is WorkOrderStatus.Cancelled or WorkOrderStatus.Completed)
            return BadRequest("order is finished");

        order.ProgressPercent = request.Percent;
        order.ProgressPercent = Math.Round(order.ProgressPercent, 3);
        if (request.Percent == 0 && order.Status == WorkOrderStatus.InProgress)
            order.Status = WorkOrderStatus.Pending;
        if (request.Percent > 0 && order.Status == WorkOrderStatus.Pending)
            order.Status = WorkOrderStatus.InProgress;
        if (request.Percent >= 100)
        {
            order.ProgressPercent = 100;
            order.Status = WorkOrderStatus.Completed;
            order.EstimatedEndDate = DateTime.Now;
        }

        await db.SaveChangesAsync();

        var details = await BuildDetailsAsync(id);
        return details is null ? NotFound() : details;
    }

    [HttpPut("{id:int}/progress")]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<ActionResult<OrderDetailsDto>> UpdateProgressForm(int id, [FromForm] UpdateProgressRequest request)
        => UpdateProgress(id, request);

    [HttpGet("{id:int}/details")]
    public async Task<ActionResult<OrderDetailsDto>> GetDetails(int id)
    {
        await UpdateOrderProgressAsync();
        var order = await BuildDetailsAsync(id);
        return order is null ? NotFound() : order;
    }

    private async Task<OrderDetailsDto?> BuildDetailsAsync(int id)
    {
        var order = await db.WorkOrders
            .AsNoTracking()
            .Include(o => o.Product)
            .Include(o => o.ProductionLine)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return null;

        return new OrderDetailsDto(
            order.Id,
            order.ProductId,
            order.Product.Name,
            order.Product.ProductionTimePerUnit,
            order.Product.Category,
            order.ProductionLineId,
            order.ProductionLine?.Name,
            order.Quantity,
            order.StartDate,
            order.EstimatedEndDate,
            order.Status,
            order.ProgressPercent);
    }

    public sealed record OrderDto(
        int Id,
        int ProductId,
        string ProductName,
        int? ProductionLineId,
        string? ProductionLineName,
        int Quantity,
        DateTime StartDate,
        DateTime EstimatedEndDate,
        string Status,
        decimal ProgressPercent);

    public sealed record OrderDetailsDto(
        int Id,
        int ProductId,
        string ProductName,
        int ProductionTimePerUnit,
        string? ProductCategory,
        int? ProductionLineId,
        string? ProductionLineName,
        int Quantity,
        DateTime StartDate,
        DateTime EstimatedEndDate,
        string Status,
        decimal ProgressPercent);
}
