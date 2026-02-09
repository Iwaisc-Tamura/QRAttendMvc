
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using QRAttendMvc.Models;

namespace QRAttendMvc.Controllers
{
    public class ScanController : Controller
    {
        private readonly AppDbContext _db;
        // EventSelectionで確定した開催コード（KAISAI_CD）
        private const string SessionKeyCurrentKaisaiCd = "CurrentKaisaiCd";

        public ScanController(AppDbContext db)
        {
            _db = db;
        }


// 作業員ID（GM01_EMPLOYEE.EMPLOYEE_CD）は 10桁固定（数字のみ）
private static bool IsEmployeeCode(string code)
    => Regex.IsMatch(code ?? "", @"^\d{10}$");

private async Task WriteOperationLogAsync(string screenId, string actionCd, string? kaisaiCd, string? workerCd, Gm01Employee? emp, string? entryTime, string? exitTime, string tResart)
{
    try
    {
        var opCd = HttpContext.Session.GetString("EMPLOYEE_CD");
        var log = new Tx01Log
        {
            ScreenId = screenId,
            ActionCd = actionCd,
            EventCd = kaisaiCd,
            EmployeeCd = workerCd,
            CooperateCd = emp?.CooperateCd,
            FamilyName = emp?.FamilyName,
            FirstName = emp?.FirstName,
            EntryTime = entryTime,
            ExitTime = exitTime,
            TResart = tResart,
            UTantoCd = opCd,
            UTimeStamp = DateTime.Now
        };

        _db.OperationLogs.Add(log);
        await _db.SaveChangesAsync();
    }
    catch
            {
                // ログ記録失敗でも業務処理は止めない
            }

        }


        // 一括入退場登録画面（連続登録）
        [HttpGet]
public async Task<IActionResult> Batch(string kind = "IN")
{
    var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);
    ViewBag.CurrentKaisaiCd = kaisaiCd;
    ViewBag.Kind = (kind ?? "IN").ToUpper();

    // イベント情報表示用
    if (!string.IsNullOrEmpty(kaisaiCd))
    {
        var ev = await _db.KaisaiEvents.FirstOrDefaultAsync(x => x.KaisaiCd == kaisaiCd);
        ViewBag.Event = ev; // null の場合もあり得る
    }

    // ログイン表示用（支店-社員）
    ViewBag.LoginDisplay = $"{HttpContext.Session.GetString("BRANCH_CD")}-{HttpContext.Session.GetString("EMPLOYEE_CD")}";

    return View();
}

public class RecordRequest
        {
            public string? Code { get; set; }
            public string? Kind { get; set; }
            // 画面から渡される開催コード（セッションが送れない環境の保険）
            public string? KaisaiCd { get; set; }
        }
        private async Task<(bool ok, string result, string mark, string message, string? name)>
            ProcessEntryExitAsync(string workerCd, string kind)
        {
            var now = DateTime.Now;
            var hhmm = now.ToString("HHmm");

            string? kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);
            if (string.IsNullOrEmpty(kaisaiCd))
            {
                return (false, "NG", "×", "イベントが確定していません。", null);
            }

            // 作業員マスタ
            var emp = await _db.Employees.FirstOrDefaultAsync(x => x.EmployeeCd == workerCd);
            if (emp == null)
            {
                return (false, "WARN", "△", "作業員情報マスタに登録がありません。", null);
            }

            var row = await _db.EntryExitLogs
                .FirstOrDefaultAsync(x => x.KaisaiCd == kaisaiCd && x.EmployeeCd == workerCd);

            // 退場で入場なし
            if (kind == "OUT" && (row == null || string.IsNullOrWhiteSpace(row.EntryTime)))
            {
                return (false, "WARN", "△", "入場記録がないため退場登録できません。", emp.DisplayName);
            }

            if (row == null)
            {
                row = new Tt02EntryExit
                {
                    KaisaiCd = kaisaiCd,
                    EmployeeCd = workerCd,
                    CooperateCd = emp.CooperateCd,
                    FamilyName = emp.FamilyName,
                    FirstName = emp.FirstName
                };
                _db.EntryExitLogs.Add(row);
            }

