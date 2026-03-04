using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QRAttendMvc.Services;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;

namespace QRAttendMvc.Controllers
{
    public class BaseController : Controller
    {
        protected readonly IActionLogService _logService;

        public BaseController(IActionLogService logService)
        {
            _logService = logService;
        }

        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            string controller = context.RouteData.Values["controller"]?.ToString() ?? "";
            string action = context.RouteData.Values["action"]?.ToString() ?? "";

            string kind = await TryGetKindAsync(context.HttpContext).ConfigureAwait(false);

            string screenId = ResolveScreenId(controller, action, kind);

            bool isEmployeeSearchIndex =
                string.Equals(controller, "EmployeeSearch", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase);

            var isSearchButton = context.HttpContext.Request.Query["isSearchButton"].ToString();
            bool isSearchClick = isEmployeeSearchIndex && isSearchButton == "1";

            try
            {
                if (isSearchClick)
                {
                    var sCooperateKana = context.HttpContext.Request.Query["companyKana"].ToString();
                    var sEmployeeKanas = context.HttpContext.Request.Query["workerKanaLast"].ToString();
                    var sEmployeeKanan = context.HttpContext.Request.Query["workerKanaFirst"].ToString();
                    var birthDate = context.HttpContext.Request.Query["birthDate"].ToString();

                    string? sBirthYmd = null;
                    if (!string.IsNullOrWhiteSpace(birthDate))
                    {
                        if (DateTime.TryParse(birthDate, CultureInfo.GetCultureInfo("ja-JP"),
                                              DateTimeStyles.None, out var dt))
                        {
                            sBirthYmd = dt.ToString("yyyyMMdd");
                        }
                        else
                        {
                            var digits = Regex.Replace(birthDate, @"\D", "");
                            if (Regex.IsMatch(digits, @"^\d{8}$"))
                                sBirthYmd = digits;
                        }
                    }

                    var sSelect = context.HttpContext.Request.Query.ContainsKey("includeExpired") ? "1" : "0";
                    var sEmployeeCd = context.HttpContext.Request.Query["employeeCd"].ToString();

                    // ★開催コード（KaisaiCd）を取る
                    var currentKaisaiCd = await TryGetEventCdAsync(context.HttpContext).ConfigureAwait(false);

                    await _logService.ActionLogSaveAsync(
                        screenId: "G41",
                        actionCd: "A03",
                        eventCd: currentKaisaiCd,
                        employeeCd: null,
                        cooperateCd: null,
                        familyName: null,
                        firstName: null,
                        birthYmd: null,
                        entryTime: null,
                        exitTime: null,
                        reasonCd: null,
                        sCooperateKana: sCooperateKana,
                        sCooperateName: null,
                        sEmployeeKana: $"{sEmployeeKanas} {sEmployeeKanan}".Trim(),
                        sEmployeeKanji: null,
                        sBirthYmd: sBirthYmd,
                        sEmployeeCd: string.IsNullOrWhiteSpace(sEmployeeCd) ? null : sEmployeeCd.Trim(),
                        sSelect: sSelect,
                        tResult: null,
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: DateTime.Now
                    );

                }
                else
                {
                    bool isEventSelectionIndex =
                        string.Equals(controller, "EventSelection", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase);

                    if (!isEventSelectionIndex)
                    {
                        await _logService.ActionLogSaveAsync(
                            screenId: screenId,
                            actionCd: "A01",
                            uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                            uTimeStamp: DateTime.Now
                        ).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                // ログ失敗で画面処理を止めない
            }

            await next().ConfigureAwait(false);
        }

        private static async Task<string> TryGetKindAsync(HttpContext http)
        {
            try
            {
                var kind = http.Request.Query["kind"].ToString();
                if (!string.IsNullOrWhiteSpace(kind))
                    return kind.Trim().ToUpperInvariant();

                if (HttpMethods.IsPost(http.Request.Method) && http.Request.HasFormContentType)
                {
                    var form = await http.Request.ReadFormAsync().ConfigureAwait(false);
                    kind = form["kind"].ToString();
                    if (!string.IsNullOrWhiteSpace(kind))
                        return kind.Trim().ToUpperInvariant();
                }

                kind = http.Session?.GetString("KIND");
                if (!string.IsNullOrWhiteSpace(kind))
                    return kind.Trim().ToUpperInvariant();

                return "";
            }
            catch
            {
                return "";
            }
        }

        private static string ResolveScreenId(string controller, string action, string kind)
        {
            bool isOut = string.Equals(kind, "OUT", StringComparison.OrdinalIgnoreCase);

            if (controller == "Home") return "G10";
            if (controller == "EventSelection") return "G20";
            if (controller == "EmployeeSearch") return "G41";
            if (controller == "CooperateSearch") return "G42";
            if (controller == "AttendeeSearch") return "G70";

            return "UNK";
        }

        /// <summary>
        /// 開催コード（KaisaiCd）を取る
        /// - Query/Form に eventCd があればそれ（画面によっては使う）
        /// - Session は CurrentKaisaiCd を優先（EventSelectionで保持しているため）
        /// - 互換で "KAISAI_CD" も見る
        /// </summary>
        private static async Task<string?> TryGetEventCdAsync(HttpContext http)
        {
            try
            {
                // 1) Query（GET）
                var v = http.Request.Query["eventCd"].ToString();
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();

                // 2) Form（POST）
                if (HttpMethods.IsPost(http.Request.Method) && http.Request.HasFormContentType)
                {
                    var form = await http.Request.ReadFormAsync().ConfigureAwait(false);
                    v = form["eventCd"].ToString();
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }

                // 3) Session：CurrentKaisaiCd（推奨）
                v = http.Session?.GetString("CurrentKaisaiCd");
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();

                // 4) 互換：古いキーも念のため
                v = http.Session?.GetString("KAISAI_CD");
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}