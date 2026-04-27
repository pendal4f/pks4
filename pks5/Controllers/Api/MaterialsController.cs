using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;

namespace pks5.Controllers.Api;

[ApiController]
[Route("api/materials")]
public sealed class MaterialsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MaterialDto>>> Get([FromQuery(Name = "low_stock")] bool lowStock = false)
    {
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
    public async Task<ActionResult<MaterialDto>> Create([FromBody] CreateMaterialRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("name is required");

        if (string.IsNullOrWhiteSpace(request.Unit))
            return BadRequest("unit is required");

        if (request.Quantity < 0 || request.MinStock < 0)
            return BadRequest("quantity/min_stock must be >= 0");

        var material = new Material
        {
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            UnitOfMeasure = request.Unit.Trim(),
            MinimalStock = request.MinStock
        };

        db.Materials.Add(material);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = material.Id },
            new MaterialDto(material.Id, material.Name, material.Quantity, material.UnitOfMeasure, material.MinimalStock));
    }

    public sealed record UpdateStockRequest(decimal Amount);

    [HttpPut("{id:int}/stock")]
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

    public sealed record MaterialDto(int Id, string Name, decimal Quantity, string Unit, decimal MinimalStock);
}

