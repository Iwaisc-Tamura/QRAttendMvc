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
        /// A03（選択）ログ：EVENT_CD には必ず KaisaiCd（開催コード）を入れる
        /// </summary>
        private async Task WriteSelectLogAsync(string? kaisaiCd)
        {
            await _logService.ActionLogSaveAsync(
                screenId: "G20",
                actionCd: "A03",
                eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd.Trim(),
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

            var events = (await _db.KaisaiEvents
                    .Where(e => e.BranchCd == userBranch && e.KaisaiYmd != null)
                    .OrderBy(e => e.EventCd)
                    .ThenBy(e => e.StartTime)
                    .ToListAsync())
                .Where(e => TryParseKaisaiDate(e.KaisaiYmd!, out var d) && d.Date == today)
                .ToList();

            // 0件
            if (events.Count == 0)
            {
                ViewData["BusinessMessage"] =
                    "本日、該当する開催イベントがありません。\n" +
                    "支店コード・開催日・イベント登録状況をご確認ください。";
                ViewBag.PreloadedEvents = Array.Empty<object>();

                // A01（画面アクセス）：未確定なので EVENT_CD=null
                await _logService.ActionLogSaveAsync(
                    screenId: "G20",
                    actionCd: "A01",
                    eventCd: null,
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: DateTime.Now
                );

                return View();
            }

            // ★ 当日の「最後に選んだ開催コード（KAISAI_CD）」を TT01_TARGET_EVENT から取得
            var todayKey = DateTime.Today.ToString("yyyyMMdd");

            var latestTargetToday = await _db.TargetEvents
                .Where(x => x.BranchCd == userBranch
                         && x.SelectYmdTime != null
                         && x.SelectYmdTime.Substring(0, 8) == todayKey)
                .OrderByDescending(x => x.SelectYmdTime)
                .FirstOrDefaultAsync();

            // TT01は KaisaiCd（開催コード）を持つので、events から一致イベントを探す
            Gt01KaisaiEvent? defaultEv = null;
            if (!string.IsNullOrWhiteSpace(latestTargetToday?.KaisaiCd))
            {
                var kaisaiCd = latestTargetToday!.KaisaiCd.Trim();
                defaultEv = events.FirstOrDefault(e => (e.KaisaiCd ?? "").Trim() == kaisaiCd);
            }

            // 1件なら自動確定
            if (events.Count == 1)
            {
                await SetCurrentEventAsync(events[0]);

                // A01：EVENT_CD=KaisaiCd
                await _logService.ActionLogSaveAsync(
                    screenId: "G20",
                    actionCd: "A01",
                    eventCd: string.IsNullOrWhiteSpace(events[0].KaisaiCd) ? null : events[0].KaisaiCd.Trim(),
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: DateTime.Now
                );

                ViewBag.AutoSelected = true;
                ViewBag.Selected = ToDetailVm(events[0]);
                ViewBag.PreloadedEvents = Array.Empty<object>();
                return View();
            }

            // 複数件：JSに渡す
            ViewBag.AutoSelected = false;
            ViewBag.PreloadedEvents = events.Select(e => new
            {
                e.KaisaiCd,    // ※保持（ログ用・将来拡張用）
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

            // defaultEv があれば「確定状態」にする
            if (defaultEv != null)
            {
                await SetCurrentEventAsync(defaultEv);
                ViewBag.DefaultKaisaiCd = (defaultEv.EventCd ?? "").Trim(); // 画面は区分(EventCd)を選択済みに見せる

                // A01：EVENT_CD=KaisaiCd
                await _logService.ActionLogSaveAsync(
                    screenId: "G20",
                    actionCd: "A01",
                    eventCd: string.IsNullOrWhiteSpace(defaultEv.KaisaiCd) ? null : defaultEv.KaisaiCd.Trim(),
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: DateTime.Now
                );
            }
            else
            {
                ViewBag.DefaultKaisaiCd = "";

                // A01：未確定なので EVENT_CD=null
                await _logService.ActionLogSaveAsync(
                    screenId: "G20",
                    actionCd: "A01",
                    eventCd: null,
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: DateTime.Now
                );
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
        /// ※仕様変更により「ここでA03ログ」は出さない（KaisaiCdが確定しない可能性があるため）
        /// </summary>
        [HttpGet]
        public IActionResult GetStartTimes(string divisionCode)
        {
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
        /// A03ログ：EVENT_CD=確定した ev.KaisaiCd を入れる
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Decide(string divisionCode, string startTime)
        {
            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var today = DateTime.Today;
            var st = NormalizeHm(startTime);

            var ev = (await _db.KaisaiEvents
                    .Where(e => e.BranchCd == userBranch
                             && e.KaisaiYmd != null
                             && e.EventCd == divisionCode
                             && (e.StartTime ?? "") == st)
                    .ToListAsync())
                .FirstOrDefault(e => TryParseKaisaiDate(e.KaisaiYmd!, out var d) && d.Date == today);

            if (ev == null)
                return NotFound("該当するイベントが存在しません。");

            await SetCurrentEventAsync(ev);

            // ★A03：EVENT_CD=KaisaiCd
            await WriteSelectLogAsync(ev.KaisaiCd);

            return Json(ToDetailVm(ev));
        }

        [HttpPost]
        public async Task<IActionResult> GoToBatch(string mode)
        {
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            if (string.IsNullOrEmpty(kaisaiCd))
            {
                TempData["Error"] = "イベントが確定していません。";
                return RedirectToAction("Index");
            }

            // ★追加：ボタン押下ログ（A02）
            await _logService.ActionLogSaveAsync(
                screenId: "G20",
                actionCd: "A02",
                eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd.Trim(),
                uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                uTimeStamp: DateTime.Now
            );

            return RedirectToAction("Batch", "Scan", new { kind = mode });
        }

        [HttpPost]
        public async Task<IActionResult> GoToTemp(string kind)
        {
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            if (string.IsNullOrEmpty(kaisaiCd))
            {
                TempData["Error"] = "イベントが確定していません。";
                return RedirectToAction("Index");
            }

            // ★追加：ボタン押下ログ（A02）
            await _logService.ActionLogSaveAsync(
                screenId: "G20",
                actionCd: "A02",
                eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd.Trim(),
                uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                uTimeStamp: DateTime.Now
            );

            return RedirectToAction("TempInput", "Scan", new { kind = kind });
        }

        [HttpPost]
        public async Task<IActionResult> GoToAttendeeSearch()
        {
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            await _logService.ActionLogSaveAsync(
                screenId: "G20",
                actionCd: "A02",
                eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd.Trim(),
                uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                uTimeStamp: DateTime.Now
            );

            return RedirectToAction("Index", "AttendeeSearch");
        }

        [HttpPost]
        public async Task<IActionResult> GoToHome()
        {
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            await _logService.ActionLogSaveAsync(
                screenId: "G20",
                actionCd: "A02",
                eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd.Trim(),
                uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                uTimeStamp: DateTime.Now
            );

            return RedirectToAction("Index", "Home");
        }

        private async Task SetCurrentEventAsync(Gt01KaisaiEvent ev)
        {
            // セッションには「開催コード（KaisaiCd）」を保持（※名前は CurrentKaisaiCd のまま）
            HttpContext.Session.SetString(SessionKeyCurrentKaisaiCd, ev.KaisaiCd ?? "");

            // 表示用イベント情報
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

        private static object ToDetailVm(Gt01KaisaiEvent ev)
        {
            return new
            {
                // 画面表示用：eventCode=区分(EventCd)
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

            var formats = new[]
            {
                "yyyy/M/d", "yyyy/MM/dd", "yyyy/M/dd", "yyyy/MM/d",
                "yyyy-M-d", "yyyy-MM-dd", "yyyy-M-dd", "yyyy-MM-d"
            };

            if (DateTime.TryParseExact(ymd, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date)) return true;

            return DateTime.TryParse(ymd, CultureInfo.GetCultureInfo("ja-JP"),
                DateTimeStyles.None, out date);
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