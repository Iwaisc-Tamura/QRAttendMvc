using Microsoft.EntityFrameworkCore;

namespace QRAttendMvc.Models
{
    [Keyless]
    public class EntryExitInquiryDto
    {
        public string? KAISAI_CD { get; set; }
        public string? COOPERATE_CD { get; set; }
        public string? COMPANY_NAME { get; set; }
        public string? COMPANY_NAME_KANA { get; set; }
        public string? EMPLOYEE_CD { get; set; }
        public string? EMPLOYEE_NAME { get; set; }
        public string? EMPLOYEE_NAME_KANA { get; set; }

        // SP側で fn_FormatYMD / fn_FormatTime されて文字列で返る前提
        public string? BIRTH_YMD { get; set; }     // 例: yyyy/MM/dd
        public string? ENTRY_TIME { get; set; }    // 例: HH:mm
        public string? EXIT_TIME { get; set; }     // 例: HH:mm
        public string? RETIRE_YMD { get; set; }    // 例: yyyy/MM/dd

        public string? PRIMEOFFICE { get; set; }
    }
}