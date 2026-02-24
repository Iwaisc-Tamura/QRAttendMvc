
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
using QRAttendMvc.Services;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace QRAttendMvc.Controllers
{
    public class AttendeeSearchController : BaseController
    {
        private readonly AppDbContext _db;
        private const string SessionKeyCurrentKaisaiCd = "CurrentKaisaiCd";

        public AttendeeSearchController(IActionLogService logService, AppDbContext db)
            : base(logService)
        {
            _db = db;
        }

           [HttpGet]
        public async Task<IActionResult> Index(
            string? companyKana,
            string? companyName,
            string? workerName,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? workerId,
            DateTime? birthDate,
            string? filter,
            bool? searched,
            string? sort,
            string? dir)
        {
            ViewBag.CurrentKaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var empCd = HttpContext.Session.GetString("EMPLOYEE_CD") ?? string.Empty;

            ViewBag.LoginText = (!string.IsNullOrEmpty(userBranch) && !string.IsNullOrEmpty(empCd))
                ? $"{userBranch}-{empCd}"
                : "     -     ";

            var eventDateStr = HttpContext.Session.GetString("SelectedEventDate") ?? "";

            if (DateTime.TryParse(eventDateStr, out var eventDate))
            {
                var ja = new System.Globalization.CultureInfo("ja-JP");
                ViewBag.EventDate = eventDate.ToString("yyyy/MM/dd (ddd)", ja);
            }
            else
            {
                ViewBag.EventDate = eventDateStr;
            }
            ViewBag.Kubun = HttpContext.Session.GetString("SelectedKubun") ?? "";
            ViewBag.Kyoten = HttpContext.Session.GetString("SelectedKyoten") ?? "";
            ViewBag.Place = HttpContext.Session.GetString("SelectedPlace") ?? "";
            ViewBag.StartTime = HttpContext.Session.GetString("SelectedStartTime") ?? "";
            ViewBag.EndTime = HttpContext.Session.GetString("SelectedEndTime") ?? "";
            ViewBag.Uketsuke = HttpContext.Session.GetString("SelectedUketsuke") ?? "";

            var hasSearched = searched.GetValueOrDefault();

            if (!hasSearched)
                return View(Enumerable.Empty<AttendeeSearchRow>());

            if (searched != true)
                return BadRequest("検索実行後にCSV出力してください。");

            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd) ?? "";

            // 母集団
            var q = BuildBaseQuery(kaisaiCd, filter);

            // 他条件
            q = ApplySearchConditions(q, companyKana, companyName, workerName, workerKanaLast, workerKanaFirst, workerId, birthDate);

            // DBソート
            q = ApplySort(q, sort, dir);

            var list = await q.ToListAsync();

            // rows作成
            var rows = new List<AttendeeSearchRow>();
            foreach (var x in list)
            {
                var p = x.Emp;
                rows.Add(new AttendeeSearchRow
                {
                    CompanyName = x.CompanyName,
                    WorkerId = p.EmployeeCd,
                    WorkerName = p.DisplayName,
                    BirthDate = ParseYyyyMMdd(p.BirthYmd),
                    ExcludeDate = ParseYyyyMMdd(p.RetireYmd),
                    LastInTime = ParseTodayHm(x.EntryTime),
                    LastOutTime = ParseTodayHm(x.ExitTime),
                });
            }

            // lastin/lastout はメモリソート
            rows = ApplyRowSort(rows, sort, dir);

            return View(rows);
        }

        private class AttendeeJoined
        {
            public Gm01Employee Emp { get; set; } = default!;
            public string CompanyName { get; set; } = "";
            public string CompanyKana { get; set; } = "";

            // ★追加：TT02の文字列(4桁HHmm想定)
            public string? EntryTime { get; set; }
            public string? ExitTime { get; set; }
        }

        public async Task<FileResult> Export(
            string? companyKana,
            string? companyName,
            string? workerName,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? workerId,
            DateTime? birthDate,
            string? filter,
            bool? searched,
            string? sort,
            string? dir)
        {
            if (searched != true)
            {
                var readmeBytes = Encoding.UTF8.GetPreamble()
                    .Concat(Encoding.UTF8.GetBytes("検索実行後にCSV出力してください。"))
                    .ToArray();

                return File(readmeBytes, "text/plain", "readme.txt");
            }

            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd) ?? "";

            // 母集団
            var q = BuildBaseQuery(kaisaiCd, filter);

            // 他条件
            q = ApplySearchConditions(q, companyKana, companyName, workerName, workerKanaLast, workerKanaFirst, workerId, birthDate);

            // DBソート
            q = ApplySort(q, sort, dir);

            var list = await q.ToListAsync();

            // rows作成
            var rows = new List<AttendeeSearchRow>();
            foreach (var x in list)
            {
                var p = x.Emp;
                rows.Add(new AttendeeSearchRow
                {
                    CompanyName = x.CompanyName,
                    WorkerId = p.EmployeeCd,
                    WorkerName = p.DisplayName,
                    BirthDate = ParseYyyyMMdd(p.BirthYmd),
                    ExcludeDate = ParseYyyyMMdd(p.RetireYmd),
                    LastInTime = ParseTodayHm(x.EntryTime),
                    LastOutTime = ParseTodayHm(x.ExitTime),
                });
            }

            // lastin/lastout はメモリソート
            rows = ApplyRowSort(rows, sort, dir);

            // TSV作成
            var sb = new StringBuilder();
            sb.AppendLine("会社名\t作業員ID\t作業員名\t生年月日\t名簿対象除外日\t入場記録\t退場記録");

            string Esc(string? v) => (v ?? "").Replace("\t", " ");

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join("\t", new[]
                {
            Esc(r.CompanyName),
            Esc(r.WorkerId),
            Esc(r.WorkerName),
            r.BirthDate?.ToString("yyyy/MM/dd") ?? "",
            r.ExcludeDate?.ToString("yyyy/MM/dd") ?? "",
            r.LastInTime?.ToString("HH:mm") ?? "",
            r.LastOutTime?.ToString("HH:mm") ?? ""
        }));
            }

            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
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

        private static IQueryable<AttendeeJoined> ApplySort(IQueryable<AttendeeJoined> q, string? sort, string? dir)
        {
            var key = (sort ?? "").Trim().ToLowerInvariant();
            var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

            // デフォルト
            if (string.IsNullOrEmpty(key)) key = "workerid";

            return key switch
            {
                "company" => desc ? q.OrderByDescending(x => x.CompanyName).ThenByDescending(x => x.Emp.EmployeeCd)
                                    : q.OrderBy(x => x.CompanyName).ThenBy(x => x.Emp.EmployeeCd),

                "workerid" => desc ? q.OrderByDescending(x => x.Emp.EmployeeCd)
                                    : q.OrderBy(x => x.Emp.EmployeeCd),

                "workername" => desc
                    ? q.OrderByDescending(x => x.Emp.FamilyNameKana).ThenByDescending(x => x.Emp.FirstNameKana).ThenByDescending(x => x.Emp.EmployeeCd)
                    : q.OrderBy(x => x.Emp.FamilyNameKana).ThenBy(x => x.Emp.FirstNameKana).ThenBy(x => x.Emp.EmployeeCd),

                "birth" => desc ? q.OrderByDescending(x => x.Emp.BirthYmd).ThenByDescending(x => x.Emp.EmployeeCd)
                                    : q.OrderBy(x => x.Emp.BirthYmd).ThenBy(x => x.Emp.EmployeeCd),

                "exclude" => desc ? q.OrderByDescending(x => x.Emp.RetireYmd).ThenByDescending(x => x.Emp.EmployeeCd)
                                    : q.OrderBy(x => x.Emp.RetireYmd).ThenBy(x => x.Emp.EmployeeCd),

                _ => q.OrderBy(x => x.Emp.EmployeeCd)
            };
        }

        private static List<AttendeeSearchRow> ApplyRowSort(List<AttendeeSearchRow> rows, string? sort, string? dir)
        {
            var key = (sort ?? "").Trim().ToLowerInvariant();
            var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

            return key switch
            {
                "lastin" => desc ? rows.OrderByDescending(r => r.LastInTime).ThenByDescending(r => r.WorkerId).ToList()
                                  : rows.OrderBy(r => r.LastInTime).ThenBy(r => r.WorkerId).ToList(),

                "lastout" => desc ? rows.OrderByDescending(r => r.LastOutTime).ThenByDescending(r => r.WorkerId).ToList()
                                  : rows.OrderBy(r => r.LastOutTime).ThenBy(r => r.WorkerId).ToList(),

                _ => rows
            };
        }

        private IQueryable<AttendeeJoined> BuildBaseQuery(string kaisaiCd, string? filter)
        {
            var key = (filter ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(kaisaiCd))
                return Enumerable.Empty<AttendeeJoined>().AsQueryable();

            IQueryable<AttendeeJoined> BaseFromTT02()
            {
                var q =
                    from l in _db.EntryExitLogs.AsNoTracking()
                    where l.KaisaiCd == kaisaiCd
                    join e in _db.Employees.AsNoTracking()
                        on l.EmployeeCd equals e.EmployeeCd
                    join c in _db.Cooperates.AsNoTracking()
                        on e.CooperateCd equals c.CooperateCd into gj
                    from c in gj.DefaultIfEmpty()
                    select new AttendeeJoined
                    {
                        Emp = e,
                        CompanyName = c != null ? (c.CompanyName ?? "") : "",
                        CompanyKana = c != null ? (c.CompanyNameKana ?? "") : "",
                        EntryTime = l.EntryTime,
                        ExitTime = l.ExitTime
                    };
                return q;
            }

            IQueryable<AttendeeJoined> BaseFromEmployeeNotInTT02()
            {
                var q =
                    from e in _db.Employees.AsNoTracking()
                    where !_db.EntryExitLogs.AsNoTracking()
                        .Any(l => l.KaisaiCd == kaisaiCd && l.EmployeeCd == e.EmployeeCd)
                    join c in _db.Cooperates.AsNoTracking()
                        on e.CooperateCd equals c.CooperateCd into gj
                    from c in gj.DefaultIfEmpty()
                    select new AttendeeJoined
                    {
                        Emp = e,
                        CompanyName = c != null ? (c.CompanyName ?? "") : "",
                        CompanyKana = c != null ? (c.CompanyNameKana ?? "") : "",
                        EntryTime = null,
                        ExitTime = null
                    };
                return q;
            }

            // ★新仕様
            IQueryable<AttendeeJoined> BothLog()
                => BaseFromTT02().Where(x => !string.IsNullOrEmpty(x.EntryTime) && !string.IsNullOrEmpty(x.ExitTime));

            IQueryable<AttendeeJoined> EntryOnly()
                => BaseFromTT02().Where(x => !string.IsNullOrEmpty(x.EntryTime) && string.IsNullOrEmpty(x.ExitTime));

            IQueryable<AttendeeJoined> MasterOnly()
                => BaseFromEmployeeNotInTT02()
                   .Where(x => string.IsNullOrEmpty(x.Emp.RetireYmd)); // 退職者除外したいなら

            // デフォルトは both_log
            if (string.IsNullOrEmpty(key)) key = "both_log";

            return key switch
            {
                "both_log" => BothLog(),
                "entry_only" => EntryOnly(),
                "master_only" => MasterOnly(),
                _ => BothLog()
            };
        }

        private IQueryable<AttendeeJoined> ApplySearchConditions(
            IQueryable<AttendeeJoined> q,
            string? companyKana,
            string? companyName,
            string? workerName,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? workerId,
            DateTime? birthDate)
        {
            if (!string.IsNullOrWhiteSpace(workerName))
            {
                var key = workerName.Trim();
                // DisplayName に部分一致（想定）
                q = q.Where(x => (x.Emp.DisplayName ?? "").Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(workerKanaFirst))
            {
                var key = NormalizeKanaToWide(workerKanaFirst);
                q = q.Where(x => (x.Emp.FirstNameKana ?? "").Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(workerKanaLast))
            {
                var key = NormalizeKanaToWide(workerKanaLast);
                q = q.Where(x => (x.Emp.FamilyNameKana ?? "").Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(workerId))
            {
                var key = workerId.Trim();
                q = q.Where(x => x.Emp.EmployeeCd.Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(companyName))
            {
                var key = companyName.Trim();
                q = q.Where(x => (x.CompanyName ?? "").Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(companyKana))
            {
                var key = NormalizeKanaToWide(companyKana);
                q = q.Where(x => (x.CompanyKana ?? "").Contains(key));
            }

            if (birthDate.HasValue)
            {
                var ymd = birthDate.Value.ToString("yyyyMMdd");
                q = q.Where(x => x.Emp.BirthYmd == ymd);
            }

            return q;
        }

    }
}
