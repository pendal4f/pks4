using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Models;
using System.Text.Json.Serialization;

namespace pks5.Controllers.Api;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> Get([FromQuery(Name = "category")] string? category = null)
    {
        IQueryable<Product> query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        var result = await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Name, p.ProductionTimePerUnit, p.Category))
            .ToListAsync();

        return result;
    }

    [HttpGet("{id:int}/materials")]
    public async Task<ActionResult<List<ProductMaterialDto>>> GetMaterials(int id)
    {
        var exists = await db.Products.AsNoTracking().AnyAsync(p => p.Id == id);
        if (!exists) return NotFound();

        var result = await db.ProductMaterials
            .AsNoTracking()
            .Where(pm => pm.ProductId == id)
            .Include(pm => pm.Material)
            .OrderBy(pm => pm.Material.Name)
            .Select(pm => new ProductMaterialDto(pm.MaterialId, pm.Material.Name, pm.QuantityNeeded, pm.Material.UnitOfMeasure))
            .ToListAsync();

        return result;
    }

    public sealed class CreateProductRequest
    {
        public string? Name { get; init; }

        // Основное поле по заданию / API
        public int? ProdTime { get; init; }

        // Часто отправляют из UI/других эндпоинтов
        public int? ProductionTimePerUnit { get; init; }

        // Алиасы (например, если кто-то отправляет snake_case)
        [JsonPropertyName("prod_time")]
        public int? ProdTimeSnake { get; init; }

        [JsonPropertyName("production_time_per_unit")]
        public int? ProductionTimePerUnitSnake { get; init; }

        public string? Category { get; init; }

        public int? MinimalStock { get; init; }

        public string? Description { get; init; }

        public string? SpecificationsJson { get; init; }
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("name is required");

        var prodTime = request.ProdTime
                       ?? request.ProductionTimePerUnit
                       ?? request.ProdTimeSnake
                       ?? request.ProductionTimePerUnitSnake
                       ?? 0;

        if (prodTime <= 0)
            return BadRequest("prod_time must be > 0 (send ProdTime or productionTimePerUnit)");

        var product = new Product
        {
            Name = request.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            ProductionTimePerUnit = prodTime,
            MinimalStock = Math.Max(0, request.MinimalStock ?? 0),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SpecificationsJson = string.IsNullOrWhiteSpace(request.SpecificationsJson) ? null : request.SpecificationsJson.Trim()
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = product.Id },
            new ProductDto(product.Id, product.Name, product.ProductionTimePerUnit, product.Category));
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<ActionResult<ProductDto>> CreateForm([FromForm] CreateProductRequest request)
        => Create(request);

    public sealed record ProductDto(int Id, string Name, int ProductionTimePerUnit, string? Category);
    public sealed record ProductMaterialDto(int MaterialId, string MaterialName, decimal QuantityNeeded, string Unit);
}
