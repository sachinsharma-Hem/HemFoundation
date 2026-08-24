using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class CourseSetupController : Controller
    {
        [HttpGet("CourseSetup/Index")]
        public IActionResult Index()
        {
            return View("~/Views/CourseSetup/Index.cshtml");
        }
    }
}