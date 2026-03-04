using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Threading.Tasks;

namespace QRAttendMvc.Services
{
    public class ActionLogService : IActionLogService
    {
        private readonly IConfiguration _configuration;

        public ActionLogService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task ActionLogSaveAsync(
            string screenId,
            string actionCd,
            string? eventCd = null,
            string? employeeCd = null,
            string? cooperateCd = null,
            string? familyName = null,
            string? firstName = null,
            string? birthYmd = null,
            string? entryTime = null,
            string? exitTime = null,
            string? reasonCd = null,
            string? sCooperateKana = null,
            string? sCooperateName = null,
            string? sEmployeeKana = null,
            string? sEmployeeKanji = null,
            string? sBirthYmd = null,
            string? sEmployeeCd = null,
            string? sSelect = null,
            string? tResult = null,
            string? uTantoCd = null,
            DateTime? uTimeStamp = null
        )
        {
            var cs = _configuration.GetConnectionString("DefaultConnection");

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            var sql = @"
INSERT INTO dbo.TX01_LOG (
  SCREEN_ID, ACTION_CD, EVENT_CD, EMPLOYEE_CD, COOPERATE_CD,
  FAMILY_NAME, FIRST_NAME, BIRTH_YMD, ENTRY_TIME, EXIT_TIME,
  REASON_CD,
  S_COOPERATE_KANA, S_COOPERATE_NAME,
  S_EMPLOYEE_KANA, S_EMPLOYEE_KANJI,
  S_BIRTH_YMD, S_EMPLOYEE_CD, S_SELECT,
  T_RESULT,
  U_TANTO_CD, U_TIME_STAMP
)
VALUES (
  @SCREEN_ID, @ACTION_CD, @EVENT_CD, @EMPLOYEE_CD, @COOPERATE_CD,
  @FAMILY_NAME, @FIRST_NAME, @BIRTH_YMD, @ENTRY_TIME, @EXIT_TIME,
  @REASON_CD,
  @S_COOPERATE_KANA, @S_COOPERATE_NAME,
  @S_EMPLOYEE_KANA, @S_EMPLOYEE_KANJI,
  @S_BIRTH_YMD, @S_EMPLOYEE_CD, @S_SELECT,
  @T_RESULT,
  @U_TANTO_CD, @U_TIME_STAMP
);";

            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@SCREEN_ID", SqlDbType.Char, 3).Value = (object?)screenId ?? DBNull.Value;
            cmd.Parameters.Add("@ACTION_CD", SqlDbType.Char, 3).Value = (object?)actionCd ?? DBNull.Value;
            cmd.Parameters.Add("@EVENT_CD", SqlDbType.Char, 10).Value = (object?)eventCd ?? DBNull.Value;
            cmd.Parameters.Add("@EMPLOYEE_CD", SqlDbType.Char, 10).Value = (object?)employeeCd ?? DBNull.Value;
            cmd.Parameters.Add("@COOPERATE_CD", SqlDbType.Char, 10).Value = (object?)cooperateCd ?? DBNull.Value;

            cmd.Parameters.Add("@FAMILY_NAME", SqlDbType.NVarChar, 100).Value = (object?)familyName ?? DBNull.Value;
            cmd.Parameters.Add("@FIRST_NAME", SqlDbType.NVarChar, 100).Value = (object?)firstName ?? DBNull.Value;

            cmd.Parameters.Add("@BIRTH_YMD", SqlDbType.Char, 8).Value = (object?)birthYmd ?? DBNull.Value;
            cmd.Parameters.Add("@ENTRY_TIME", SqlDbType.Char, 4).Value = (object?)entryTime ?? DBNull.Value;
            cmd.Parameters.Add("@EXIT_TIME", SqlDbType.Char, 4).Value = (object?)exitTime ?? DBNull.Value;

            cmd.Parameters.Add("@REASON_CD", SqlDbType.Char, 2).Value = (object?)reasonCd ?? DBNull.Value;

            cmd.Parameters.Add("@S_COOPERATE_KANA", SqlDbType.NVarChar, 100).Value = (object?)sCooperateKana ?? DBNull.Value;
            cmd.Parameters.Add("@S_COOPERATE_NAME", SqlDbType.NVarChar, 100).Value = (object?)sCooperateName ?? DBNull.Value;

            cmd.Parameters.Add("@S_EMPLOYEE_KANA", SqlDbType.NVarChar, 100).Value = (object?)sEmployeeKana ?? DBNull.Value;
            cmd.Parameters.Add("@S_EMPLOYEE_KANJI", SqlDbType.NVarChar, 100).Value = (object?)sEmployeeKanji ?? DBNull.Value;

            cmd.Parameters.Add("@S_BIRTH_YMD", SqlDbType.Char, 8).Value = (object?)sBirthYmd ?? DBNull.Value;
            cmd.Parameters.Add("@S_EMPLOYEE_CD", SqlDbType.Char, 10).Value = (object?)sEmployeeCd ?? DBNull.Value;

            cmd.Parameters.Add("@S_SELECT", SqlDbType.Char, 3).Value = (object?)sSelect ?? DBNull.Value;

            cmd.Parameters.Add("@T_RESULT", SqlDbType.Char, 3).Value = (object?)tResult ?? DBNull.Value;

            cmd.Parameters.Add("@U_TANTO_CD", SqlDbType.Char, 5).Value = (object?)uTantoCd ?? DBNull.Value;
            cmd.Parameters.Add("@U_TIME_STAMP", SqlDbType.DateTime).Value = (object?)(uTimeStamp ?? DateTime.Now) ?? DBNull.Value;

            await cmd.ExecuteNonQueryAsync();
        }
    }
}