namespace QRAttendMvc.Models
{
    public class AttendeeSearchRow
    {
        public string CompanyName { get; set; } = string.Empty;
        public string WorkerId { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;

        /*  追加 2026.02.14 Takada */
        public DateTime? BirthDate { get; set; }      // 生年月日
        public DateTime? ExcludeDate { get; set; }    // 名簿対象除外日（=退職日）

        public DateTime? LastInTime { get; set; }
        public DateTime? LastOutTime { get; set; }

        public string RoleType { get; set; } = "名簿";
    }
}
