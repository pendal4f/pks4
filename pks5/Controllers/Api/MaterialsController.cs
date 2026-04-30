using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;
using pks5.Services;

namespace pks5.Controllers.Api;

[ApiController]
[Route("api/materials")]
public sealed class MaterialsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MaterialDto>>> Get([FromQuery(Name = "low_stock")] bool lowStock = false)
    {
        await MaterialDeduplicator.MergeDuplicatesAsync(db);

        IQueryable<Material> query = db.Materials.AsNoTracking();

        if (lowStock)
            query = query.Where(m => m.Quantity <= m.MinimalStock);

        var result = await query
            .OrderBy(m => m.Name)
            .Select(m => new MaterialDto(m.Id, m.Name, m.Quantity, m.UnitOfMeasure, m.MinimalStock))
            .ToListAsync();

        return result;
    }

    public sealed record CreateMaterialRequest(string Name, decimal Quantity, string Unit, decimal MinStock);

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<MaterialDto>> Create([FromBody] CreateMaterialRequest request)
    {
        await MaterialDeduplicator.MergeDuplicatesAsync(db);

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("name is required");

        if (string.IsNullOrWhiteSpace(request.Unit))
            return BadRequest("unit is required");

        if (request.Quantity < 0 || request.MinStock < 0)
            return BadRequest("quantity/min_stock must be >= 0");

        var normalizedName = request.Name.Trim();
        var normalizedUnit = request.Unit.Trim();

        var existing = await db.Materials.FirstOrDefaultAsync(m => m.Name.ToLower() == normalizedName.ToLower());
        if (existing is not null)
        {
            if (!string.Equals(existing.UnitOfMeasure, normalizedUnit, StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "unit_mismatch", existing = existing.UnitOfMeasure, provided = normalizedUnit });

            existing.Quantity += request.Quantity;
            existing.MinimalStock = Math.Max(existing.MinimalStock, request.MinStock);
            await db.SaveChangesAsync();

            await MaterialDeduplicator.MergeDuplicatesAsync(db);
            return new MaterialDto(existing.Id, existing.Name, existing.Quantity, existing.UnitOfMeasure, existing.MinimalStock);
        }

        var material = new Material
        {
            Name = normalizedName,
            Quantity = request.Quantity,
            UnitOfMeasure = normalizedUnit,
            MinimalStock = request.MinStock
        };

        db.Materials.Add(material);
        await db.SaveChangesAsync();

        await MaterialDeduplicator.MergeDuplicatesAsync(db);
        return CreatedAtAction(nameof(Get), new { id = material.Id },
            new MaterialDto(material.Id, material.Name, material.Quantity, material.UnitOfMeasure, material.MinimalStock));
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<ActionResult<MaterialDto>> CreateForm([FromForm] CreateMaterialFormRequest request)
    {
        var unit = string.IsNullOrWhiteSpace(request.Unit) ? request.UnitOfMeasure : request.Unit;
        unit = unit?.Trim() ?? string.Empty;

        var minStock = request.MinStock ?? request.MinimalStock ?? 0;

        return Create(new CreateMaterialRequest(
            request.Name,
            request.Quantity,
            unit,
            minStock));
    }

    public sealed class CreateMaterialFormRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public string? UnitOfMeasure { get; set; }
        public decimal? MinStock { get; set; }
        public decimal? MinimalStock { get; set; }
    }

    public sealed record UpdateStockRequest(decimal Amount);

    [HttpPut("{id:int}/stock")]
    [Consumes("application/json")]
    public async Task<ActionResult<MaterialDto>> UpdateStock(int id, [FromBody] UpdateStockRequest request)
    {
        var material = await db.Materials.FirstOrDefaultAsync(m => m.Id == id);
        if (material is null)
            return NotFound();

        var next = material.Quantity + request.Amount;
        if (next < 0)
            return BadRequest("resulting quantity must be >= 0");

        material.Quantity = next;
        await db.SaveChangesAsync();

        return new MaterialDto(material.Id, material.Name, material.Quantity, material.UnitOfMeasure, material.MinimalStock);
    }

    [HttpPut("{id:int}/stock")]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<ActionResult<MaterialDto>> UpdateStockForm(int id, [FromForm] UpdateStockRequest request)
        => UpdateStock(id, request);

    public sealed record MaterialDto(int Id, string Name, decimal Quantity, string Unit, decimal MinimalStock);
}
