using Microsoft.AspNetCore.Mvc;

/*　追加 2026.02.19 Takada strat*/
using QRAttendMvc.Services;
using System;
using System.Threading.Tasks;
/*　追加 2026.02.19 Takada end  */


namespace QRAttendMvc.Controllers
{
    public class HomeController : BaseController
    {
        private readonly IActionLogService _logService;

        public HomeController(IActionLogService logService) : base(logService)
        {
            _logService = logService;
        }

        public IActionResult Index()
        {
            return View(); // Views/Home/Index.cshtml
        }

        // ② 「イベント選択」押下ログ（フル項目、無いものはnull）
        public async Task<IActionResult> GoEventSelection()
        {
            await _logService.ActionLogSaveAsync(
                screenId: "G10",
                actionCd: "A02",
                eventCd: null,
                employeeCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                cooperateCd: null,
                familyName: null,
                firstName: null,
                birthYmd: null,
                entryTime: null,
                exitTime: null,
                reasonCd: null,
                sCooperateKana: null,
                sCooperateName: null,
                sEmployeeKanas: null,
                sEmployeeKanan: null,
                sEmployeeKanjis: null,
                sEmployeeKanjin: null,
                sBirthYmd: null,
                sEmployeeCd: null,
                sSelect: null,
                jStrat: null,
                jMaisu: null,
                tResart: null,
                uTantoCd: HttpContext.Session.GetString("BRANCH_CD"),
                uTimeStamp: DateTime.Now
            );

            return RedirectToAction("Index", "EventSelection");
        }
    }
}
