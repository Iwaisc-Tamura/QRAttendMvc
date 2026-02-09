
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
using System.Text;

namespace QRAttendMvc.Controllers
{
    public class AttendeeSearchController : Controller
    {
        private readonly AppDbContext _db;
        private const string SessionKeyCurrentKaisaiCd = "CurrentKaisaiCd";

        public AttendeeSearchController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? companyKana,
            string? companyName,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? workerId,
            DateTime? birthDate,
            bool includeLower)
        {
            // 現行仕様：イベントは GT01_KAISAI_EVENT を選択してセッションに保持する。
            // 名簿検索画面では「現在選択中のイベント」は必須ではないため、ここでは参照のみ。
            ViewBag.CurrentKaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            var q = _db.Employees.AsQueryable();

            if (!string.IsNullOrWhiteSpace(workerId))
                q = q.Where(p => p.EmployeeCd.Contains(workerId));

            if (!string.IsNullOrWhiteSpace(companyName))
                // 会社名での検索はマスタ未使用のため未対応（必要なら協力会社マスタと連携）
                ;

            ViewBag.CompanyKana = companyKana;
            ViewBag.CompanyName = companyName;
            ViewBag.WorkerKanaLast = workerKanaLast;
            ViewBag.WorkerKanaFirst = workerKanaFirst;
            ViewBag.WorkerId = workerId;
            ViewBag.BirthDate = birthDate?.ToString("yyyy-MM-dd");
            ViewBag.IncludeLower = includeLower;

            var employees = await q
                .OrderBy(p => p.EmployeeCd)
                .ToListAsync();

            var rows = new List<AttendeeSearchRow>();

            foreach (var p in employees)
            {
                DateTime? lastIn = null;
                DateTime? lastOut = null;

                var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);
                if (!string.IsNullOrEmpty(kaisaiCd))
                {
                    var row = await _db.EntryExitLogs
                        .FirstOrDefaultAsync(e => e.EmployeeCd == p.EmployeeCd && e.KaisaiCd == kaisaiCd);

                    lastIn = ParseTodayHm(row?.EntryTime);
                    lastOut = ParseTodayHm(row?.ExitTime);
                }

                rows.Add(new AttendeeSearchRow
                {
                    CompanyName = "",
                    WorkerId = p.EmployeeCd,
                    WorkerName = p.DisplayName,

                    LastInTime = lastIn,
                    LastOutTime = lastOut,
                    RoleType = "名簿"
                });
            }

            return View(rows);
        }

        [HttpGet]
        public async Task<FileResult> Export(
            string? companyKana,
            string? companyName,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? workerId,
            DateTime? birthDate,
            bool includeLower)
        {
            var q = _db.Employees.AsQueryable();

            if (!string.IsNullOrWhiteSpace(workerId))
                q = q.Where(p => p.EmployeeCd.Contains(workerId));

            if (!string.IsNullOrWhiteSpace(companyName))
                ;

            var employees = await q
                .OrderBy(p => p.EmployeeCd)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("会社名	作業員ID	作業員名	生年月日	名簿対象除外日	入場記録	退場記録	区分");

            string Esc(string? v) => (v ?? "").Replace("	", " ");

            foreach (var p in employees)
            {
                DateTime? lastIn = null;
                DateTime? lastOut = null;

                var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);
                if (!string.IsNullOrEmpty(kaisaiCd))
                {
                    var row = await _db.EntryExitLogs
                        .FirstOrDefaultAsync(e => e.EmployeeCd == p.EmployeeCd && e.KaisaiCd == kaisaiCd);

                    lastIn = ParseTodayHm(row?.EntryTime);
                    lastOut = ParseTodayHm(row?.ExitTime);
                }

                sb.AppendLine(string.Join("\t", new[]
                {
                    "",
                    Esc(p.EmployeeCd),
                    Esc(p.DisplayName),
                    "",
                    "",
                    lastIn?.ToString("HH:mm") ?? "",
                    lastOut?.ToString("HH:mm") ?? "",
                    "名簿"
                }));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            var fileName = $"attendee_{DateTime.Now:yyyyMMdd_HHmmss}.tsv";
            return File(bytes, "text/tab-separated-values", fileName);
        }

        private static DateTime? ParseTodayHm(string? hhmm)
        {
            if (string.IsNullOrWhiteSpace(hhmm)) return null;
            var s = hhmm.Trim();
            if (s.Length != 4) return null;
            if (!int.TryParse(s.Substring(0, 2), out var h)) return null;
            if (!int.TryParse(s.Substring(2, 2), out var m)) return null;
            if (h < 0 || h > 23 || m < 0 || m > 59) return null;
            return DateTime.Today.AddHours(h).AddMinutes(m);
        }
    }
}
