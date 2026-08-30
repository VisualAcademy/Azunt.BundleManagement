using Microsoft.AspNetCore.Mvc;

namespace Azunt.Web.Areas.DotNetNote.Controllers;

[Area("DotNetNote")]
public sealed class BundlesController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
