using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks5.Data;
using pks5.Services;

namespace pks5.Controllers.Api;

[ApiController]
[Route("api/calculate")]
public sealed class CalculateController(AppDbContext db) : ControllerBase
{
    public sealed record ProductionRequest(int ProductId, int Quantity);
    public sealed record ProductionResponse(int Minutes);

    [HttpPost("production")]
    [Consumes("application/json")]
    public async Task<ActionResult<ProductionResponse>> Production([FromBody] ProductionRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest("quantity must be > 0");

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId);
        if (product is null) return BadRequest("product_id not found");

        var minutes = ProductionCalculator.CalculateProductionMinutes(request.Quantity, product.ProductionTimePerUnit, 1.0m);
        return new ProductionResponse(minutes);
    }

    [HttpPost("production")]
    [Consumes("application/x-www-form-urlencoded")]
    public Task<ActionResult<ProductionResponse>> ProductionForm([FromForm] ProductionRequest request)
        => Production(request);
}
