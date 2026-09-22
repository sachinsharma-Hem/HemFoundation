using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("account")]
    public class AccountController : Controller
    {
        [HttpGet("register")]
        public IActionResult Register()
        {
            return View();
        }
    }
}