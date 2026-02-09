using System.ComponentModel.DataAnnotations;

namespace QRAttendMvc.Models
{
    /// <summary>
    /// 対象イベント（TT01_TARGET_EVENT）
    /// 仕様：BRANCH_CD + KAISAI_CD がPK。選択日時を保持。
    /// </summary>
    public class TargetEvent
    {
        [Key]
        public string BranchCd { get; set; } = "";      // BRANCH_CD (CHAR(5))

        [Key]
        public string KaisaiCd { get; set; } = "";      // KAISAI_CD (CHAR(10))

        public string? SelectYmdTime { get; set; }       // SELECT_YMD_TIME (CHAR(15)) yyyyMMdd-hhmmss
    }
}
