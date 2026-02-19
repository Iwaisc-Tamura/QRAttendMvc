using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using QRAttendMvc.Services;
using System;
using System.Threading.Tasks;

namespace QRAttendMvc.Controllers
{
    public class BaseController : Controller
    {
        private readonly IActionLogService _logService;

        public BaseController(IActionLogService logService)
        {
            _logService = logService;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 3文字コード化（例）
            //string controller = context.RouteData.Values["controller"]?.ToString() ?? "";
            //string action = context.RouteData.Values["action"]?.ToString() ?? "";
            string controller =  "G10";
            string action = "A01";

            string screenId = To3(controller); // "Home" -> "HOM"
            string actionCd = To3(action);     // "Index" -> "IND" などでもOK。運用コードに合わせて変えてください

            // セッション（例：Index_home.cshtml で参照していた値）
            string? branchCd = HttpContext.Session.GetString("BRANCH_CD");
            string? employeeCd = HttpContext.Session.GetString("EMPLOYEE_CD");

            // ① 画面表示時ログ（フル項目 / 無いものはnull）
            await _logService.ActionLogSaveAsync(
                screenId: screenId,
                actionCd: actionCd,
                eventCd: null,
                employeeCd: employeeCd,   // 分かるなら入れる（無ければnull）
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
                uTantoCd: branchCd,       // 担当コードにブランチを入れる例
                uTimeStamp: DateTime.Now
            );

            await next();
        }

        private static string To3(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "   ";
            s = s.Trim().ToUpperInvariant();
            return s.Length >= 3 ? s.Substring(0, 3) : s.PadRight(3, '_');
        }
    }
}