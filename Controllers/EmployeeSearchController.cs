using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
using QRAttendMvc.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace QRAttendMvc.Controllers
{
    public class EmployeeSearchController : BaseController
    {
        private readonly AppDbContext _db;

        public EmployeeSearchController(IActionLogService logService, AppDbContext db)
            : base(logService)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? employeeCd,
            string? companyKana,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? birthDate,
            bool includeExpired,
            string? returnUrl,
            bool? searched,
            string? sort,
            string? dir)
        {
            // ログイン表示（Attendeeと同等）
            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var empCd = HttpContext.Session.GetString("EMPLOYEE_CD") ?? string.Empty;

            ViewBag.LoginText = (!string.IsNullOrEmpty(userBranch) && !string.IsNullOrEmpty(empCd))
                ? $"{userBranch}-{empCd}"
                : "     -     ";

            // サーバ現在日時（表示用）
            ViewBag.ServerNow = DateTime.Now;

            // 検索条件保持
            ViewBag.EmployeeCd = employeeCd ?? "";
            ViewBag.CompanyKana = companyKana ?? "";
            ViewBag.WorkerKanaLast = workerKanaLast ?? "";
            ViewBag.WorkerKanaFirst = workerKanaFirst ?? "";
            ViewBag.BirthDate = birthDate ?? "";
            ViewBag.IncludeExpired = includeExpired;
            ViewBag.ReturnUrl = returnUrl ?? "";

            var hasSearched = searched.GetValueOrDefault();

            // 初回表示は空リスト
            if (!hasSearched)
            {
                return View(Enumerable.Empty<EmployeeSearchRow>());
            }

            // 検索（未入力なら全件）
            var q = BuildEmployeeQuery(employeeCd, companyKana, workerKanaLast, workerKanaFirst, birthDate, includeExpired);

            // ソート適用
            q = ApplySort(q, sort, dir);

            var list = await q.ToListAsync();

            var rows = new List<EmployeeSearchRow>();

            foreach (var x in list)
            {
                var p = x.Emp;

                rows.Add(new EmployeeSearchRow
                {
                    EmployeeCd = p.EmployeeCd ?? "",
                    CooperateCd = p.CooperateCd ?? "",

                    CompanyName = x.CompanyName ?? "",
                    CompanyKana = x.CompanyKana ?? "",

                    FamilyName = p.FamilyName ?? "",
                    FirstName = p.FirstName ?? "",
                    FamilyNameKana = p.FamilyNameKana ?? "",
                    FirstNameKana = p.FirstNameKana ?? "",

                    BirthDate = ParseYyyyMMdd(p.BirthYmd),
                    ExcludeDate = ParseYyyyMMdd(p.RetireYmd),
                });
            }

            return View(rows);
        }

        [HttpPost]
        public IActionResult Select(string employeeCd, string cooperateCd, string returnUrl)
        {
            // TempInput側で使うのでセッションに保存（必要なものだけでOK）
            HttpContext.Session.SetString("TEMP_WORKER_CD", employeeCd ?? "");
            HttpContext.Session.SetString("TEMP_COOPERATE_CD", cooperateCd ?? "");

            // returnUrl があればそこへ戻る
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // 保険：returnUrlが無い/危険なURLなら既定に戻す
            return RedirectToAction("TempInput", "Scan", new { kind = "IN" });
        }

        private IQueryable<EmployeeJoined> BuildEmployeeQuery(
            string? employeeCd,
            string? companyKana,
            string? workerKanaLast,
            string? workerKanaFirst,
            string? birthDate,
            bool includeExpired)
        {
            // LEFT JOIN
            var q =
                from e in _db.Employees.AsNoTracking()
                join c in _db.Cooperates.AsNoTracking()
                    on e.CooperateCd equals c.CooperateCd into gj
                from c in gj.DefaultIfEmpty()
                select new EmployeeJoined
                {
                    Emp = e,
                    CompanyName = c != null ? (c.CompanyName ?? "") : "",
                    CompanyKana = c != null ? (c.CompanyNameKana ?? "") : "",
                    ApplyEYmd = c != null ? (c.ApplyEYmd ?? "") : ""
                };

            // 適用終了を含まない
            if (!includeExpired)
            {
                var today = DateTime.Today.ToString("yyyyMMdd");

                q = q.Where(x =>
                    (
                        x.Emp.RetireYmd == null ||
                        x.Emp.RetireYmd == "" ||
                        x.Emp.RetireYmd.CompareTo(today) >= 0
                    )
                    &&
                    (
                        x.ApplyEYmd == null ||
                        x.ApplyEYmd == "" ||
                        x.ApplyEYmd.CompareTo(today) >= 0
                    )
                );
            }

            if (!string.IsNullOrWhiteSpace(employeeCd))
            {
                var key = employeeCd.Trim();
                q = q.Where(x => (x.Emp.EmployeeCd ?? "").Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(companyKana))
            {
                var key = NormalizeKanaToWide(companyKana);
                q = q.Where(x => (x.CompanyKana ?? "").Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(workerKanaLast))
            {
                var key = NormalizeKanaToWide(workerKanaLast);
                q = q.Where(x => (x.Emp.FamilyNameKana ?? "").Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(workerKanaFirst))
            {
                var key = NormalizeKanaToWide(workerKanaFirst);
                q = q.Where(x => (x.Emp.FirstNameKana ?? "").Contains(key));
            }

            if (!string.IsNullOrWhiteSpace(birthDate))
            {
                var s = birthDate.Trim()
                                 .Replace("/", "")
                                 .Replace("-", "");

                if (Regex.IsMatch(s, @"^\d{8}$"))
                {
                    q = q.Where(x => x.Emp.BirthYmd == s);
                }
            }

            return q;
        }

        // DTO（Controller内）
        private class EmployeeJoined
        {
            public Gm01Employee Emp { get; set; } = default!;
            public string CompanyName { get; set; } = "";
            public string CompanyKana { get; set; } = "";
            public string ApplyEYmd { get; set; } = "";
        }

        public class EmployeeSearchRow
        {
            public string EmployeeCd { get; set; } = "";
            public string CooperateCd { get; set; } = "";

            public string CompanyName { get; set; } = "";
            public string CompanyKana { get; set; } = "";

            public string FamilyName { get; set; } = "";
            public string FirstName { get; set; } = "";
            public string FamilyNameKana { get; set; } = "";
            public string FirstNameKana { get; set; } = "";

            public DateTime? BirthDate { get; set; }
            public DateTime? ExcludeDate { get; set; }

            public string WorkerName => $"{FamilyName} {FirstName}".Trim();
            public string WorkerNameKana => $"{FamilyNameKana} {FirstNameKana}".Trim();
        }

        /* Attendeeと同じ */
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

            s = s.Trim();
            s = s.Replace('\u3000', ' ');
            s = Regex.Replace(s, @"\s+", " ");
            s = s.Normalize(NormalizationForm.FormKC);

            return s;
        }

        private static IQueryable<EmployeeJoined> ApplySort(IQueryable<EmployeeJoined> q, string? sort, string? dir)
        {
            var key = (sort ?? "").Trim().ToLowerInvariant();
            var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(key)) key = "employeecd";

            return key switch
            {
                "employeecd" => desc ? q.OrderByDescending(x => x.Emp.EmployeeCd)
                                     : q.OrderBy(x => x.Emp.EmployeeCd),

                "companyname" => desc ? q.OrderByDescending(x => x.CompanyName).ThenByDescending(x => x.Emp.EmployeeCd)
                                      : q.OrderBy(x => x.CompanyName).ThenBy(x => x.Emp.EmployeeCd),

                "companykana" => desc ? q.OrderByDescending(x => x.CompanyKana).ThenByDescending(x => x.Emp.EmployeeCd)
                                      : q.OrderBy(x => x.CompanyKana).ThenBy(x => x.Emp.EmployeeCd),

                "workername" => desc
                    ? q.OrderByDescending(x => x.Emp.FamilyName).ThenByDescending(x => x.Emp.FirstName).ThenByDescending(x => x.Emp.EmployeeCd)
                    : q.OrderBy(x => x.Emp.FamilyName).ThenBy(x => x.Emp.FirstName).ThenBy(x => x.Emp.EmployeeCd),

                "workernamekana" => desc
                    ? q.OrderByDescending(x => x.Emp.FamilyNameKana).ThenByDescending(x => x.Emp.FirstNameKana).ThenByDescending(x => x.Emp.EmployeeCd)
                    : q.OrderBy(x => x.Emp.FamilyNameKana).ThenBy(x => x.Emp.FirstNameKana).ThenBy(x => x.Emp.EmployeeCd),

                "birth" => desc ? q.OrderByDescending(x => x.Emp.BirthYmd).ThenByDescending(x => x.Emp.EmployeeCd)
                                : q.OrderBy(x => x.Emp.BirthYmd).ThenBy(x => x.Emp.EmployeeCd),

                "exclude" => desc ? q.OrderByDescending(x => x.Emp.RetireYmd).ThenByDescending(x => x.Emp.EmployeeCd)
                                  : q.OrderBy(x => x.Emp.RetireYmd).ThenBy(x => x.Emp.EmployeeCd),

                _ => q.OrderBy(x => x.Emp.EmployeeCd)
            };
        }
    }
}