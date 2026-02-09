using System;

namespace QRAttendMvc.Models
{
    /// <summary>
    /// 操作ログ（TX01_LOG）
    /// </summary>
    public class Tx01Log
    {
        /// <summary>自動連番（IDENTITY, PK）</summary>
        public int InsNo { get; set; }

        /// <summary>画面ID（G+連番）</summary>
        public string? ScreenId { get; set; }

        /// <summary>アクションコード（A+連番）</summary>
        public string? ActionCd { get; set; }

        /// <summary>イベントコード（開催コード/KAISAI_CD を格納）</summary>
        public string? EventCd { get; set; }

        /// <summary>作業員ID</summary>
        public string? EmployeeCd { get; set; }

        /// <summary>協力会社コード</summary>
        public string? CooperateCd { get; set; }

        /// <summary>作業員名 姓</summary>
        public string? FamilyName { get; set; }

        /// <summary>作業員名 名</summary>
        public string? FirstName { get; set; }

        /// <summary>生年月日(yyyymmdd)</summary>
        public string? BirthYmd { get; set; }

        /// <summary>入場時刻(HHmm)</summary>
        public string? EntryTime { get; set; }

        /// <summary>退場時刻(HHmm)</summary>
        public string? ExitTime { get; set; }

        /// <summary>事由コード</summary>
        public string? ReasonCd { get; set; }

        // 検索ワード系（必要な場合に使用）
        public string? SCooperateKana { get; set; }
        public string? SCooperateName { get; set; }
        public string? SEmployeeKanas { get; set; }
        public string? SEmployeeKanan { get; set; }
        public string? SEmployeeKanjis { get; set; }
        public string? SEmployeeKanjin { get; set; }
        public string? SBirthYmd { get; set; }
        public string? SEmployeeCd { get; set; }
        public string? SSelect { get; set; }

        public int? JStrat { get; set; }
        public int? JMaisu { get; set; }

        /// <summary>登録結果（T+連番）</summary>
        public string? TResart { get; set; }

        /// <summary>更新担当者コード（ログイン社員コード）</summary>
        public string? UTantoCd { get; set; }

        /// <summary>更新時刻</summary>
        public DateTime? UTimeStamp { get; set; }
    }
}
