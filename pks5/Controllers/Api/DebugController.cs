using Microsoft.AspNetCore.Mvc;

namespace pks5.Controllers.Api;

[ApiController]
[Route("api/debug")]
public sealed class DebugController : ControllerBase
{
#if DEBUG
    [HttpGet("throw")]
    public IActionResult Throw()
    {
        throw new InvalidOperationException("Debug endpoint: forced 500 error");
    }
#else
    [HttpGet("throw")]
    public IActionResult ThrowProd() => NotFound();
#endif
}

