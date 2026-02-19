using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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

            // 4) actionCd は固定
            const string actionCd = "A01";

            // 5) ログ保存（必要なものだけ埋める：他は既定引数で null）
            //    セッション値が無い場合は null のまま入ります。
            try
            {
                await _logService.ActionLogSaveAsync(
                    screenId: screenId,
                    actionCd: actionCd,
                    employeeCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTantoCd: HttpContext.Session.GetString("BRANCH_CD"),
                    uTimeStamp: DateTime.Now
                ).ConfigureAwait(false);
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

            // Scan + action単位（タイトル別（入/退）で分離）
            if (controller == "Scan" && action == "Batch")
            {
                // 入場: G30 / 退場: G50
                return isOut ? "G50" : "G30";
            }

            if (controller == "Scan" && action == "TempInput")
            {
                // 入場: G40 / 退場: G60
                return isOut ? "G60" : "G40";
            }

            return "UNK";
        }
    }
}