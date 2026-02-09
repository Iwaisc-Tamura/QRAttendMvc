
using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
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
//    - 桁数/数字が不正な場合はセッションを設定しない（ガード）
app.Use(async (context, next) =>
{
    if (context.Request.Host.Host == "localhost")
    {
        static bool Is5Digits(string s) => s.Length == 5 && s.All(char.IsDigit);

        var p1 = context.Request.Query["p1"].ToString();
        var p2 = context.Request.Query["p2"].ToString();

        // URL パラメータ優先
        if (!string.IsNullOrEmpty(p1) || !string.IsNullOrEmpty(p2))
        {
            if (Is5Digits(p1) && Is5Digits(p2))
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
                context.Session.SetString("DEBUG_LOGIN_ERROR", "URLパラメータ(p1/p2)が不正です。支店コード・社員コードは各5桁数字で指定してください。");
            }
        }
        else if (app.Environment.IsDevelopment())
        {
            // 開発時の初期値（appsettings.Development.json）
            var dbgBranch = app.Configuration["DebugLogin:BranchCd"] ?? "";
            var dbgEmp = app.Configuration["DebugLogin:EmployeeCd"] ?? "";
            if (Is5Digits(dbgBranch) && Is5Digits(dbgEmp))
            {
                context.Session.SetString("BRANCH_CD", dbgBranch);
                context.Session.SetString("EMPLOYEE_CD", dbgEmp);
            }
        }
    }
    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();