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
            string? sEmployeeKanas = null,
            string? sEmployeeKanan = null,
            string? sEmployeeKanjis = null,
            string? sEmployeeKanjin = null,
            string? sBirthYmd = null,
            string? sEmployeeCd = null,
            string? sSelect = null,
            int? jStrat = null,
            int? jMaisu = null,
            string? tResart = null,
            string? uTantoCd = null,
            DateTime? uTimeStamp = null
        )
        {
            var cs = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            var sql = @"
INSERT INTO dbo.TX01_LOG (
  SCREEN_ID, ACTION_CD, EVENT_CD, EMPLOYEE_CD, COOPERATE_CD,
  FAMILY_NAME, FIRST_NAME, BIRTH_YMD, ENTRY_TIME, EXIT_TIME,
  REASON_CD, S_COOPERATE_KANA, S_COOPERATE_NAME,
  S_EMPLOYEE_KANAS, S_EMPLOYEE_KANAN, S_EMPLOYEE_KANJIS, S_EMPLOYEE_KANJIN,
  S_BIRTH_YMD, S_EMPLOYEE_CD, S_SELECT,
  J_STRAT, J_MAISU, T_RESART,
  U_TANTO_CD, U_TIME_STAMP
)
VALUES (
  @SCREEN_ID, @ACTION_CD, @EVENT_CD, @EMPLOYEE_CD, @COOPERATE_CD,
  @FAMILY_NAME, @FIRST_NAME, @BIRTH_YMD, @ENTRY_TIME, @EXIT_TIME,
  @REASON_CD, @S_COOPERATE_KANA, @S_COOPERATE_NAME,
  @S_EMPLOYEE_KANAS, @S_EMPLOYEE_KANAN, @S_EMPLOYEE_KANJIS, @S_EMPLOYEE_KANJIN,
  @S_BIRTH_YMD, @S_EMPLOYEE_CD, @S_SELECT,
  @J_STRAT, @J_MAISU, @T_RESART,
  @U_TANTO_CD, @U_TIME_STAMP
)";

            using var cmd = new SqlCommand(sql, conn);

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

            cmd.Parameters.Add("@S_EMPLOYEE_KANAS", SqlDbType.NVarChar, 100).Value = (object?)sEmployeeKanas ?? DBNull.Value;
            cmd.Parameters.Add("@S_EMPLOYEE_KANAN", SqlDbType.NVarChar, 100).Value = (object?)sEmployeeKanan ?? DBNull.Value;
            cmd.Parameters.Add("@S_EMPLOYEE_KANJIS", SqlDbType.NVarChar, 100).Value = (object?)sEmployeeKanjis ?? DBNull.Value;
            cmd.Parameters.Add("@S_EMPLOYEE_KANJIN", SqlDbType.NVarChar, 100).Value = (object?)sEmployeeKanjin ?? DBNull.Value;

            cmd.Parameters.Add("@S_BIRTH_YMD", SqlDbType.Char, 8).Value = (object?)sBirthYmd ?? DBNull.Value;
            cmd.Parameters.Add("@S_EMPLOYEE_CD", SqlDbType.Char, 10).Value = (object?)sEmployeeCd ?? DBNull.Value;

            cmd.Parameters.Add("@S_SELECT", SqlDbType.Char, 3).Value = (object?)sSelect ?? DBNull.Value;

            cmd.Parameters.Add("@J_STRAT", SqlDbType.Decimal).Value = (object?)jStrat ?? DBNull.Value;
            cmd.Parameters["@J_STRAT"].Precision = 3;
            cmd.Parameters["@J_STRAT"].Scale = 0;

            cmd.Parameters.Add("@J_MAISU", SqlDbType.Decimal).Value = (object?)jMaisu ?? DBNull.Value;
            cmd.Parameters["@J_MAISU"].Precision = 3;
            cmd.Parameters["@J_MAISU"].Scale = 0;

            cmd.Parameters.Add("@T_RESART", SqlDbType.Char, 3).Value = (object?)tResart ?? DBNull.Value;

            cmd.Parameters.Add("@U_TANTO_CD", SqlDbType.Char, 5).Value = (object?)uTantoCd ?? DBNull.Value;
            cmd.Parameters.Add("@U_TIME_STAMP", SqlDbType.DateTime).Value = (object?)(uTimeStamp ?? DateTime.Now) ?? DBNull.Value;

            await cmd.ExecuteNonQueryAsync();
        }
    }
}