            if (kind == "IN")
            {
                // 入場は何度でもOK（既存データは変更しない）
                if (string.IsNullOrEmpty(row.EntryTime))
                {
                    row.EntryTime = hhmm;
                }
            }
            else
            {
                // 退場は入場がある場合のみ
                if (string.IsNullOrEmpty(row.EntryTime))
                {
                    return (false, "WARN", "△", "入場記録がないため退場登録できません。", emp.DisplayName);
                }
                row.ExitTime = hhmm;
            }

            await _db.SaveChangesAsync();

            return (true, "OK", "〇",
                kind == "IN" ? "入場記録OK" : "退場記録OK",
                emp.DisplayName);
        }


        // QR または手入力で入退場記録（連続登録）

[HttpPost]
public async Task<IActionResult> Record([FromBody] RecordRequest req)
{
    var now = DateTime.Now;
    var hhmm = now.ToString("HHmm");

    if (req == null || string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Kind))
    {
        return Json(new { ok = false, message = "パラメータ不正", time = hhmm });
    }

    var workerCd = req.Code.Trim();
    var kind = req.Kind.ToUpper().Trim();

    // 10桁チェックは今まで通り
    if (!IsEmployeeCode(workerCd))
    {
        return Json(new
        {
            ok = false,
            result = "NG",
            mark = "×",
            message = "QRコードが読めません（作業員ID形式不正）。",
            code = workerCd,
            time = hhmm
        });
    }

    var result = await ProcessEntryExitAsync(workerCd, kind);

    return Json(new
    {
        ok = result.ok,
        result = result.result,
        mark = result.mark,
        message = result.message,
        code = workerCd,
        name = result.name,
        time = hhmm
    });
}
        public class TempRecordRequest
        {
            public string? CompanyCd { get; set; }
            public string? WorkerName { get; set; }
            public string? Kind { get; set; }
        }
        [HttpGet]
        public async Task<IActionResult> TempInput(string kind)
        {
            var kaisaiCd = HttpContext.Session.GetString("CurrentKaisaiCd");
            if (string.IsNullOrEmpty(kaisaiCd))
            {
                TempData["Error"] = "イベントが確定していません。";
                return RedirectToAction("Index", "EventSelection");
            }

            var ev = await _db.KaisaiEvents
                .FirstOrDefaultAsync(x => x.KaisaiCd == kaisaiCd);

            if (ev == null)
            {
                TempData["Error"] = "イベント情報が取得できません。";
                return RedirectToAction("Index", "EventSelection");
            }

            ViewBag.Event = ev;


            ViewBag.LoginDisplay =
                $"{HttpContext.Session.GetString("BRANCH_CD")}-{HttpContext.Session.GetString("EMPLOYEE_CD")}";

            kind = (kind ?? "").ToUpper();
            if (kind != "IN" && kind != "OUT")
                return RedirectToAction("Index", "EventSelection");

            ViewBag.Kind = kind;
            ViewBag.ModeLabel = kind == "IN" ? "入場" : "退場";
            ViewBag.Title = "入退場登録（臨時対応窓口）";

            return View();
        }


        public async Task<IActionResult> TempRecord([FromBody] TempRecordRequest req)
        {
            var now = DateTime.Now;
            var hhmm = now.ToString("HHmm");

            if (req == null || string.IsNullOrWhiteSpace(req.CompanyCd) || string.IsNullOrWhiteSpace(req.WorkerName))
            {
                return Json(new { ok = false, message = "必須項目が未入力です", time = hhmm });
            }

            // ★ 臨時入力用：仮の作業員コードを生成
            // 例：T + 時刻 + 連番（業務ルールに応じて変更可）
            var workerCd = "T" + DateTime.Now.ToString("HHmmssfff");

            var kind = (req.Kind ?? "IN").ToUpper();

            // 既存ロジックを流用
            var result = await ProcessEntryExitAsync(workerCd, kind);

            return Json(new
            {
                ok = result.ok,
                message = result.message,
                time = hhmm
            });
        }

    }

}


