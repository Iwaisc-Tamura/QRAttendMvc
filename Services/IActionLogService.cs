using System;
using System.Threading.Tasks;

namespace QRAttendMvc.Services
{
    public interface IActionLogService
    {
        Task ActionLogSaveAsync(
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
            string? sEmployeeKana = null,   // ★DB列に合わせる
            string? sEmployeeKanji = null,  // ★DB列に合わせる
            string? sBirthYmd = null,
            string? sEmployeeCd = null,
            string? sSelect = null,
            string? tResult = null,         // ★DB列に合わせる
            string? uTantoCd = null,
            DateTime? uTimeStamp = null
        );
    }
}