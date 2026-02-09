
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;

namespace QRAttendMvc.Controllers
{
    public class TestUtilityController : Controller
    {
        private readonly IConfiguration _configuration;

        public TestUtilityController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public IActionResult UpdateTodayEventDate()
        {
            var pcName = Environment.MachineName;

            using (var conn = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                var updateSql = @"
UPDATE [dbo].[GT01_KAISAI_EVENT]
SET [KAISAI_YMD] = CONVERT(char(10), GETDATE(), 111)";
                using (var cmd = new SqlCommand(updateSql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                var logSql = @"
INSERT INTO TX99_TEST_EXEC_LOG
    (EXEC_PC_NAME, EXEC_ACTION)
VALUES
    (@PC_NAME, @ACTION)";
                using (var cmd = new SqlCommand(logSql, conn))
                {
                    cmd.Parameters.AddWithValue("@PC_NAME", pcName);
                    cmd.Parameters.AddWithValue("@ACTION", "開催日を本日に更新（テスト用）");
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["Message"] = $"【テスト】開催日を更新しました（実行PC: {pcName}）";
            return RedirectToAction("Index", "Home");
        }
    }
}
