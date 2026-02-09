using Microsoft.AspNetCore.Mvc;

namespace QRAttendMvc.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // 既定のトップページ（任意）
            return View();
        }
    }
}
