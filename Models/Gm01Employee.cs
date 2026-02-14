using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QRAttendMvc.Models
{
    /// <summary>
    /// 協力会社従業員情報マスタ（GM01_EMPLOYEE）
    /// ※本システムで必要な最小項目のみ定義
    /// </summary>
    public class Gm01Employee
    {
        /// <summary>協力会社コード</summary>
        [Key]
        public string CooperateCd { get; set; } = "";

        /// <summary>従業員コード</summary>
        [Key]
        public string EmployeeCd { get; set; } = "";

        /// <summary>姓</summary>
        public string? FamilyName { get; set; }

        /// <summary>名</summary>
        public string? FirstName { get; set; }

        /* 追加 2026.02.14 Takada 生年月日（yyyyMMdd）*/
        [Column("BIRTH_YMD")]
        public string? BirthYmd { get; set; }

        /* 追加 2026.02.14 Takada 名簿対象除外日（= 退職日 yyyyMMdd）*/
        [Column("RETIRE_YMD")]
        public string? RetireYmd { get; set; }

        /// <summary>フルネーム（画面表示用）</summary>
        public string DisplayName
            => $"{FamilyName ?? ""} {FirstName ?? ""}".Trim();
    }
}
