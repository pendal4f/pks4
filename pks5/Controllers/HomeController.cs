using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace pks5.Controllers;

public class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
