using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QRAttendMvc.Services;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;

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

            bool isCooperateSearchIndex =
            string.Equals(controller, "CooperateSearch", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase);

            bool isEmployeeSearchClick = isEmployeeSearchIndex && isSearchButton == "1";
            bool isCooperateSearchClick = isCooperateSearchIndex && isSearchButton == "1";

            bool isAttendeeSearchIndex =
            string.Equals(controller, "AttendeeSearch", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase);

            bool isAttendeeSearchClick = isAttendeeSearchIndex && isSearchButton == "1";

            bool isAttendeeExport =
            string.Equals(controller, "AttendeeSearch", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action, "Export", StringComparison.OrdinalIgnoreCase);

            try
            {
                if (isEmployeeSearchClick)
                {
                    var sCooperateKana = context.HttpContext.Request.Query["companyKana"].ToString();
                    var sEmployeeKana = context.HttpContext.Request.Query["workerNameKana"].ToString();
                    var sEmployeeKanji = context.HttpContext.Request.Query["workerName"].ToString();
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

                    var sSelect = context.HttpContext.Request.Query.ContainsKey("includeExpired") ? "S21" : "S22";
                    var sEmployeeCd = context.HttpContext.Request.Query["employeeCd"].ToString();

                    var currentKaisaiCd = await TryGetEventCdAsync(context.HttpContext).ConfigureAwait(false);

                    var eventCdForLog = NormalizeEventCdByScreen("G41", currentKaisaiCd);

                    await _logService.ActionLogSaveAsync(
                        screenId: "G41",
                        actionCd: "A03",
                        eventCd: eventCdForLog,
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
                        sEmployeeKana: string.IsNullOrWhiteSpace(sEmployeeKana) ? null : sEmployeeKana.Trim(),
                        sEmployeeKanji: sEmployeeKanji,
                        sBirthYmd: sBirthYmd,
                        sEmployeeCd: string.IsNullOrWhiteSpace(sEmployeeCd) ? null : sEmployeeCd.Trim(),
                        sSelect: sSelect,
                        tResult: null,
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: DateTime.Now
                    );

                }
                else if (isCooperateSearchClick)
                {
                    var sCooperateKana = context.HttpContext.Request.Query["companyKana"].ToString();
                    var currentKaisaiCd = await TryGetEventCdAsync(context.HttpContext).ConfigureAwait(false);
                    var eventCdForLog = NormalizeEventCdByScreen("G42", currentKaisaiCd);


                    await _logService.ActionLogSaveAsync(
                        screenId: "G42",
                        actionCd: "A03",
                        eventCd: eventCdForLog,
                        // 仕様②：S_COOPERATE に会社名カナ
                        sCooperateKana: string.IsNullOrWhiteSpace(sCooperateKana) ? null : sCooperateKana.Trim(),
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: DateTime.Now
                    );
                }
                else if (isAttendeeSearchClick)
                {
                    var sCooperateKana = context.HttpContext.Request.Query["companyKana"].ToString();
                    var sCooperateName = context.HttpContext.Request.Query["companyName"].ToString();
                    var sEmployeeKana = context.HttpContext.Request.Query["workerKana"].ToString();
                    var sEmployeeKanji = context.HttpContext.Request.Query["workerName"].ToString();
                    var birthDate = context.HttpContext.Request.Query["birthDate"].ToString();
                    var sEmployeeCd = context.HttpContext.Request.Query["workerId"].ToString();

                    // birth yyyyMMdd
                    string? sBirthYmd = null;
                    if (!string.IsNullOrWhiteSpace(birthDate))
                    {
                        if (DateTime.TryParse(birthDate, CultureInfo.GetCultureInfo("ja-JP"),
                                              DateTimeStyles.None, out var dt))
                            sBirthYmd = dt.ToString("yyyyMMdd");
                        else
                        {
                            var digits = Regex.Replace(birthDate, @"\D", "");
                            if (Regex.IsMatch(digits, @"^\d{8}$")) sBirthYmd = digits;
                        }
                    }

                    // S_SELECT 生成（filter × includeSecond）
                    var filter = context.HttpContext.Request.Query["filter"].ToString();
                    if (string.IsNullOrWhiteSpace(filter))
                        filter = context.HttpContext.Request.Query["filterCondition"].ToString();
                    if (string.IsNullOrWhiteSpace(filter)) filter = "1";

                    var includeSecondValues = context.HttpContext.Request.Query["includeSecond"];
                    bool includeSecondChecked =
                        includeSecondValues.Any(v =>
                            string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(v, "on", StringComparison.OrdinalIgnoreCase));
                    string sSelect = MapAttendeeSelect(filter, includeSecondChecked);

                    var currentKaisaiCd = await TryGetEventCdAsync(context.HttpContext).ConfigureAwait(false);
                    var eventCdForLog = NormalizeEventCdByScreen("G70", currentKaisaiCd);


                    await _logService.ActionLogSaveAsync(
                        screenId: "G70",
                        actionCd: "A03",
                        eventCd: eventCdForLog,
                        sCooperateKana: string.IsNullOrWhiteSpace(sCooperateKana) ? null : sCooperateKana.Trim(),
                        sCooperateName: string.IsNullOrWhiteSpace(sCooperateName) ? null : sCooperateName.Trim(),
                        sEmployeeKana: string.IsNullOrWhiteSpace(sEmployeeKana) ? null : sEmployeeKana.Trim(),
                        sEmployeeKanji: string.IsNullOrWhiteSpace(sEmployeeKanji) ? null : sEmployeeKanji.Trim(),
                        sBirthYmd: sBirthYmd,
                        sEmployeeCd: string.IsNullOrWhiteSpace(sEmployeeCd) ? null : sEmployeeCd.Trim(),
                        sSelect: sSelect,
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: DateTime.Now
                    );
                }
                else if (isAttendeeExport)
                {
                    var currentKaisaiCd = await TryGetEventCdAsync(context.HttpContext).ConfigureAwait(false);
                    var eventCdForLog = NormalizeEventCdByScreen("G70", currentKaisaiCd);


                    await _logService.ActionLogSaveAsync(
                        screenId: "G70",
                        actionCd: "A05",
                        eventCd: eventCdForLog,
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
                        var currentKaisaiCd = await TryGetEventCdAsync(context.HttpContext).ConfigureAwait(false);
                        var eventCdForLog = NormalizeEventCdByScreen(screenId, currentKaisaiCd);

                        await _logService.ActionLogSaveAsync(
                            screenId: screenId,
                            actionCd: "A01",
                          　eventCd: eventCdForLog,
                            uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                            uTimeStamp: DateTime.Now
                        ).ConfigureAwait(false); ;
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

        private static string? NormalizeEventCdByScreen(string screenId, string? eventCd)
        {
            // ★要件：G10 / G20 は必ず NULL
            if (string.Equals(screenId, "G10", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(screenId, "G20", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // その他は従来どおり（空はNULL、あればTrim）
            return string.IsNullOrWhiteSpace(eventCd) ? null : eventCd.Trim();
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

        private static string MapAttendeeSelect(string filter, bool includeSecondChecked)
        {
            // filter は "1"～"4" 想定、違うのが来たら "1"
            if (filter != "1" && filter != "2" && filter != "3" && filter != "4")
                filter = "1";

            if (includeSecondChecked)
            {
                return filter switch
                {
                    "1" => "S01",
                    "2" => "SD2",
                    "3" => "S03",
                    "4" => "S04",
                    _ => "S01"
                };
            }
            else
            {
                return filter switch
                {
                    "1" => "S11",
                    "2" => "S12",
                    "3" => "S13",
                    "4" => "S14",
                    _ => "S11"
                };
            }
        }
    }
}