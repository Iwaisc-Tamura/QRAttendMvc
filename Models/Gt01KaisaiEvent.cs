using System.ComponentModel.DataAnnotations;

namespace QRAttendMvc.Models
{
    /// <summary>
    /// 開催イベント（GT01_KAISAI_EVENT）
    /// </summary>
    public class Gt01KaisaiEvent
    {
        [Key]
        public string KaisaiCd { get; set; } = string.Empty;   // KAISAI_CD
        public string? EventKbn { get; set; }                  // EVENT_KBN
        public string? PartnerCd { get; set; }                 // PARTNER_CD
        public string EventCd { get; set; } = string.Empty;    // EVENT_CD
        public string EventName { get; set; } = string.Empty;  // EVENT_NAME
        public int? Nendo { get; set; }                        // NENDO
        public string KaisaiYmd { get; set; } = string.Empty;  // KAISAI_YMD (YYYYMMDD)
        public string BranchCd { get; set; } = string.Empty;   // BRANCH_CD
        public string? BranchName { get; set; }                // BRANCH_NAME
        public string? Location { get; set; }                  // LOCATION
        public string? ReceptTime { get; set; }                // RECEPT_TIME (HHmm)
        public string? StartTime { get; set; }                 // START_TIME (HHmm)
        public string? EndTime { get; set; }                   // END_TIME (HHmm)
        public string? TrKbn { get; set; }                     // TR_KBN (1/2/3)
        public string? QrKbn { get; set; }                     // QR_KBN
        public string? GroupCd { get; set; }                   // GROUP_CD
    }
}
