using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using QRAttendMvc.Services;


namespace QRAttendMvc.Controllers
{
    public class CooperateSearchController : BaseController
    {
        private readonly AppDbContext _db;

        public CooperateSearchController(IActionLogService logService, AppDbContext db)
            : base(logService)
        {
            _db = db;
        }

        /// <summary>
        /// 会社検索画面（会社名カナのみ）
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            string? companyKana,
            string? returnUrl,
            bool? searched,
            string? sort,
            string? dir)
        {
            // ログイン表示（既存の Session に合わせて）
            var userBranch = HttpContext.Session.GetString("BRANCH_CD") ?? string.Empty;
            var empCd = HttpContext.Session.GetString("EMPLOYEE_CD") ?? string.Empty;

            ViewBag.LoginText = (!string.IsNullOrEmpty(userBranch) && !string.IsNullOrEmpty(empCd))
                ? $"{userBranch}-{empCd}"
                : "     -     ";

            // 初回表示は空（EmployeeSearch と同様の振る舞い）
            var hasSearched = searched.GetValueOrDefault();
            if (!hasSearched)
            {
                return View(Enumerable.Empty<CooperateSearchRow>());
            }

            // 検索
            var rows = await SearchAsync(companyKana, sort, dir);
            return View(rows);
        }

        /// <summary>
        /// 「入退場登録に戻る」押下時：選択CDをセッションへ格納し returnUrl に戻す
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Back(string? selectedCooperateCd, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                // returnUrl が無い場合の保険（プロジェクトに合わせて変更可）
                returnUrl = Url.Action("Index", "TempInput") ?? "/";
            }

            var cd = (selectedCooperateCd ?? "").Trim();

            // 要求仕様：セッションで受け渡し
            HttpContext.Session.SetString("COOPERATE_CD", cd);

            // ついでに QueryString でも渡す（TempInput 側で扱いやすい）
            var backUrl = AppendQuery(returnUrl, "COOPERATE_CD", cd);

            return Redirect(backUrl);
        }

        // ===== 検索本体（GM02_COOPERATE を参照。住所列を合成表示するためSQLで取得） =====

        private async Task<List<CooperateSearchRow>> SearchAsync(string? companyKana, string? sort, string? dir)
        {
            // sort のホワイトリスト（SQLインジェクション対策）
            string orderBy = BuildOrderBy(sort, dir);

            var sql = new StringBuilder();
            sql.AppendLine(@"
SELECT
    COOPERATE_CD,
    COMPANY_NAME,
    COMPANY_NAME_KANA,
    POSTAL_CD,
    PREFECTURE,
    CITY,
    ADDRESS_1,
    ADDRESS_2,
    APPLY_S_YMD,
    APPLY_E_YMD
FROM GM02_COOPERATE
WHERE 1=1
");

            var parameters = new List<(string Name, object Value)>();

            // 追加条件：検索条件は「会社名（カナ）のみ」
            if (!string.IsNullOrWhiteSpace(companyKana))
            {
                var key = NormalizeKanaToWide(companyKana);
                sql.AppendLine("AND COMPANY_NAME_KANA LIKE @p_companyKana");
                parameters.Add(("@p_companyKana", $"%{key}%"));
            }

            sql.AppendLine(orderBy);

            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql.ToString();

            foreach (var (Name, Value) in parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = Name;
                p.Value = Value;
                cmd.Parameters.Add(p);
            }

            var list = new List<CooperateSearchRow>();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string GetString(string col)
                {
                    var i = reader.GetOrdinal(col);
                    return reader.IsDBNull(i) ? "" : reader.GetString(i).Trim();
                }

                list.Add(new CooperateSearchRow
                {
                    CooperateCd = GetString("COOPERATE_CD"),
                    CompanyName = GetString("COMPANY_NAME"),
                    CompanyNameKana = GetString("COMPANY_NAME_KANA"),
                    PostalCd = GetString("POSTAL_CD"),
                    Prefecture = GetString("PREFECTURE"),
                    City = GetString("CITY"),
                    Address1 = GetString("ADDRESS_1"),
                    Address2 = GetString("ADDRESS_2"),
                    ApplySYmd = GetString("APPLY_S_YMD"),
                    ApplyEYmd = GetString("APPLY_E_YMD"),
                });
            }

            return list;
        }

        private static string BuildOrderBy(string? sort, string? dir)
        {
            var key = (sort ?? "").Trim().ToLowerInvariant();
            var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(key)) key = "cooperatecd";

            string col = key switch
            {
                "cooperatecd" => "COOPERATE_CD",
                "companyname" => "COMPANY_NAME",
                "companykana" => "COMPANY_NAME_KANA",
                "address" => "PREFECTURE",
                _ => "COOPERATE_CD"
            };

            var primary = $"{col} {(desc ? "DESC" : "ASC")}";

            // primary が COOPERATE_CD の場合は安定ソート用の付加をしない
            if (string.Equals(col, "COOPERATE_CD", StringComparison.OrdinalIgnoreCase))
                return $"ORDER BY {primary}";

            return $"ORDER BY {primary}, COOPERATE_CD ASC";
        }

        // ViewModel（Controller内）
        public class CooperateSearchRow
        {
            public string CooperateCd { get; set; } = "";
            public string CompanyName { get; set; } = "";
            public string CompanyNameKana { get; set; } = "";
            public string PostalCd { get; set; } = "";
            public string Prefecture { get; set; } = "";
            public string City { get; set; } = "";
            public string Address1 { get; set; } = "";
            public string Address2 { get; set; } = "";
            public string ApplySYmd { get; set; } = "";
            public string ApplyEYmd { get; set; } = "";

            public string Address => $"{Prefecture}{City}{Address1}{Address2}".Trim();
        }

        // カナ入力の揺れ吸収（全角/半角・スペースなど）
        private static string NormalizeKanaToWide(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            s = s.Trim();
            s = s.Replace('\u3000', ' ');
            s = Regex.Replace(s, @"\s+", " ");
            s = s.Normalize(NormalizationForm.FormKC);

            return s;
        }

        private static string AppendQuery(string url, string key, string value)
        {
            try
            {
                var baseUri = new Uri(url, UriKind.RelativeOrAbsolute);

                // 相対URL対策：ダミーoriginを付与
                Uri abs = baseUri.IsAbsoluteUri
                    ? baseUri
                    : new Uri(new Uri("http://dummy.local"), baseUri);

                var ub = new UriBuilder(abs);
                var q = System.Web.HttpUtility.ParseQueryString(ub.Query);
                q[key] = value;
                ub.Query = q.ToString() ?? "";

                // 元が相対なら相対で返す
                if (!baseUri.IsAbsoluteUri)
                    return ub.Uri.PathAndQuery;

                return ub.Uri.ToString();
            }
            catch
            {
                return url;
            }
        }
    }
}