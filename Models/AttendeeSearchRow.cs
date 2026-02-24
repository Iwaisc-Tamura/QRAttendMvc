namespace QRAttendMvc.Models
{
    public class AttendeeSearchRow
    {
        public string CompanyName { get; set; } = "";
        public string WorkerId { get; set; } = "";
        public string WorkerName { get; set; } = "";

        public DateTime? BirthDate { get; set; }
        public DateTime? ExcludeDate { get; set; }

        public DateTime? LastInTime { get; set; }
        public DateTime? LastOutTime { get; set; }
    }
}