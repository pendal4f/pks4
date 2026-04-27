using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pks4.Data;
using pks4.Models;
using pks4.Utilities;

namespace pks4.Controllers;

public sealed class CitiesController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        var cities = await db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ViewBag.Query = q;

        if (string.IsNullOrWhiteSpace(q))
            return View(cities);

        var qNorm = SearchTokens.Normalize(q);
        var filtered = cities
            .Where(c => SearchTokens.MatchesQuery(SearchTokens.CitySearchText(c), qNorm))
            .ToList();

        return View(filtered);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var city = await db.Cities
            .AsNoTracking()
            .Include(c => c.Attractions.OrderBy(a => a.Name))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (city is null)
            return NotFound();

        return View(city);
    }
}
