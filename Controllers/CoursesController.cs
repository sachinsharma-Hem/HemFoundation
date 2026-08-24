using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class CoursesController : Controller
    {
        [HttpGet("Courses/Index")]
        public IActionResult Index()
        {
            return View("~/Views/Courses/Index.cshtml");
        }
    }
}