namespace QRAttendMvc.Models
{
    public class Gm02Cooperate
    {
        public string CooperateCd { get; set; } = default!;
        public string? CompanyName { get; set; }
        public string? CompanyNameKana { get; set; }
        public string? ApplySYmd { get; set; }
        public string? ApplyEYmd { get; set; }
    }
}