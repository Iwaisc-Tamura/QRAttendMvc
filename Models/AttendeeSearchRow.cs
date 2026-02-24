namespace QRAttendMvc.Models
{
    public class AttendeeSearchRow
    {
        public string KaisaiCd { get; set; } = "";
        public string CooperateCd { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CompanyNameKana { get; set; } = "";
        public string WorkerId { get; set; } = "";
        public string WorkerName { get; set; } = "";
        public string WorkerNameKana { get; set; } = "";

        public string? BirthDate { get; set; }
        public string? LastInTime { get; set; }
        public string? LastOutTime { get; set; }
        public string? ExcludeDate { get; set; }

        public string PrimeOffice { get; set; } = "";
    }
}