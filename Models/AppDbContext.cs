using Microsoft.EntityFrameworkCore;

namespace QRAttendMvc.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // 開催イベント
        public DbSet<Gt01KaisaiEvent> KaisaiEvents => Set<Gt01KaisaiEvent>();

        // 従業員（旧 Participants）
        public DbSet<Gm01Employee> Employees => Set<Gm01Employee>();

        // 入退場ログ（旧 EventLogs）
        public DbSet<Tt02EntryExit> EntryExitLogs => Set<Tt02EntryExit>();

        // 対象イベント
        public DbSet<TargetEvent> TargetEvents => Set<TargetEvent>();

        // 操作ログ
        public DbSet<Tx01Log> OperationLogs => Set<Tx01Log>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Gt01KaisaiEvent>(e =>
            {
                e.ToTable("GT01_KAISAI_EVENT");
                e.HasKey(x => x.KaisaiCd);

                // DB column names are UPPER_SNAKE. Map explicitly.
                e.Property(x => x.KaisaiCd).HasColumnName("KAISAI_CD");
                e.Property(x => x.EventKbn).HasColumnName("EVENT_KBN");
                e.Property(x => x.PartnerCd).HasColumnName("PARTNER_CD");
                e.Property(x => x.EventCd).HasColumnName("EVENT_CD");
                e.Property(x => x.EventName).HasColumnName("EVENT_NAME");
                e.Property(x => x.Nendo).HasColumnName("NENDO");
                e.Property(x => x.KaisaiYmd).HasColumnName("KAISAI_YMD");
                e.Property(x => x.BranchCd).HasColumnName("BRANCH_CD");
                e.Property(x => x.BranchName).HasColumnName("BRANCH_NAME");
                e.Property(x => x.Location).HasColumnName("LOCATION");
                e.Property(x => x.ReceptTime).HasColumnName("RECEPT_TIME");
                e.Property(x => x.StartTime).HasColumnName("START_TIME");
                e.Property(x => x.EndTime).HasColumnName("END_TIME");
                e.Property(x => x.TrKbn).HasColumnName("TR_KBN");
                e.Property(x => x.QrKbn).HasColumnName("QR_KBN");
                e.Property(x => x.GroupCd).HasColumnName("GROUP_CD");
            });

            modelBuilder.Entity<Gm01Employee>(e =>
            {
                e.ToTable("GM01_EMPLOYEE");
                e.HasKey(x => new { x.CooperateCd, x.EmployeeCd });

                e.Property(x => x.CooperateCd).HasColumnName("COOPERATE_CD");
                e.Property(x => x.EmployeeCd).HasColumnName("EMPLOYEE_CD");
                e.Property(x => x.FamilyName).HasColumnName("FAMILY_NAME");
                e.Property(x => x.FirstName).HasColumnName("FIRST_NAME");
            });

            modelBuilder.Entity<Tt02EntryExit>(e =>
            {
                e.ToTable("TT02_ENTRY_EXIT");
                e.HasKey(x => new { x.KaisaiCd, x.EmployeeCd });

                e.Property(x => x.KaisaiCd).HasColumnName("KAISAI_CD");
                e.Property(x => x.EmployeeCd).HasColumnName("EMPLOYEE_CD");
                e.Property(x => x.CooperateCd).HasColumnName("COOPERATE_CD");
                e.Property(x => x.CompanyName).HasColumnName("COMPANY_NAME");
                e.Property(x => x.FamilyName).HasColumnName("FAMILY_NAME");
                e.Property(x => x.FirstName).HasColumnName("FIRST_NAME");
                e.Property(x => x.FamilyNameKana).HasColumnName("FAMILY_NAME_KANA");
                e.Property(x => x.FirstNameKana).HasColumnName("FIRST_NAME_KANA");
                e.Property(x => x.BirthYmd).HasColumnName("BIRTH_YMD");
                e.Property(x => x.Type).HasColumnName("TYPE");
                e.Property(x => x.EntryTime).HasColumnName("ENTRY_TIME");
                e.Property(x => x.ExitTime).HasColumnName("EXIT_TIME");
                e.Property(x => x.ActionCd).HasColumnName("ACTION_CD");
                e.Property(x => x.TensoFlg).HasColumnName("TENSO_FLG");
                e.Property(x => x.TensoYmdTime).HasColumnName("TENSO_YMD_TIME");
            });

            modelBuilder.Entity<TargetEvent>(e =>
            {
                e.ToTable("TT01_TARGET_EVENT");
                e.HasKey(x => new { x.BranchCd, x.KaisaiCd });

                e.Property(x => x.BranchCd).HasColumnName("BRANCH_CD");
                e.Property(x => x.KaisaiCd).HasColumnName("KAISAI_CD");
                e.Property(x => x.SelectYmdTime).HasColumnName("SELECT_YMD_TIME");
            });
        
modelBuilder.Entity<Tx01Log>(e =>
{
    e.ToTable("TX01_LOG");
    e.HasKey(x => x.InsNo);

    e.Property(x => x.InsNo).HasColumnName("INS_NO");
    e.Property(x => x.ScreenId).HasColumnName("SCREEN_ID");
    e.Property(x => x.ActionCd).HasColumnName("ACTION_CD");
    e.Property(x => x.EventCd).HasColumnName("EVENT_CD");
    e.Property(x => x.EmployeeCd).HasColumnName("EMPLOYEE_CD");
    e.Property(x => x.CooperateCd).HasColumnName("COOPERATE_CD");
    e.Property(x => x.FamilyName).HasColumnName("FAMILY_NAME");
    e.Property(x => x.FirstName).HasColumnName("FIRST_NAME");
    e.Property(x => x.BirthYmd).HasColumnName("BIRTH_YMD");
    e.Property(x => x.EntryTime).HasColumnName("ENTRY_TIME");
    e.Property(x => x.ExitTime).HasColumnName("EXIT_TIME");
    e.Property(x => x.ReasonCd).HasColumnName("REASON_CD");

    e.Property(x => x.SCooperateKana).HasColumnName("S_COOPERATE_KANA");
    e.Property(x => x.SCooperateName).HasColumnName("S_COOPERATE_NAME");
    e.Property(x => x.SEmployeeKanas).HasColumnName("S_EMPLOYEE_KANAS");
    e.Property(x => x.SEmployeeKanan).HasColumnName("S_EMPLOYEE_KANAN");
    e.Property(x => x.SEmployeeKanjis).HasColumnName("S_EMPLOYEE_KANJIS");
    e.Property(x => x.SEmployeeKanjin).HasColumnName("S_EMPLOYEE_KANJIN");
    e.Property(x => x.SBirthYmd).HasColumnName("S_BIRTH_YMD");
    e.Property(x => x.SEmployeeCd).HasColumnName("S_EMPLOYEE_CD");
    e.Property(x => x.SSelect).HasColumnName("S_SELECT");

    e.Property(x => x.JStrat).HasColumnName("J_STRAT");
    e.Property(x => x.JMaisu).HasColumnName("J_MAISU");
    e.Property(x => x.TResart).HasColumnName("T_RESART");

    e.Property(x => x.UTantoCd).HasColumnName("U_TANTO_CD");
    e.Property(x => x.UTimeStamp).HasColumnName("U_TIME_STAMP");
});
}
    }
}
