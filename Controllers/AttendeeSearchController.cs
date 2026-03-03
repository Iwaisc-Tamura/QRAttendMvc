using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
using QRAttendMvc.Services;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;


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

        // =========================
        // GET: /AttendeeSearch/Index
        // =========================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? companyKana,
            string? companyName,
            string? workerKana,
            string? workerName,
            string? workerId,
            string? birthDate,          // ★Viewが文字入力なので string で受ける
            string? filter,             // ★Viewは name="filter"
            string? filterCondition,    // ★別実装の name を吸収
            string? primeOffice,        // ★別実装の name を吸収
            string? includeSecond,      // ★Viewのcheckbox（name="includeSecond"）を吸収
            bool? searched,
            string? sort,
            string? dir)
        {
            // -------------------------
            // セッション：開催コード
            // -------------------------
            ViewBag.CurrentKaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            // -------------------------
            // ログイン情報
            // -------------------------
            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var empCd = HttpContext.Session.GetString("EMPLOYEE_CD") ?? string.Empty;

            ViewBag.LoginText = (!string.IsNullOrEmpty(userBranch) && !string.IsNullOrEmpty(empCd))
                ? $"{userBranch}-{empCd}"
                : "     -     ";

            // -------------------------
            // イベント表示（EventSelectionController で Session に入れた値）
            // 日付は "yyyy/MM/dd (ddd)" に整形（可能なら）
            // -------------------------
            var eventDateStr = HttpContext.Session.GetString("SelectedEventDate") ?? "";
            if (DateTime.TryParse(eventDateStr, out var eventDate))
            {
                var ja = new CultureInfo("ja-JP");
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

            // -------------------------
            // 初回表示は空
            // -------------------------
            var hasSearched = searched.GetValueOrDefault();
            if (!hasSearched)
                return View(Enumerable.Empty<AttendeeSearchRow>());

            // CSV直叩き防止（searched=true を要求）ボタンが押せないから不要
            //if (searched != true)
            //    return BadRequest("検索実行後にCSV出力してください。");

            // -------------------------
            // SPパラメータ整形
            // -------------------------
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd) ?? "";

            // filterは '1','2','3' が来る前提。未指定は '1'
            var filterKey = !string.IsNullOrWhiteSpace(filter)
                ? filter
                : (!string.IsNullOrWhiteSpace(filterCondition)
                    ? filterCondition
                    : "1");

            // 想定外は '1' に丸める（安全策）
            if (filterKey != "1" && filterKey != "2" && filterKey != "3" && filterKey != "4")
            {
                filterKey = "1";
            }

            // ★SPに渡す値はそのまま
            var spFilterCondition = filterKey;

            // primeOffice は Viewの includeSecond からも生成
            // includeSecond は "true"/"on" を想定
            bool includeSecondChecked =
                string.Equals(includeSecond, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(includeSecond, "on", StringComparison.OrdinalIgnoreCase);

            var spPrimeOffice = !string.IsNullOrWhiteSpace(primeOffice)
                ? primeOffice
                : MapPrimeOfficeToSp(includeSecondChecked);

            // 誕生日：yyyyMMdd に正規化（できなければ空）
            var birthYmd = NormalizeBirthDateToYmd8(birthDate) ?? "";

            // workerKana：全角寄せ（必要なら）
            var kana = NormalizeKanaToWide(workerKana ?? "");

            // -------------------------
            // SP実行（DbSet<AttendeeSearchSpRow>）
            // -------------------------
            var list = await _db.AttendeeSearchSpRows
                .FromSqlRaw(
                    "EXEC sp_EntryExit_Inquiry " +
                    "@KAISAI_CD,@COMPANY_NAME_KANA,@COMPANY_NAME,@EMPLOYEE_NAME_KANA,@EMPLOYEE_NAME,@EMPLOYEE_CD,@BIRTH_YMD,@FILTERCONDITION,@PRIMEOFFICE",

                    new SqlParameter("@KAISAI_CD", SqlDbType.Char, 10) { Value = kaisaiCd ?? "" },
                    new SqlParameter("@COMPANY_NAME_KANA", SqlDbType.VarChar, 320) { Value = companyKana ?? "" },
                    new SqlParameter("@COMPANY_NAME", SqlDbType.VarChar, 160) { Value = companyName ?? "" },
                    new SqlParameter("@EMPLOYEE_NAME_KANA", SqlDbType.VarChar, 200) { Value = kana ?? "" },
                    new SqlParameter("@EMPLOYEE_NAME", SqlDbType.VarChar, 120) { Value = (workerName ?? "").Trim() },
                    new SqlParameter("@EMPLOYEE_CD", SqlDbType.VarChar, 10) { Value = workerId ?? "" },
                    new SqlParameter("@BIRTH_YMD", SqlDbType.Char, 8) { Value = birthYmd },
                    new SqlParameter("@FILTERCONDITION", SqlDbType.Char, 1) { Value = spFilterCondition },
                    new SqlParameter("@PRIMEOFFICE", SqlDbType.Char, 1) { Value = spPrimeOffice ?? "9" }
                )
                .AsNoTracking()
                .ToListAsync();

            // -------------------------
            // 表示用 rows へ変換（AttendeeSearchRow.cs は“新規登録版”を使用）
            // -------------------------
            var rows = list.Select(x => new AttendeeSearchRow
            {
                KaisaiCd = x.KAISAI_CD ?? "",
                CooperateCd = x.COOPERATE_CD ?? "",
                CompanyName = x.COMPANY_NAME ?? "",
                CompanyNameKana = x.COMPANY_NAME_KANA ?? "",
                WorkerId = x.EMPLOYEE_CD ?? "",
                WorkerName = x.EMPLOYEE_NAME ?? "",
                WorkerNameKana = x.EMPLOYEE_NAME_KANA ?? "",
                BirthDate = x.BIRTH_YMD,
                LastInTime = x.ENTRY_TIME,
                LastOutTime = x.EXIT_TIME,
                ExcludeDate = x.RETIRE_YMD,
                PrimeOffice = x.PRIMEOFFICE ?? ""
            }).ToList();

            if (rows.Count == 0)
            {
                ViewBag.NoResult = true;
            }

            // -------------------------
            // ソート（SP結果をメモリソート）
            // Viewのヘッダ sort/dir と整合させる
            // -------------------------
            rows = ApplyRowSort(rows, sort, dir);

            ViewBag.ResultCount = rows.Count;
            ViewBag.NoResult = rows.Count == 0;

            return View(rows);
        }

        // =========================
        // GET: /AttendeeSearch/Export
        // =========================
        public async Task<FileResult> Export(
            string? companyKana,
            string? companyName,
            string? workerKana,
            string? workerName,
            string? workerId,
            string? birthDate,
            string? filter,
            string? filterCondition,
            string? primeOffice,
            string? includeSecond,
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

            var filterKey = !string.IsNullOrWhiteSpace(filter)
                ? filter
                : (!string.IsNullOrWhiteSpace(filterCondition)
                    ? filterCondition
                    : "1");

            // 想定外は '1' に丸める（安全策）
            if (filterKey != "1" && filterKey != "2" && filterKey != "3" && filterKey != "4")
            {
                filterKey = "1";
            }

            // ★SPに渡す値はそのまま
            var spFilterCondition = filterKey;

            bool includeSecondChecked =
                string.Equals(includeSecond, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(includeSecond, "on", StringComparison.OrdinalIgnoreCase);

            var spPrimeOffice = !string.IsNullOrWhiteSpace(primeOffice)
                ? primeOffice
                : MapPrimeOfficeToSp(includeSecondChecked);

            var birthYmd = NormalizeBirthDateToYmd8(birthDate) ?? "";
            var kana = NormalizeKanaToWide(workerKana ?? "");

            var list = await _db.AttendeeSearchSpRows
                .FromSqlRaw(
                    "EXEC sp_EntryExit_Inquiry " +
                    "@KAISAI_CD,@COMPANY_NAME_KANA,@COMPANY_NAME,@EMPLOYEE_NAME_KANA,@EMPLOYEE_NAME,@EMPLOYEE_CD,@BIRTH_YMD,@FILTERCONDITION,@PRIMEOFFICE",

                    new SqlParameter("@KAISAI_CD", SqlDbType.Char, 10) { Value = kaisaiCd ?? "" },
                    new SqlParameter("@COMPANY_NAME_KANA", SqlDbType.VarChar, 320) { Value = companyKana ?? "" },
                    new SqlParameter("@COMPANY_NAME", SqlDbType.VarChar, 160) { Value = companyName ?? "" },
                    new SqlParameter("@EMPLOYEE_NAME_KANA", SqlDbType.VarChar, 200) { Value = kana ?? "" },
                    new SqlParameter("@EMPLOYEE_NAME", SqlDbType.VarChar, 120) { Value = (workerName ?? "").Trim() },
                    new SqlParameter("@EMPLOYEE_CD", SqlDbType.VarChar, 10) { Value = workerId ?? "" },
                    new SqlParameter("@BIRTH_YMD", SqlDbType.Char, 8) { Value = birthYmd },
                    new SqlParameter("@FILTERCONDITION", SqlDbType.Char, 1) { Value = spFilterCondition },
                    new SqlParameter("@PRIMEOFFICE", SqlDbType.Char, 1) { Value = spPrimeOffice ?? "9" }
                )
                .AsNoTracking()
                .ToListAsync();

            var rows = list.Select(x => new AttendeeSearchRow
            {
                KaisaiCd = x.KAISAI_CD ?? "",
                CooperateCd = x.COOPERATE_CD ?? "",
                CompanyName = x.COMPANY_NAME ?? "",
                CompanyNameKana = x.COMPANY_NAME_KANA ?? "",
                WorkerId = x.EMPLOYEE_CD ?? "",
                WorkerName = x.EMPLOYEE_NAME ?? "",
                WorkerNameKana = x.EMPLOYEE_NAME_KANA ?? "",
                BirthDate = x.BIRTH_YMD,
                LastInTime = x.ENTRY_TIME,
                LastOutTime = x.EXIT_TIME,
                ExcludeDate = x.RETIRE_YMD,
                PrimeOffice = x.PRIMEOFFICE ?? ""
            }).ToList();

            rows = ApplyRowSort(rows, sort, dir);

            // TSV作成（画面と同じ列構成に寄せる）
            var sb = new StringBuilder();
            sb.AppendLine("会社名,作業員ID,作業員名,生年月日,名簿対象除外日,入場記録,退場記録");

            string Esc(string? v)
            {
                if (string.IsNullOrEmpty(v)) return "";

                var value = v.Replace("\"", "\"\""); // " → ""

                // カンマ・改行・ダブルクォートが含まれる場合は囲む
                if (value.Contains(",") || value.Contains("\n") || value.Contains("\""))
                {
                    value = $"\"{value}\"";
                }

                return value;
            }

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Esc(r.CompanyName),
                    Esc(r.WorkerId),
                    Esc(r.WorkerName),
                    Esc(r.BirthDate),
                    Esc(r.ExcludeDate),
                    Esc(r.LastInTime),
                    Esc(r.LastOutTime)
                }));
            }

            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            var fileName = $"attendee_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        // =====================================================
        // ここから下：共通ユーティリティ
        // =====================================================

        /// <summary>
        /// 画面の checkbox（2次店以下を含む）→ SP用 PRIMEOFFICE へ（暫定）
        /// SPが 0/1/9 等どれを期待しているかに合わせてここだけ差し替えればOK。
        /// 現状は includeSecond=true なら "1"、false なら "0"。
        /// </summary>
        private static string MapPrimeOfficeToSp(bool includeSecond)
        {
            return includeSecond ? "9" : "1";
        }

        /// <summary>
        /// Viewの filter 値（no_log/partial_log/no_log_active 等）を
        /// SPの FILTERCONDITION（char(1)想定）へ変換（暫定）。
        ///
        /// ※SPが "both_log" 等の文字列を受ける設計なら、ここは string に戻して SQLParam型も変えてください。
        /// </summary>

        /// <summary>
        /// "yyyy/mm/dd" / "yyyy-mm-dd" / "yyyymmdd" を "yyyyMMdd" へ正規化
        /// </summary>
        private static string? NormalizeBirthDateToYmd8(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var s = input.Trim();

            // 日本フォーマット限定で解釈
            if (DateTime.TryParseExact(
                    s,
                    new[] { "yyyyMMdd", "yyyy/MM/dd", "yyyy/M/d", "yyyy-MM-dd", "yyyy-M-d" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return dt.ToString("yyyyMMdd");
            }

            return null;
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

        // -------------------------
        // 行ソート（SP結果をメモリで並べ替え）
        // sortキーは View の RouteFor() と合わせる：
        // "company","workerid","workername","birth","exclude","lastin","lastout"
        // -------------------------
        private static List<AttendeeSearchRow> ApplyRowSort(List<AttendeeSearchRow> rows, string? sort, string? dir)
        {
            var key = (sort ?? "").Trim().ToLowerInvariant();
            var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

            // 文字列比較用のキー（nullは空扱い）
            static string S(string? v) => v ?? "";

            // yyyyMMdd の比較を安定させたいので "yyyyMMdd" 以外が来ても比較できるように整形
            static string DateKey(string? ymd)
            {
                if (string.IsNullOrWhiteSpace(ymd)) return "";
                var s = ymd.Trim().Replace("/", "").Replace("-", "");
                return s; // 8桁前提（違っても文字列比較は可能）
            }

            // HH:mm / HHmm の比較を安定させる
            static string TimeKey(string? t)
            {
                if (string.IsNullOrWhiteSpace(t)) return "";
                var s = t.Trim();
                s = s.Replace("：", ":");        // 全角コロン対策
                s = s.Replace(":", "");         // HHmm に寄せる
                return s;
            }

            if (string.IsNullOrEmpty(key)) key = "workerid";

            return key switch
            {
                "company" => desc
                    ? rows.OrderByDescending(r => S(r.CompanyName)).ThenByDescending(r => S(r.WorkerId)).ToList()
                    : rows.OrderBy(r => S(r.CompanyName)).ThenBy(r => S(r.WorkerId)).ToList(),

                "workerid" => desc
                    ? rows.OrderByDescending(r => S(r.WorkerId)).ToList()
                    : rows.OrderBy(r => S(r.WorkerId)).ToList(),

                "workername" => desc
                    ? rows.OrderByDescending(r => S(r.WorkerNameKana)).ThenByDescending(r => S(r.WorkerId)).ToList()
                    : rows.OrderBy(r => S(r.WorkerNameKana)).ThenBy(r => S(r.WorkerId)).ToList(),

                "birth" => desc
                    ? rows.OrderByDescending(r => DateKey(r.BirthDate)).ThenByDescending(r => S(r.WorkerId)).ToList()
                    : rows.OrderBy(r => DateKey(r.BirthDate)).ThenBy(r => S(r.WorkerId)).ToList(),

                "exclude" => desc
                    ? rows.OrderByDescending(r => DateKey(r.ExcludeDate)).ThenByDescending(r => S(r.WorkerId)).ToList()
                    : rows.OrderBy(r => DateKey(r.ExcludeDate)).ThenBy(r => S(r.WorkerId)).ToList(),

                "lastin" => desc
                    ? rows.OrderByDescending(r => TimeKey(r.LastInTime)).ThenByDescending(r => S(r.WorkerId)).ToList()
                    : rows.OrderBy(r => TimeKey(r.LastInTime)).ThenBy(r => S(r.WorkerId)).ToList(),

                "lastout" => desc
                    ? rows.OrderByDescending(r => TimeKey(r.LastOutTime)).ThenByDescending(r => S(r.WorkerId)).ToList()
                    : rows.OrderBy(r => TimeKey(r.LastOutTime)).ThenBy(r => S(r.WorkerId)).ToList(),

                _ => rows
            };
        }
    }
}