using System.ComponentModel.DataAnnotations;

namespace QRAttendMvc.Models
{
    /// <summary>
    /// 入退場データ（TT02_ENTRY_EXIT）
    /// PK：KAISAI_CD + EMPLOYEE_CD
    /// </summary>
    public class Tt02EntryExit
    {
        [Key]
        public string KaisaiCd { get; set; } = "";      // KAISAI_CD

        [Key]
        public string EmployeeCd { get; set; } = "";    // EMPLOYEE_CD

        public string? CooperateCd { get; set; }
        public string? CompanyName { get; set; }
        public string? FamilyName { get; set; }
        public string? FirstName { get; set; }
        public string? FamilyNameKana { get; set; }
        public string? FirstNameKana { get; set; }
        public string? BirthYmd { get; set; }
        public string? Type { get; set; }

        /// <summary>入場時刻（HHmm）</summary>
        public string? EntryTime { get; set; }

        /// <summary>退場時刻（HHmm）</summary>
        public string? ExitTime { get; set; }

        public string? ActionCd { get; set; }
        public string? TensoFlg { get; set; }
        public string? TensoYmdTime { get; set; }
    }
}
