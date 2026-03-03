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
            // 1) ルート情報（Controller/Action）
            string controller = context.RouteData.Values["controller"]?.ToString() ?? "";
            string action = context.RouteData.Values["action"]?.ToString() ?? "";

            // 2) kind を安全に取得（Scan系のみで使う想定）
            //    GET: QueryString (?kind=IN)
            //    POST(Form): formフィールド kind
            //    その他: Session("KIND")
            string kind = await TryGetKindAsync(context.HttpContext).ConfigureAwait(false);

            // 3) screenId 判定（指定条件）
            string screenId = ResolveScreenId(controller, action, kind);

            // ★検索ボタン押下判定（EmployeeSearch/Index のときだけ）
            bool isEmployeeSearchIndex =
                string.Equals(controller, "EmployeeSearch", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase);

            // isSearchButton=1 のときだけ「検索ボタン押下」とみなす
            var isSearchButton = context.HttpContext.Request.Query["isSearchButton"].ToString();
            bool isSearchClick = isEmployeeSearchIndex && isSearchButton == "1";

            try
            {
                if (isSearchClick)
                {
                    // ===== A03（検索）ログ：A01は出さない =====

                    // 検索条件を QueryString から取得
                    var sCooperateKana = context.HttpContext.Request.Query["companyKana"].ToString();
                    var sEmployeeKanas = context.HttpContext.Request.Query["workerKanaLast"].ToString();
                    var sEmployeeKanan = context.HttpContext.Request.Query["workerKanaFirst"].ToString();
                    var birthDate = context.HttpContext.Request.Query["birthDate"].ToString();

                    string? sBirthYmd = null;
                    if (!string.IsNullOrWhiteSpace(birthDate))
                    {
                        // まず DateTime として解釈（2025/2/6 でもOK）
                        if (DateTime.TryParse(birthDate, CultureInfo.GetCultureInfo("ja-JP"),
                                              DateTimeStyles.None, out var dt))
                        {
                            sBirthYmd = dt.ToString("yyyyMMdd");
                        }
                        else
                        {
                            // 最後の保険：数字だけ抜き出して8桁ならOK
                            var digits = Regex.Replace(birthDate, @"\D", "");
                            if (Regex.IsMatch(digits, @"^\d{8}$"))
                                sBirthYmd = digits;
                        }
                    }

                    var sSelect = context.HttpContext.Request.Query.ContainsKey("includeExpired") ? "1" : "0";

                    var currentKaisaiCd = context.HttpContext.Session.GetString("KAISAI_CD");

                    var sEmployeeCd = context.HttpContext.Request.Query["employeeCd"].ToString();

                    currentKaisaiCd = await TryGetEventCdAsync(context.HttpContext).ConfigureAwait(false);

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
                        sEmployeeKanas: sEmployeeKanas,
                        sEmployeeKanan: sEmployeeKanan,
                        sEmployeeKanjis: null,
                        sEmployeeKanjin: null,
                        sBirthYmd: sBirthYmd,
                        sEmployeeCd: string.IsNullOrWhiteSpace(sEmployeeCd) ? null : sEmployeeCd.Trim(),
                        sSelect: sSelect,

                        jStrat: null,
                        jMaisu: null,
                        tResart: null,

                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: DateTime.Now
                    ).ConfigureAwait(false);
                }
                else
                {
                    // ===== 従来通り A01（画面アクセス）ログ =====
                    await _logService.ActionLogSaveAsync(
                        screenId: screenId,
                        actionCd: "A01",
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: DateTime.Now
                    ).ConfigureAwait(false);
                }
            }
            catch
            {
                // ログ失敗で画面処理を止めない（ログ処理の鉄則）
            }

            // 6) 本来のアクションへ
            await next().ConfigureAwait(false);
        }

        /// <summary>
        /// kind を「落ちない」形で取得する（IN/OUT）。
        /// - Query: ?kind=IN
        /// - Form: kind=IN（application/x-www-form-urlencoded / multipart/form-data）
        /// - Session: "KIND"
        /// 取れない場合は "" を返す（呼び出し側で既定値扱い）
        /// </summary>
        private static async Task<string> TryGetKindAsync(HttpContext http)
        {
            try
            {
                // QueryString（GETでも安全）
                var kind = http.Request.Query["kind"].ToString();
                if (!string.IsNullOrWhiteSpace(kind))
                    return kind.Trim().ToUpperInvariant();

                // Form（Content-Type がフォームの時だけ読む）
                if (HttpMethods.IsPost(http.Request.Method) && http.Request.HasFormContentType)
                {
                    var form = await http.Request.ReadFormAsync().ConfigureAwait(false);
                    kind = form["kind"].ToString();
                    if (!string.IsNullOrWhiteSpace(kind))
                        return kind.Trim().ToUpperInvariant();
                }

                // Session（存在しない場合は null -> ""）
                kind = http.Session?.GetString("KIND");
                if (!string.IsNullOrWhiteSpace(kind))
                    return kind.Trim().ToUpperInvariant();

                return "";
            }
            catch
            {
                // 何が起きてもここでは落とさない
                return "";
            }
        }

        /// <summary>
        /// 指定ルールで screenId を決める
        /// Scanは action と kind（IN/OUT）で分ける
        /// </summary>
        private static string ResolveScreenId(string controller, string action, string kind)
        {
            // kind は OUT 以外は IN 扱い（未指定でも IN）
            bool isOut = string.Equals(kind, "OUT", StringComparison.OrdinalIgnoreCase);

            // controller単位
            if (controller == "Home") return "G10";
            if (controller == "EventSelection") return "G20";
            if (controller == "EmployeeSearch") return "G41";
            if (controller == "CooperateSearch") return "G42";
            if (controller == "AttendeeSearch") return "G70";

            //// Scan + action単位（タイトル別（入/退）で分離）
            //if (controller == "Scan" && action == "Batch")
            //{
            //    // 入場: G30 / 退場: G50
            //    return isOut ? "G50" : "G30";
            //}

            //if (controller == "Scan" && action == "TempInput")
            //{
            //    // 入場: G40 / 退場: G60
            //    return isOut ? "G60" : "G40";
            //}

            return "UNK";
        }

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

                // 3) Session
                v = http.Session?.GetString("KAISAI_CD");
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();

                return null;
            }
            catch { return null; }
        }

    }
}