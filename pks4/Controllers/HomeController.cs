using Microsoft.AspNetCore.Mvc;

namespace pks4.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet]
    public IActionResult Error() => View();
}
