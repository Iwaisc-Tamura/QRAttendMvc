using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
using QRAttendMvc.Services;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
});

/*　追加 2026.02.19 Takada strat*/
builder.Services.AddScoped<IActionLogService, ActionLogService>();
/*　追加 2026.02.19 Takada end  */

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

// ★ デバッグ：URLパラメータ p1=支店コード(5桁), p2=社員コード(5桁) をセッションへ反映（localhostのみ）
//    - パラメータが無い場合は appsettings.Development.json の DebugLogin を使用
//    - 桁数/形式が不正な場合はセッションを設定しない（ガード）
app.Use(async (context, next) =>
{
        static bool Is5Digits(string s) => s.Length == 5 && s.All(char.IsDigit);
        static bool Is5AlphaNumeric(string s) => s.Length == 5 && s.All(char.IsLetterOrDigit);

        var p1 = context.Request.Query["p1"].ToString();
        var p2 = context.Request.Query["p2"].ToString();

        // URL パラメータ優先
        if (!string.IsNullOrEmpty(p1) || !string.IsNullOrEmpty(p2))
        {

            if (Is5Digits(p1) && Is5AlphaNumeric(p2))
            {
                context.Session.SetString("BRANCH_CD", p1);
                context.Session.SetString("EMPLOYEE_CD", p2);
                context.Session.Remove("DEBUG_LOGIN_ERROR");
            }
            else
            {
                // 片方でも不正なら設定しない（業務向けガード）
                context.Session.Remove("BRANCH_CD");
                context.Session.Remove("EMPLOYEE_CD");
                context.Session.SetString("DEBUG_LOGIN_ERROR", "URLパラメータ(p1/p2)が不正です。支店コードは5桁数字、社員コードは5桁英数字で指定してください。");
            }
        }
        else if (context.Request.Host.Host == "localhost")
        {
            // 開発時の初期値（appsettings.Development.json）
            var dbgBranch = app.Configuration["DebugLogin:BranchCd"] ?? "";
            var dbgEmp = app.Configuration["DebugLogin:EmployeeCd"] ?? "";
            if (Is5Digits(dbgBranch) && Is5AlphaNumeric(dbgEmp))
            {
                context.Session.SetString("BRANCH_CD", dbgBranch);
                context.Session.SetString("EMPLOYEE_CD", dbgEmp);
            }
        }
        else
        {
            var sessionBranch = context.Session.GetString("BRANCH_CD") ?? "";
            var sessionEmp = context.Session.GetString("EMPLOYEE_CD") ?? "";
            if (string.IsNullOrWhiteSpace(sessionBranch) || string.IsNullOrWhiteSpace(sessionEmp))
            {
                var path = context.Request.Path;
                var isHome = path == "/" || path.StartsWithSegments("/Home", StringComparison.OrdinalIgnoreCase);
                if (isHome)
                {
                    context.Session.SetString("DEBUG_LOGIN_ERROR", "URLパラメータ(p1/p2)が指定されていません。支店コードと社員コードを指定してください。");
                }
            }
            else
            {
                context.Session.Remove("DEBUG_LOGIN_ERROR");
            }
        }
    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();