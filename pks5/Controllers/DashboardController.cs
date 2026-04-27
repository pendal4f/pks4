using Microsoft.AspNetCore.Mvc;

namespace pks5.Controllers;

public sealed class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}

