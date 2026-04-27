using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;
using pks5.Services;

namespace pks5.Controllers.Api;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> Get(
        [FromQuery(Name = "status")] string? status = null,
        [FromQuery(Name = "date")] string? date = null)
    {
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

    public sealed record CreateOrderRequest(int ProductId, int Quantity, int? LineId);

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest("quantity must be > 0");

        var product = await db.Products
            .Include(p => p.ProductMaterials)
            .ThenInclude(pm => pm.Material)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId);

        if (product is null)
            return BadRequest("product_id not found");

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

        return CreatedAtAction(nameof(GetDetails), new { id = order.Id }, await BuildDetailsAsync(order.Id));
    }

    public sealed record UpdateProgressRequest(decimal Percent);

    [HttpPut("{id:int}/progress")]
    public async Task<ActionResult<OrderDto>> UpdateProgress(int id, [FromBody] UpdateProgressRequest request)
    {
        if (request.Percent < 0 || request.Percent > 100)
            return BadRequest("percent must be 0..100");

        var order = await db.WorkOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        if (order.Status is WorkOrderStatus.Cancelled or WorkOrderStatus.Completed)
            return BadRequest("order is finished");

        order.ProgressPercent = request.Percent;
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

        return await BuildDetailsAsync(id);
    }

    [HttpGet("{id:int}/details")]
    public async Task<ActionResult<OrderDetailsDto>> GetDetails(int id)
    {
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

