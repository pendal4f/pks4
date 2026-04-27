using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;

namespace pks5.Controllers.Api;

[ApiController]
[Route("api/lines")]
public sealed class LinesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LineDto>>> Get([FromQuery(Name = "available")] bool available = false)
    {
        IQueryable<ProductionLine> query = db.ProductionLines.AsNoTracking();

        if (available)
            query = query.Where(l => l.Status == LineStatus.Active && l.CurrentWorkOrderId == null);

        var result = await query
            .OrderBy(l => l.Name)
            .Select(l => new LineDto(l.Id, l.Name, l.Status, l.EfficiencyFactor, l.CurrentWorkOrderId))
            .ToListAsync();

        return result;
    }

    public sealed record UpdateStatusRequest(string Status);

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<LineDto>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var line = await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == id);
        if (line is null) return NotFound();

        var status = request.Status?.Trim();
        if (status is not (LineStatus.Active or LineStatus.Stopped))
            return BadRequest("status must be 'Active' or 'Stopped'");

        line.Status = status;
        await db.SaveChangesAsync();

        return new LineDto(line.Id, line.Name, line.Status, line.EfficiencyFactor, line.CurrentWorkOrderId);
    }

    [HttpGet("{id:int}/schedule")]
    public async Task<ActionResult<List<LineScheduleDto>>> GetSchedule(int id)
    {
        var exists = await db.ProductionLines.AsNoTracking().AnyAsync(l => l.Id == id);
        if (!exists) return NotFound();

        var result = await db.WorkOrders
            .AsNoTracking()
            .Where(w => w.ProductionLineId == id && w.Status != WorkOrderStatus.Cancelled)
            .Include(w => w.Product)
            .OrderBy(w => w.StartDate)
            .Select(w => new LineScheduleDto(w.Id, w.Product.Name, w.Quantity, w.StartDate, w.EstimatedEndDate, w.Status, w.ProgressPercent))
            .ToListAsync();

        return result;
    }

    public sealed record LineDto(int Id, string Name, string Status, decimal EfficiencyFactor, int? CurrentWorkOrderId);
    public sealed record LineScheduleDto(int Id, string ProductName, int Quantity, DateTime StartDate, DateTime EstimatedEndDate, string Status, decimal ProgressPercent);
}

