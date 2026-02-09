namespace QRAttendMvc.Models
{
    public class AttendeeSearchRow
    {
        public string CompanyName { get; set; } = string.Empty;
        public string WorkerId { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;

        public DateTime? LastInTime { get; set; }
        public DateTime? LastOutTime { get; set; }

        public string RoleType { get; set; } = "名簿";
    }
}
