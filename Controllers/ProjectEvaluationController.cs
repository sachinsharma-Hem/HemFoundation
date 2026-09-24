using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class ProjectEvaluationController : Controller
    {
        [Route("ProjectEvaluation/index")]
        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public IActionResult GetAnalyticsPartial()
        {
            return PartialView("_EvaluationAnalyticsPartial");
        }
    }

}