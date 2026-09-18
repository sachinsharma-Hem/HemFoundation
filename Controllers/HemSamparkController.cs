using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class HemSamparkController : Controller
    {
        [HttpGet("HemSampark/Admin")]
        public IActionResult Admin()
        {
            return View(); 
        }

        [HttpGet("HemSampark/User")]
        public IActionResult UserPortal()
        {
            return View(); 
        }
    }
}