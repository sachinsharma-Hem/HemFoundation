using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class CourseHubController : Controller
    {
        [HttpGet("CourseHub/Index")]
        public IActionResult Index()
        {
            return View("~/Views/CourseHub/Index.cshtml");
        }
    }
}