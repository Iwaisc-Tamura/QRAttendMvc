using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using QRAttendMvc.Models;
using QRAttendMvc.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QRAttendMvc.Controllers
{
    /// <summary>
    /// イベント選択画面仕様.xlsx 準拠
    /// - GT01_KAISAI_EVENT を事前読込
    /// - 抽出条件：支店コード(パラメータ/セッション) + 開催日(本日)
    /// - 前提：支店コード・開催日・区分(EventCd)・開始時刻(StartTime)で一意
    /// - ①上記条件で1件なら「表示のみ」（自動確定）
    /// - ②複数なら「区分 → 開始時刻」の順で絞り込み
    ///   ※区分が1件の場合は区分は表示のみで開始時刻を選択
    ///   ※区分選択後、開始時刻が1件の場合は開始時刻は表示のみ（自動確定）
    /// </summary>
    public class EventSelectionController : BaseController
    {
        private readonly AppDbContext _db;

        private const string SessionKeyCurrentKaisaiCd = "CurrentKaisaiCd";

        /* 追加 2026.02.16 Takada */
        private const string SessionKeyEventDate = "SelectedEventDate";
        private const string SessionKeyKubun = "SelectedKubun";
        private const string SessionKeyKyoten = "SelectedKyoten";
        private const string SessionKeyPlace = "SelectedPlace";
        private const string SessionKeyStartTime = "SelectedStartTime";
        private const string SessionKeyEndTime = "SelectedEndTime";
        private const string SessionKeyUketsuke = "SelectedUketsuke";

        public EventSelectionController(IActionLogService logService, AppDbContext db)
                : base(logService)
        {
            _db = db;
        }

        /// <summary>
        /// プルダウン選択時ログ（区分/開始時刻の選択で呼ぶ）
        /// eventCd は「選択したときの CurrentKaisaiCd」を入れる想定
        /// </summary>
        private async Task WritePullDownLogAsync()
        {
            // 「選択したときの CurrentKaisaiCd」
            var currentKaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            await _logService.ActionLogSaveAsync(
                screenId: "G20",
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
                // ご指定どおり：EMPLOYEE_CD を uTantoCd に入れる
                uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                uTimeStamp: DateTime.Now
            );
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var empCd = HttpContext.Session.GetString("EMPLOYEE_CD") ?? string.Empty;

            ViewBag.LoginText = (!string.IsNullOrEmpty(userBranch) && !string.IsNullOrEmpty(empCd))
                ? $"{userBranch}-{empCd}"
                : "XXXXXX-YYYYY";

            var today = DateTime.Today;

            // 支店のイベントを事前読込 → KAISAI_YMD は "2026/1/30" のようにゼロ埋め無しが混在するため
            // SQL側で yyyyMMdd 文字列比較すると一致しないケースがある。
            // ここでは支店で絞って取得し、日付はメモリ上で DateTime にパースして当日比較する。
            var events = (await _db.KaisaiEvents
                    .Where(e => e.BranchCd == userBranch && e.KaisaiYmd != null)
                    .OrderBy(e => e.EventCd)
                    .ThenBy(e => e.StartTime)
                    .ToListAsync())
                .Where(e => TryParseKaisaiDate(e.KaisaiYmd!, out var d) && d.Date == today)
                .ToList();

            // 0件メッセージ
            if (events.Count == 0)
            {
                ViewData["BusinessMessage"] =
                    "本日、該当する開催イベントがありません。\n" +
                    "支店コード・開催日・イベント登録状況をご確認ください。";
                ViewBag.PreloadedEvents = Array.Empty<object>();
                return View();
            }

            // 1件なら自動確定（表示のみ）
            if (events.Count == 1)
            {
                await SetCurrentEventAsync(events[0]);
                ViewBag.AutoSelected = true;
                ViewBag.Selected = ToDetailVm(events[0]);
                ViewBag.PreloadedEvents = Array.Empty<object>(); // JS不要
                return View();
            }

            // 複数件：JSで絞り込みするため一覧を渡す（必要項目のみ）
            ViewBag.AutoSelected = false;
            ViewBag.PreloadedEvents = events.Select(e => new
            {
                e.KaisaiCd,
                e.EventCd,
                e.EventName,
                StartTime = FormatHm(e.StartTime),
                StartTimeRaw = e.StartTime,
                ReceptTime = FormatHm(e.ReceptTime),
                EndTime = FormatHm(e.EndTime),
                KaisaiYmd = (TryParseKaisaiDate(e.KaisaiYmd ?? "", out var d) ? d.ToString("yyyy/MM/dd") : ""),
                e.BranchCd,
                BranchName = e.BranchName ?? "",
                Location = e.Location ?? "",
                e.Nendo,
                e.TrKbn,
                e.QrKbn
            }).ToList();

            // ★ 当日の「最後に選んだ区分（KAISAI_CD）」を TT01_TARGET_EVENT から取得
            var todayKey = DateTime.Today.ToString("yyyyMMdd"); // サーバー当日

            var latestTargetToday = await _db.TargetEvents
                .Where(x => x.BranchCd == userBranch
                         && x.SelectYmdTime != null
                         && x.SelectYmdTime.Substring(0, 8) == todayKey) // 当日分だけ
                .OrderByDescending(x => x.SelectYmdTime)
                .FirstOrDefaultAsync();

            // ★ TT01はKAISAI_CD（開催コード）なので、events から一致イベントを特定
            Gt01KaisaiEvent? defaultEv = null;

            if (!string.IsNullOrWhiteSpace(latestTargetToday?.KaisaiCd))
            {
                var kaisaiCd = latestTargetToday!.KaisaiCd.Trim();
                defaultEv = events.FirstOrDefault(e => (e.KaisaiCd ?? "").Trim() == kaisaiCd);
            }

            // ★ TT01 に該当があれば「常に選択済み（確定状態）」にする
            if (defaultEv != null)
            {
                await SetCurrentEventAsync(defaultEv);               // ←これで戻っても確定が維持される
                ViewBag.DefaultKaisaiCd = (defaultEv.EventCd ?? "").Trim(); // 区分も選択済みに
            }
            else
            {
                ViewBag.DefaultKaisaiCd = "";
            }

            return View();
        }

        /// <summary>区分一覧（EventCd + EventName）</summary>
        [HttpGet]
        public IActionResult GetDivisions()
        {
            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var today = DateTime.Today;

            var list = _db.KaisaiEvents
                .Where(e => e.BranchCd == userBranch && e.KaisaiYmd != null)
                .AsEnumerable()
                .Where(e => TryParseKaisaiDate(e.KaisaiYmd!, out var d) && d.Date == today)
                .Select(e => new { divisionCode = e.EventCd, divisionName = e.EventName })
                .Distinct()
                .OrderBy(x => x.divisionCode)
                .ToList();

            return Json(list);
        }

        /// <summary>
        /// 開始時刻一覧（区分で絞り込み）
        /// ★ここが「1つ目のプルダウン（区分）を選択したタイミング」で呼ばれる想定なのでログ出力
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStartTimes(string divisionCode)
        {
            // 区分プルダウン選択ログ
            await WritePullDownLogAsync();

            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var today = DateTime.Today;

            var times = _db.KaisaiEvents
                .Where(e => e.BranchCd == userBranch && e.KaisaiYmd != null && e.EventCd == divisionCode)
                .AsEnumerable()
                .Where(e => TryParseKaisaiDate(e.KaisaiYmd!, out var d) && d.Date == today)
                .Select(e => e.StartTime)
                .Where(x => x != null && x != "")
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return Json(times.Select(FormatHm));
        }

        /// <summary>
        /// 区分＋開始時刻でイベント確定（一意前提）
        /// ★ここが「2つ目のプルダウン（開始時刻）を選択したタイミング」で呼ばれる想定なのでログ出力
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Decide(string divisionCode, string startTime)
        {
            // 開始時刻プルダウン選択ログ
            await WritePullDownLogAsync();

            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var today = DateTime.Today;
            var st = NormalizeHm(startTime); // "HH:mm" -> "HHmm"

            var ev = (await _db.KaisaiEvents
                    .Where(e => e.BranchCd == userBranch && e.KaisaiYmd != null && e.EventCd == divisionCode && (e.StartTime ?? "") == st)
                    .ToListAsync())
                .FirstOrDefault(e => TryParseKaisaiDate(e.KaisaiYmd!, out var d) && d.Date == today);

            if (ev == null)
                return NotFound("該当するイベントが存在しません。");

            await SetCurrentEventAsync(ev);
            return Json(ToDetailVm(ev));
        }

        [HttpPost]
        public IActionResult GoToBatch(string mode)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd)))
            {
                TempData["Error"] = "イベントが確定していません。";
                return RedirectToAction("Index");
            }
            return RedirectToAction("Batch", "Scan", new { kind = mode });
        }

        [HttpPost]
        public IActionResult GoToTemp(string kind)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd)))
            {
                TempData["Error"] = "イベントが確定していません。";
                return RedirectToAction("Index");
            }

            return RedirectToAction("TempInput", "Scan", new { kind = kind });
        }

        private async Task SetCurrentEventAsync(Gt01KaisaiEvent ev)
        {
            HttpContext.Session.SetString(SessionKeyCurrentKaisaiCd, ev.KaisaiCd ?? "");

            // 追加2026.02.16 Takada 表示用イベント情報を Session に保存
            HttpContext.Session.SetString(SessionKeyEventDate,
                (TryParseKaisaiDate(ev.KaisaiYmd ?? "", out var d) ? d.ToString("yyyy/MM/dd") : ""));

            HttpContext.Session.SetString(SessionKeyKubun, $"{ev.EventCd} {ev.EventName}".Trim());
            HttpContext.Session.SetString(SessionKeyKyoten, $"{ev.BranchCd} {ev.BranchName}".Trim());
            HttpContext.Session.SetString(SessionKeyPlace, ev.Location ?? "");

            HttpContext.Session.SetString(SessionKeyStartTime, FormatHm(ev.StartTime));
            HttpContext.Session.SetString(SessionKeyEndTime, FormatHm(ev.EndTime));
            HttpContext.Session.SetString(SessionKeyUketsuke, FormatHm(ev.ReceptTime));

            // TT01_TARGET_EVENT 更新（支店＋開催コード）
            var te = await _db.TargetEvents
                .FirstOrDefaultAsync(x => x.BranchCd == ev.BranchCd && x.KaisaiCd == ev.KaisaiCd);

            if (te == null)
            {
                te = new TargetEvent
                {
                    BranchCd = ev.BranchCd ?? "",
                    KaisaiCd = ev.KaisaiCd ?? "",
                    SelectYmdTime = DateTime.Now.ToString("yyyyMMdd-HHmmss")
                };
                _db.TargetEvents.Add(te);
            }
            else
            {
                te.SelectYmdTime = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            }

            await _db.SaveChangesAsync();
        }

        [HttpPost]
        public IActionResult GoToTempInput(string mode)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd)))
            {
                TempData["Error"] = "イベントが確定していません。";
                return RedirectToAction("Index");
            }
            return RedirectToAction("Batch", "Scan", new { kind = mode });
        }

        private static object ToDetailVm(Gt01KaisaiEvent ev)
        {
            return new
            {
                eventCode = ev.EventCd,
                eventName = ev.EventName ?? "",
                fiscalYear = ev.Nendo,
                branchCd = ev.BranchCd,
                branchName = ev.BranchName ?? "",
                kaisaiYmd = (TryParseKaisaiDate(ev.KaisaiYmd ?? "", out var d) ? d.ToString("yyyy/MM/dd") : ""),
                location = ev.Location ?? "",
                receptTime = FormatHm(ev.ReceptTime),
                startTime = FormatHm(ev.StartTime),
                endTime = FormatHm(ev.EndTime),
                trKbn = ev.TrKbn,
                qrKbn = ev.QrKbn
            };
        }

        private static bool TryParseKaisaiDate(string ymd, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(ymd)) return false;
            ymd = ymd.Trim();

            // DBは "yyyy/M/d" のようにゼロ埋め無しが混在
            var formats = new[]
            {
                "yyyy/M/d", "yyyy/MM/dd", "yyyy/M/dd", "yyyy/MM/d",
                "yyyy-M-d", "yyyy-MM-dd", "yyyy-M-dd", "yyyy-MM-d"
            };

            if (DateTime.TryParseExact(ymd, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date)) return true;

            // 念のため通常パース（ja-JP）
            return DateTime.TryParse(ymd, CultureInfo.GetCultureInfo("ja-JP"),
                DateTimeStyles.None, out date);
        }

        private static string NormalizeYmd(string? ymd)
        {
            if (string.IsNullOrWhiteSpace(ymd)) return "";
            return ymd.Replace("/", "").Replace("-", "").Trim();
        }

        private static string FormatYmd(string ymd)
        {
            if (string.IsNullOrWhiteSpace(ymd)) return string.Empty;
            if (ymd.Length == 8)
                return $"{ymd.Substring(0, 4)}/{ymd.Substring(4, 2)}/{ymd.Substring(6, 2)}";
            return ymd;
        }

        private static string FormatHm(string? hhmm)
        {
            if (string.IsNullOrWhiteSpace(hhmm)) return string.Empty;
            var s = hhmm.Trim();
            if (s.Length == 4)
                return $"{s.Substring(0, 2)}:{s.Substring(2, 2)}";
            return s;
        }

        private static string NormalizeHm(string? hm)
        {
            if (string.IsNullOrWhiteSpace(hm)) return string.Empty;
            var s = hm.Trim();
            if (s.Contains(":"))
            {
                var parts = s.Split(':');
                if (parts.Length == 2)
                    return parts[0].PadLeft(2, '0') + parts[1].PadLeft(2, '0');
            }
            return s.Length == 4 ? s : string.Empty;
        }
    }
}