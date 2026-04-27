using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks4.Data;

namespace pks4.Controllers;

public sealed class AttractionsController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var attraction = await db.Attractions
            .AsNoTracking()
            .Include(a => a.City)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attraction is null)
            return NotFound();

        return View(attraction);
    }
}
