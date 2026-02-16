
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
using System.Text;
using System.Text.RegularExpressions;

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
            bool? searched)
        {
            // 現行仕様：イベントは GT01_KAISAI_EVENT を選択してセッションに保持する。
            // 名簿検索画面では「現在選択中のイベント」は必須ではないため、ここでは参照のみ。
            ViewBag.CurrentKaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            // 追加 2026.02.16 Takada ；ログインコードの遷移
            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var empCd = HttpContext.Session.GetString("EMPLOYEE_CD") ?? string.Empty;

            ViewBag.LoginText = (!string.IsNullOrEmpty(userBranch) && !string.IsNullOrEmpty(empCd))
                ? $"{userBranch}-{empCd}"
                : "     -     ";

            // 追加 2026.02.16 Takada ；イベント表示用（EventSelectionController で Session に入れた値）
            ViewBag.EventDate = HttpContext.Session.GetString("SelectedEventDate") ?? "";
            ViewBag.Kubun = HttpContext.Session.GetString("SelectedKubun") ?? "";
            ViewBag.Kyoten = HttpContext.Session.GetString("SelectedKyoten") ?? "";
            ViewBag.Place = HttpContext.Session.GetString("SelectedPlace") ?? "";
            ViewBag.StartTime = HttpContext.Session.GetString("SelectedStartTime") ?? "";
            ViewBag.EndTime = HttpContext.Session.GetString("SelectedEndTime") ?? "";
            ViewBag.Uketsuke = HttpContext.Session.GetString("SelectedUketsuke") ?? "";

            /* 追加 2026.02.17 Takada*/
            var hasSearched = searched.GetValueOrDefault();

            // 検索条件の保持
            ViewBag.CompanyKana = companyKana ?? "";
            ViewBag.CompanyName = companyName ?? "";
            ViewBag.WorkerKanaLast = workerKanaLast ?? "";
            ViewBag.WorkerKanaFirst = workerKanaFirst ?? "";
            ViewBag.WorkerId = workerId ?? "";
            ViewBag.BirthDate = birthDate?.ToString("yyyy-MM-dd") ?? "";

            // 初回表示は空リストで返す
            if (!hasSearched)
            {
                return View(Enumerable.Empty<AttendeeSearchRow>());
            }
            // 空csv出力防止
            if (searched != true)
                return BadRequest("検索実行後にCSV出力してください。");

            /* 修正 2025.02.16 Takada */
            var q = BuildAttendeeQuery(companyKana, companyName, workerKanaLast, workerKanaFirst, workerId, birthDate);

            var list = await q
                .OrderBy(x => x.Emp.EmployeeCd)
                .ToListAsync();

            var rows = new List<AttendeeSearchRow>();

            /* 追加 2025.02.17 Takada */
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd) ?? "";

            // 一括でログを取る（N+1解消）
            var employeeCds = list.Select(x => x.Emp.EmployeeCd).Distinct().ToList();
            var logMap = await LoadEntryExitMapAsync(kaisaiCd, employeeCds);

            /* 修正 2025.02.16 Takada */
            foreach (var x in list)
            {
                var p = x.Emp;

                DateTime? lastIn = null;
                DateTime? lastOut = null;

                /* 修正 2025.02.17 Takada */
                if (!string.IsNullOrEmpty(kaisaiCd) && logMap.TryGetValue(p.EmployeeCd, out var row))
                {
                    lastIn = ParseTodayHm(row.EntryTime);
                    lastOut = ParseTodayHm(row.ExitTime);
                }

                rows.Add(new AttendeeSearchRow
                {
                    CompanyName = x.CompanyName,
                    WorkerId = p.EmployeeCd,
                    WorkerName = p.DisplayName,

                    /* 追加 2026.02.14 Takada　*/
                    BirthDate = ParseYyyyMMdd(p.BirthYmd),
                    ExcludeDate = ParseYyyyMMdd(p.RetireYmd),

                    LastInTime = lastIn,
                    LastOutTime = lastOut,
                    RoleType = "名簿"
                });
            }

            return View(rows);
        }

        private IQueryable<AttendeeJoined> BuildAttendeeQuery(
            string? companyKana,
            string? companyName,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? workerId,
            DateTime? birthDate)
        {
            // ★ JOIN（LEFT JOIN）
            var q =
                from e in _db.Employees.AsNoTracking()
                join c in _db.Cooperates.AsNoTracking()
                    on e.CooperateCd equals c.CooperateCd into gj
                from c in gj.DefaultIfEmpty()
                select new AttendeeJoined
                {
                    Emp = e,
                    CompanyName = c != null ? (c.CompanyName ?? "") : "",
                    CompanyKana = c != null ? (c.CompanyNameKana ?? "") : ""
                };


            if (!string.IsNullOrWhiteSpace(workerKanaFirst))
            {
                var key = NormalizeKanaToWide(workerKanaFirst);
                q = q.Where(x => (x.Emp.FirstNameKana ?? "").Contains(key));
            }

            /* 追加 2026.02.16 Takada*/
            if (!string.IsNullOrWhiteSpace(workerKanaLast))
            {
                var key = NormalizeKanaToWide(workerKanaLast);
                q = q.Where(x => (x.Emp.FamilyNameKana ?? "").Contains(key));
            }

            // ★ WHERE（共通）
            if (!string.IsNullOrWhiteSpace(workerId))
            {
                var key = workerId.Trim();
                q = q.Where(x => x.Emp.EmployeeCd.Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(companyName))
            {
                var key = companyName.Trim();
                q = q.Where(x => x.CompanyName.Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(companyKana))
            {
                 var key = NormalizeKanaToWide(companyKana);// 全角寄せ

                q = q.Where(x =>
                    (x.CompanyKana ?? "").Contains(key));
            }

            /* 追加 2026.02.16 Takada */
            if (birthDate.HasValue)
            {
                var ymd = birthDate.Value.ToString("yyyyMMdd");
                q = q.Where(x => x.Emp.BirthYmd == ymd);
            }

            return q;
        }

        // BuildAttendeeQuery 用のDTO（Controller内でOK）
        private class AttendeeJoined
        {
            public Gm01Employee Emp { get; set; } = default!;
            public string CompanyName { get; set; } = "";
            public string CompanyKana { get; set; } = "";
        }

        public async Task<FileResult> Export(
            string? companyKana,
            string? companyName,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? workerId,
            DateTime? birthDate,
            bool? searched)
        {

            if (searched != true)
            {
                var readmeBytes = Encoding.UTF8.GetPreamble()
                    .Concat(Encoding.UTF8.GetBytes("検索実行後にCSV出力してください。"))
                    .ToArray();

                return File(readmeBytes, "text/plain", "readme.txt");
            }

            /* 変更 2026.02.16 Takada*/
            var q = BuildAttendeeQuery(companyKana, companyName, workerKanaLast, workerKanaFirst, workerId, birthDate);

            var list = await q
                .OrderBy(x => x.Emp.EmployeeCd)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("会社名	作業員ID	作業員名	生年月日	名簿対象除外日	入場記録	退場記録	区分");

            string Esc(string? v) => (v ?? "").Replace("	", " ");

            /* 追加 2026.02.17 Takada */
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd) ?? "";

            // 一括でログを取る（N+1解消）
            var employeeCds = list.Select(x => x.Emp.EmployeeCd).Distinct().ToList();
            var logMap = await LoadEntryExitMapAsync(kaisaiCd, employeeCds);

            /* 変更 2026.02.16 Takada */
            foreach (var x in list)
            {
                var p = x.Emp;

                DateTime? lastIn = null;
                DateTime? lastOut = null;

                /* 変更 2026.02.17 Takada */
                if (!string.IsNullOrEmpty(kaisaiCd) && logMap.TryGetValue(p.EmployeeCd, out var row))
                {
                    lastIn = ParseTodayHm(row.EntryTime);
                    lastOut = ParseTodayHm(row.ExitTime);
                }

                sb.AppendLine(string.Join("\t", new[]
                {
                    Esc(x.CompanyName),
                    Esc(p.EmployeeCd),
                    Esc(p.DisplayName),

                    /* 追加 2026.02.14 Takada　*/
                    ParseYyyyMMdd(p.BirthYmd)?.ToString("yyyy/MM/dd") ?? "",
                    ParseYyyyMMdd(p.RetireYmd)?.ToString("yyyy/MM/dd") ?? "",

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
 
        /* 追加 2026.02.14　Takada */
        private static DateTime? ParseYyyyMMdd(string? yyyymmdd)
        {
            if (string.IsNullOrWhiteSpace(yyyymmdd)) return null;
            var s = yyyymmdd.Trim();
            if (s.Length != 8) return null;

            if (!int.TryParse(s.Substring(0, 4), out var y)) return null;
            if (!int.TryParse(s.Substring(4, 2), out var m)) return null;
            if (!int.TryParse(s.Substring(6, 2), out var d)) return null;

            try { return new DateTime(y, m, d); }
            catch { return null; }
        }

        private static string NormalizeKanaToWide(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            // 前後空白除去
            s = s.Trim();

            // 全角スペース → 半角スペース
            s = s.Replace('\u3000', ' ');

            // 連続スペースを1つに
            s = Regex.Replace(s, @"\s+", " ");

            // Unicode正規化（濁点分離対策）
            s = s.Normalize(NormalizationForm.FormKC);

            return s;
        }

        private async Task<Dictionary<string, Tt02EntryExit>> LoadEntryExitMapAsync(
            string kaisaiCd,
            List<string> employeeCds)
        {
            var map = new Dictionary<string, Tt02EntryExit>();

            if (string.IsNullOrWhiteSpace(kaisaiCd) || employeeCds.Count == 0)
                return map;

            // SQL Server の IN パラメータ上限対策（大人数でも落ちないように）
            const int batchSize = 1000;

            for (int i = 0; i < employeeCds.Count; i += batchSize)
            {
                var batch = employeeCds.Skip(i).Take(batchSize).ToList();

                var rows = await _db.EntryExitLogs.AsNoTracking()
                    .Where(l => l.KaisaiCd == kaisaiCd && batch.Contains(l.EmployeeCd))
                    .ToListAsync();

                foreach (var r in rows)
                {
                    // 同一 EmployeeCd が複数あっても最後に入ったものを優先（基本は一意のはず）
                    map[r.EmployeeCd] = r;
                }
            }

            return map;
        }


    }
}
