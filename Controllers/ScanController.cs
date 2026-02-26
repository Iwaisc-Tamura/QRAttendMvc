using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QRAttendMvc.Models;
using QRAttendMvc.Services;
using System.Linq;
using System.Text.RegularExpressions;

namespace QRAttendMvc.Controllers
{
//変更 2026.02.19 Takada 画面入場ログ
    //public class ScanController : Controller
    //{
    public class ScanController(AppDbContext db, IActionLogService logService)
        //追加 2026.02.20 Takada　ビルドエラーが出た為
        : BaseController(logService)
    {
        private readonly AppDbContext _db = db;
        private readonly IActionLogService _logService = logService;
        // EventSelectionで確定した開催コード（KAISAI_CD）
        private const string SessionKeyCurrentKaisaiCd = "CurrentKaisaiCd";
        // 作業員ID（GM01_EMPLOYEE.EMPLOYEE_CD）は 10桁固定（数字のみ）
        private static bool IsEmployeeCode(string code)
            => Regex.IsMatch(code ?? "", @"^\d{10}$");

        private string ScreenIdForKind(string kind)
            => (kind ?? "").ToUpper() == "IN" ? "G30" : "G50";

        private string ScreenIdForTempKind(string kind)
            => (kind ?? "").ToUpper() == "IN" ? "G40" : "G60";

        private async Task WriteOperationLogAsync(string screenId, string actionCd, string? kaisaiCd, string? workerCd, Gm01Employee? emp, string? entryTime, string? exitTime, string tResart)
        {
            try
            {
                var opCd = HttpContext.Session.GetString("EMPLOYEE_CD");
                var log = new Tx01Log
                {
                    ScreenId = screenId,
                    ActionCd = actionCd,
                    EventCd = kaisaiCd,
                    EmployeeCd = workerCd,
                    CooperateCd = emp?.CooperateCd,
                    FamilyName = emp?.FamilyName,
                    FirstName = emp?.FirstName,
                    EntryTime = entryTime,
                    ExitTime = exitTime,
                    TResart = tResart,
                    UTantoCd = opCd,
                    UTimeStamp = DateTime.Now
                };

                _db.OperationLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // ログ記録失敗でも業務処理は止めない
            }
        }

        // 一括入退場登録画面（連続登録）
        [HttpGet]
        public async Task<IActionResult> Batch(string kind = "IN")
        {
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);
            ViewBag.CurrentKaisaiCd = kaisaiCd;
            var kindUpper = (kind ?? "IN").ToUpper();
            ViewBag.Kind = kindUpper;

            // イベント情報表示用
            if (!string.IsNullOrEmpty(kaisaiCd))
            {
                var ev = await _db.KaisaiEvents.FirstOrDefaultAsync(x => x.KaisaiCd == kaisaiCd);
                ViewBag.Event = ev; // null の場合もあり得る
            }

            // ログイン表示用（支店-社員）
            ViewBag.LoginDisplay = $"{HttpContext.Session.GetString("BRANCH_CD")}-{HttpContext.Session.GetString("EMPLOYEE_CD")}";

            // 画面表示ログ（ACTION_CD：A01）
            try
            {
                await _logService.ActionLogSaveAsync(
                    screenId: ScreenIdForKind(kindUpper),
                    actionCd: "A01",
                    eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                    employeeCd: null,
                    cooperateCd: null,
                    familyName: null,
                    firstName: null,
                    birthYmd: null,
                    entryTime: null,
                    exitTime: null,
                    reasonCd: null,
                    sCooperateKana: null,
                    sCooperateName: null,
                    sEmployeeKanas: null,
                    sEmployeeKanan: null,
                    sEmployeeKanjis: null,
                    sEmployeeKanjin: null,
                    sBirthYmd: null,
                    sEmployeeCd: null,
                    sSelect: null,
                    jStrat: null,
                    jMaisu: null,
                    tResart: null,
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: null
                );
            }
            catch
            {
                // ログ失敗は無視して画面を返す
            }

            return View();
        }

        // イベント選択画面に戻る（Batch画面から） — ログを残して遷移
        [HttpPost]
        public async Task<IActionResult> BackToEventSelection(string kind)
        {
            var kindUpper = (kind ?? "IN").ToUpper();
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            try
            {
                await _logService.ActionLogSaveAsync(
                    screenId: ScreenIdForKind(kindUpper),
                    actionCd: "A02",
                    eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                    employeeCd: null,
                    cooperateCd: null,
                    familyName: null,
                    firstName: null,
                    birthYmd: null,
                    entryTime: null,
                    exitTime: null,
                    reasonCd: null,
                    sCooperateKana: null,
                    sCooperateName: null,
                    sEmployeeKanas: null,
                    sEmployeeKanan: null,
                    sEmployeeKanjis: null,
                    sEmployeeKanjin: null,
                    sBirthYmd: null,
                    sEmployeeCd: null,
                    sSelect: null,
                    jStrat: null,
                    jMaisu: null,
                    tResart: null,
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: null
                );
            }
            catch
            {
                // ログ失敗は無視
            }

            return RedirectToAction("Index", "EventSelection");
        }

        [HttpPost]
        public async Task<IActionResult> BackToEventSelectionTemp(string kind)
        {
            var kindUpper = (kind ?? "IN").ToUpper();
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            try
            {
                await _logService.ActionLogSaveAsync(
                    screenId: ScreenIdForTempKind(kindUpper),
                    actionCd: "A02",
                    eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                    employeeCd: null,
                    cooperateCd: null,
                    familyName: null,
                    firstName: null,
                    birthYmd: null,
                    entryTime: null,
                    exitTime: null,
                    reasonCd: null,
                    sCooperateKana: null,
                    sCooperateName: null,
                    sEmployeeKanas: null,
                    sEmployeeKanan: null,
                    sEmployeeKanjis: null,
                    sEmployeeKanjin: null,
                    sBirthYmd: null,
                    sEmployeeCd: null,
                    sSelect: null,
                    jStrat: null,
                    jMaisu: null,
                    tResart: null,
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: null
                );
            }
            catch
            {
                // ログ失敗は無視
            }

            return RedirectToAction("Index", "EventSelection");
        }

        public class TempQrLogRequest
        {
            public string? Kind { get; set; }
            public string? WorkerCd { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> TempQrReadLog([FromBody] TempQrLogRequest req)
        {
            var kindUpper = (req?.Kind ?? "IN").ToUpper();
            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            try
            {
                await _logService.ActionLogSaveAsync(
                    screenId: ScreenIdForTempKind(kindUpper),
                    actionCd: "A03",
                    eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                    employeeCd: string.IsNullOrWhiteSpace(req?.WorkerCd) ? null : req!.WorkerCd,
                    cooperateCd: null,
                    familyName: null,
                    firstName: null,
                    birthYmd: null,
                    entryTime: null,
                    exitTime: null,
                    reasonCd: null,
                    sCooperateKana: null,
                    sCooperateName: null,
                    sEmployeeKanas: null,
                    sEmployeeKanan: null,
                    sEmployeeKanjis: null,
                    sEmployeeKanjin: null,
                    sBirthYmd: null,
                    sEmployeeCd: null,
                    sSelect: null,
                    jStrat: null,
                    jMaisu: null,
                    tResart: null,
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: null
                );
            }
            catch
            {
                // ログ失敗は無視
            }

            return Ok();
        }

        public class RecordRequest
        {
            public string? Code { get; set; }
            public string? Kind { get; set; }
            // 画面から渡される開催コード（セッションが送れない環境の保険）
            public string? KaisaiCd { get; set; }
            // QR種別（'1' or '2'）を渡せる（Lookup で仮QRを許容するため）
            public string? Prefix { get; set; }
        }

        private async Task<(bool ok, string result, string mark, string message, string? name)>
            ProcessEntryExitAsync(string workerCd, string kind, string? overrideKaisaiCd = null, string? qrPrefix = null)
        {
            var now = DateTime.Now;
            var hhmm = now.ToString("HHmm");

            // セッションの開催コードを優先。ただし画面から渡されたものがある場合はそれを使えるようにする。
            string? kaisaiCd = !string.IsNullOrWhiteSpace(overrideKaisaiCd)
                ? overrideKaisaiCd
                : HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            if (string.IsNullOrEmpty(kaisaiCd))
            {
                return (false, "NG", "×", "イベントが確定していません。", null);
            }

            // 操作者コードをセッションから取得
            var tantoCd = HttpContext.Session.GetString("EMPLOYEE_CD");

            // 作業員マスタ（仮QR の場合はマスタなしでも処理を許可する）
            var emp = await _db.Employees.FirstOrDefaultAsync(x => x.EmployeeCd == workerCd);

            string ResolveDisplayName(Tt02EntryExit? entryRow)
            {
                if (emp != null) return emp.DisplayName;
                var name = string.Join(" ", new[] { entryRow?.FamilyName, entryRow?.FirstName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                return string.IsNullOrWhiteSpace(name) ? workerCd : name;
            }

            // 協力会社マスタ（会社名などを補完） — emp が存在する場合のみ取得
            Gm02Cooperate? coop = null;
            if (emp != null && !string.IsNullOrWhiteSpace(emp.CooperateCd))
            {
                coop = await _db.Cooperates.FirstOrDefaultAsync(c => c.CooperateCd == emp.CooperateCd);
            }

            // QR種別から Type を決定（1:通常QR, 5:仮QR, 9:その他）
            string newType;
            if (qrPrefix == "1")
                newType = "1";
            else if (qrPrefix == "2")
                newType = "5";
            else if (!string.IsNullOrWhiteSpace(workerCd) && workerCd.StartsWith("T"))
                newType = "9";
            else
                newType = "9";

            // 当該開催の既存入退場レコード
            var row = await _db.EntryExitLogs
                .FirstOrDefaultAsync(x => x.KaisaiCd == kaisaiCd && x.EmployeeCd == workerCd);

            // 退場で入場なしは受け付けない（既存の仕様）
            if (kind == "OUT" && (row == null || string.IsNullOrWhiteSpace(row.EntryTime)))
            {
                var display = ResolveDisplayName(row);
                return (false, "WARN", "△", "入場記録がないため退場登録できません。", display);
            }

            // 新規レコード作成時は可能な限りマスタ情報で埋める（IN の場合のみ）
            if (row == null)
            {
                row = new Tt02EntryExit
                {
                    KaisaiCd = kaisaiCd,
                    EmployeeCd = workerCd,
                    CooperateCd = emp?.CooperateCd,
                    CompanyName = coop?.CompanyName ?? null,
                    FamilyName = emp?.FamilyName,
                    FirstName = emp?.FirstName,
                    FamilyNameKana = emp?.FamilyNameKana ?? null,
                    FirstNameKana = emp?.FirstNameKana ?? null,
                    BirthYmd = emp?.BirthYmd ?? null,
                    // 必要に応じて Type を設定してください（現状は null）
                    Type = newType,
                    // 一括画面ではNULL
                    ActionCd = null,
                    // TENSO 関連は現時点では未転送としておく
                    TensoFlg = null,
                    TensoYmdTime = null,
                    UTantoCd = tantoCd,
                    UTimeStamp = now
                };
                _db.EntryExitLogs.Add(row);
            }
            else
            {
                // 既存レコードについて不足カラムを補完（マスタにある場合のみ上書きしない方針）
                if (emp != null)
                {
                    if (string.IsNullOrWhiteSpace(row.CooperateCd) && !string.IsNullOrWhiteSpace(emp.CooperateCd))
                        row.CooperateCd = emp.CooperateCd;
                    if (string.IsNullOrWhiteSpace(row.CompanyName) && !string.IsNullOrWhiteSpace(coop?.CompanyName))
                        row.CompanyName = coop!.CompanyName;
                    if (string.IsNullOrWhiteSpace(row.FamilyName) && !string.IsNullOrWhiteSpace(emp.FamilyName))
                        row.FamilyName = emp.FamilyName;
                    if (string.IsNullOrWhiteSpace(row.FirstName) && !string.IsNullOrWhiteSpace(emp.FirstName))
                        row.FirstName = emp.FirstName;
                    if (string.IsNullOrWhiteSpace(row.FamilyNameKana) && !string.IsNullOrWhiteSpace(emp.FamilyNameKana))
                        row.FamilyNameKana = emp.FamilyNameKana;
                    if (string.IsNullOrWhiteSpace(row.FirstNameKana) && !string.IsNullOrWhiteSpace(emp.FirstNameKana))
                        row.FirstNameKana = emp.FirstNameKana;
                    if (string.IsNullOrWhiteSpace(row.BirthYmd) && !string.IsNullOrWhiteSpace(emp.BirthYmd))
                        row.BirthYmd = emp.BirthYmd;
                }

                // Type が未設定なら補完（既存方針に合わせ上書きは行わない）
                if (string.IsNullOrWhiteSpace(row.Type) && !string.IsNullOrWhiteSpace(newType))
                    row.Type = newType;
                
                row.ActionCd = "OK";

                // 更新時は常にU_TANTO_CDとU_TIME_STAMPを更新
                row.UTantoCd = tantoCd;
                row.UTimeStamp = now;
            }

            if (kind == "IN")
            {
                // 入場は何度でも OK（既存の EntryTime を保持する方針）
                if (string.IsNullOrEmpty(row.EntryTime))
                {
                    row.EntryTime = hhmm;
                    // 明示的に変更フラグを立てる（安全策）
                    _db.Entry(row).Property(r => r.EntryTime).IsModified = true;
                }
            }
            else
            {
                // 退場時は時間を毎回更新
                row.ExitTime = hhmm;
                // 明示的に変更フラグを立てる（更新が反映されない問題を回避）
                _db.Entry(row).Property(r => r.ExitTime).IsModified = true;
            }

            // DB書き込み（例外を捕まえてエラーメッセージを返す）
            try
            {
                await _db.SaveChangesAsync();

                // 操作ログを保存（失敗しても業務処理は止めない）
                try
                {
                    // 既存の Tx01_LOG（WriteOperationLogAsync）保持 — emp が null でも許容
                    await WriteOperationLogAsync("G001", kind == "IN" ? "IN" : "OUT", kaisaiCd, workerCd, emp, row.EntryTime, row.ExitTime, "TOK");
                }
                catch
                {
                    // ログ失敗は無視
                }

                // 要求どおりのアクションログ（成功：ACTION_CD=T01）
                try
                {
                    if (kind == "IN")
                    {
                        await _logService.ActionLogSaveAsync(
                            screenId: ScreenIdForKind(kind),
                            actionCd: "A04",
                            eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                            employeeCd: workerCd,
                            cooperateCd: emp?.CooperateCd,
                            familyName: emp?.FamilyName,
                            firstName: emp?.FirstName,
                            birthYmd: emp?.BirthYmd,
                            entryTime: row.EntryTime,
                            exitTime: null,
                            reasonCd: null,
                            sCooperateKana: null,
                            sCooperateName: null,
                            sEmployeeKanas: null,
                            sEmployeeKanan: null,
                            sEmployeeKanjis: null,
                            sEmployeeKanjin: null,
                            sBirthYmd: null,
                            sEmployeeCd: null,
                            sSelect: null,
                            jStrat: null,
                            jMaisu: null,
                            tResart: "T01",
                            uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                            uTimeStamp: null
                        );
                    }
                    else
                    {
                        await _logService.ActionLogSaveAsync(
                            screenId: ScreenIdForKind(kind),
                            actionCd: "A04",
                            eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                            employeeCd: workerCd,
                            cooperateCd: emp?.CooperateCd,
                            familyName: emp?.FamilyName,
                            firstName: emp?.FirstName,
                            birthYmd: emp?.BirthYmd,
                            entryTime: null,
                            exitTime: row.ExitTime,
                            reasonCd: null,
                            sCooperateKana: null,
                            sCooperateName: null,
                            sEmployeeKanas: null,
                            sEmployeeKanan: null,
                            sEmployeeKanjis: null,
                            sEmployeeKanjin: null,
                            sBirthYmd: null,
                            sEmployeeCd: null,
                            sSelect: null,
                            jStrat: null,
                            jMaisu: null,
                            tResart: "T01",
                            uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                            uTimeStamp: null
                        );
                    }
                }
                catch
                {
                    // ログ失敗は無視
                }
            }
            //修正 2026.02.20 Takada ；catch (Exception ex)からワーニング除外
            catch (Exception)
            {
                // 追跡ログとして簡潔に返す（詳細はサーバ側ログ参照）
                var display = ResolveDisplayName(row);
                return (false, "NG", "×", "DB書き込みエラーが発生しました。係員に問い合わせてください。", display);
            }

            return (true, "OK", "〇",
                kind == "IN" ? "入場記録OK" : "退場記録OK",
                ResolveDisplayName(row));
        }

        // QR または手入力で入退場記録（連続登録） 
        [HttpPost]
        public async Task<IActionResult> Record([FromBody] RecordRequest req)
        {
            var now = DateTime.Now;
            var hhmm = now.ToString("HHmm");

            if (req == null || string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Kind))
            {
                return Json(new { ok = false, message = "パラメータ不正", time = hhmm });
            }

            var kind = (req.Kind ?? "IN").ToUpper().Trim();

            // 受信した Code を柔軟に処理:
            // - カンマ区切りで先頭が '1' or '2' の場合はその仕様を使用
            var raw = req.Code.Trim();
            string? overrideKaisaiFromQr = null;
            string workerCd;
            string? qrPrefix = null;

            if (raw.Contains(","))
            {
                var parts = raw.Split(',').Select(p => p.Trim()).ToArray();
                // 先頭は必ず '1' または '2' を期待する。違う場合は T03 をログしてNG。
                if (parts.Length >= 2 && (parts[0] == "1" || parts[0] == "2"))
                {
                    qrPrefix = parts[0];
                    workerCd = parts[1];
                    if (parts[0] == "2" && parts.Length >= 3)
                    {
                        overrideKaisaiFromQr = parts[2];
                    }
                }
                else
                {
                    try
                    {
                        await _logService.ActionLogSaveAsync(
                            screenId: ScreenIdForKind(kind),
                            actionCd: "A04",
                            eventCd: HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd),
                            employeeCd: null,
                            cooperateCd: null,
                            familyName: null,
                            firstName: null,
                            birthYmd: null,
                            entryTime: null,
                            exitTime: null,
                            reasonCd: null,
                            sCooperateKana: null,
                            sCooperateName: null,
                            sEmployeeKanas: null,
                            sEmployeeKanan: null,
                            sEmployeeKanjis: null,
                            sEmployeeKanjin: null,
                            sBirthYmd: null,
                            sEmployeeCd: null,
                            sSelect: null,
                            jStrat: null,
                            jMaisu: null,
                            tResart: "T03",
                            uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                            uTimeStamp: null
                        );
                    }
                    catch
                    {
                        // ログ失敗は無視
                    }

                    return Json(new
                    {
                        ok = false,
                        result = "NG",
                        mark = "×",
                        message = "QRコードが読めません。係員に相談して下さい。（QR形式不正）",
                        code = raw,
                        time = hhmm
                    });
                }
            }
            else
            {
                // カンマ無し: 作業員ID形式不正 — ログ ACTION_CD=T03
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForKind(kind),
                        actionCd: "A04",
                        eventCd: HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd),
                        employeeCd: null,
                        cooperateCd: null,
                        familyName: null,
                        firstName: null,
                        birthYmd: null,
                        entryTime: null,
                        exitTime: null,
                        reasonCd: null,
                        sCooperateKana: null,
                        sCooperateName: null,
                        sEmployeeKanas: null,
                        sEmployeeKanan: null,
                        sEmployeeKanjis: null,
                        sEmployeeKanjin: null,
                        sBirthYmd: null,
                        sEmployeeCd: null,
                        sSelect: null,
                        jStrat: null,
                        jMaisu: null,
                        tResart: "T03",
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: null
                    );
                }
                catch
                {
                    // ログ失敗は無視
                }

                return Json(new
                {
                    ok = false,
                    result = "NG",
                    mark = "×",
                    message = "QRコードが読めません。係員に相談して下さい。（QR形式不正）",
                    code = raw,
                    time = hhmm
                });
            }

            // 作業員ID形式チェック（仮QRは除外）
            if (qrPrefix != "2" && !IsEmployeeCode(workerCd))
            {
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForKind(kind),
                        actionCd: "A04",
                        eventCd: HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd),
                        employeeCd: null,
                        cooperateCd: null,
                        familyName: null,
                        firstName: null,
                        birthYmd: null,
                        entryTime: null,
                        exitTime: null,
                        reasonCd: null,
                        sCooperateKana: null,
                        sCooperateName: null,
                        sEmployeeKanas: null,
                        sEmployeeKanan: null,
                        sEmployeeKanjis: null,
                        sEmployeeKanjin: null,
                        sBirthYmd: null,
                        sEmployeeCd: null,
                        sSelect: null,
                        jStrat: null,
                        jMaisu: null,
                        tResart: "T03",
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: null
                    );
                }
                catch
                {
                    // ログ失敗は無視
                }

                return Json(new
                {
                    ok = false,
                    result = "NG",
                    mark = "×",
                    message = "作業員ID形式が不正です",
                    code = workerCd,
                    time = hhmm
                });
            }

            // 追加: 仮QR (prefix == "2") の場合、QR内イベントがセッションのイベントと一致することを必須にする
            if (qrPrefix == "2")
            {
                var sessionKaisai = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

                if (string.IsNullOrWhiteSpace(overrideKaisaiFromQr) || string.IsNullOrWhiteSpace(sessionKaisai)
                    || !string.Equals(overrideKaisaiFromQr, sessionKaisai))
                {
                    try
                    {
                        await _logService.ActionLogSaveAsync(
                            screenId: ScreenIdForKind(kind),
                            actionCd: "A04",
                            eventCd: sessionKaisai,
                            employeeCd: null,
                            cooperateCd: null,
                            familyName: null,
                            firstName: null,
                            birthYmd: null,
                            entryTime: null,
                            exitTime: null,
                            reasonCd: null,
                            sCooperateKana: null,
                            sCooperateName: null,
                            sEmployeeKanas: null,
                            sEmployeeKanan: null,
                            sEmployeeKanjis: null,
                            sEmployeeKanjin: null,
                            sBirthYmd: null,
                            sEmployeeCd: null,
                            sSelect: null,
                            jStrat: null,
                            jMaisu: null,
                            tResart: "T03",
                            uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                            uTimeStamp: null
                        );
                    }
                    catch
                    {
                        // ignore
                    }

                    return Json(new
                    {
                        ok = false,
                        result = "NG",
                        mark = "×",
                        message = "仮QRのイベントが現在のイベントと一致しません。",
                        code = workerCd,
                        time = hhmm
                    });
                }
            }

            // 入場の場合、仮QRは使用不可 — ログ ACTION_CD=T03
            if (qrPrefix == "2" && kind == "IN")
            {
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForKind(kind),
                        actionCd: "A04",
                        eventCd: HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd),
                        employeeCd: null,
                        cooperateCd: null,
                        familyName: null,
                        firstName: null,
                        birthYmd: null,
                        entryTime: null,
                        exitTime: null,
                        reasonCd: null,
                        sCooperateKana: null,
                        sCooperateName: null,
                        sEmployeeKanas: null,
                        sEmployeeKanan: null,
                        sEmployeeKanjis: null,
                        sEmployeeKanjin: null,
                        sBirthYmd: null,
                        sEmployeeCd: null,
                        sSelect: null,
                        jStrat: null,
                        jMaisu: null,
                        tResart: "T03",
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: null
                    );
                }
                catch
                {
                    // ignore
                }

                return Json(new
                {
                    ok = false,
                    result = "NG",
                    mark = "×",
                    message = "仮QRは入場登録では使用できません。",
                    code = workerCd,
                    time = hhmm
                });
            }

            // 通常QRで作業員マスタ未登録の場合は警告
            if (qrPrefix == "1")
            {
                var empCheck = await _db.Employees.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.EmployeeCd == workerCd);
                if (empCheck == null)
                {
                    return Json(new
                    {
                        ok = false,
                        result = "WARN",
                        mark = "△",
                        message = "作業員マスタに登録がありません",
                        code = workerCd,
                        time = hhmm
                    });
                }
            }

            // 画面から渡された KaisaiCd（あれば）を優先する
            var effectiveKaisai = !string.IsNullOrWhiteSpace(req.KaisaiCd) ? req.KaisaiCd : null;

            // 画面から渡された KaisaiCd（あれば）と QR種別（qrPrefix）を処理へ渡す
            var result = await ProcessEntryExitAsync(workerCd, kind, effectiveKaisai, qrPrefix);

            return Json(new
            {
                ok = result.ok,
                result = result.result,
                mark = result.mark,
                message = result.message,
                code = workerCd,
                name = result.name,
                time = hhmm
            });
        }

        private static string? partsOrFallback(string? workerCd)
        {
            // helper for previous return where original variable 'parts' is out of scope
            return workerCd;
        }

        /// <summary>
        /// QRで読み取った作業員IDから名簿／マスタ情報を取得して返す（TempInput 用）
        /// 必要項目が欠けている場合はエラーで返します。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Lookup([FromBody] RecordRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Code))
                return Json(new { ok = false, message = "パラメータ不正" });

            var workerCd = req.Code.Trim();

            // 仮QR(prefix == "2")の場合は作業員コード形式チェックをスキップ
            if (req.Prefix != "2" && !IsEmployeeCode(workerCd))
            {
                return Json(new { ok = false, message = "作業員ID形式が不正です" });
            }

            var kaisaiCd = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);
            if (string.IsNullOrEmpty(kaisaiCd))
            {
                return Json(new { ok = false, message = "イベントが確定していません" });
            }

            // 作業員マスタ（あれば取得）
            var emp = await _db.Employees.FirstOrDefaultAsync(x => x.EmployeeCd == workerCd);

            // 協力会社マスタ（会社名・会社名カナを取得）
            Gm02Cooperate? coop = null;
            if (emp != null && !string.IsNullOrWhiteSpace(emp.CooperateCd))
            {
                coop = await _db.Cooperates.FirstOrDefaultAsync(c => c.CooperateCd == emp.CooperateCd);
            }

            // 当該開催の名簿情報（入退場時刻を取得するため）
            var entry = await _db.EntryExitLogs
                .FirstOrDefaultAsync(e => e.EmployeeCd == workerCd && e.KaisaiCd == kaisaiCd);

            // 除外日をフォーマット（YYYYMMDD -> YYYY/MM/DD）- GM01_EMPLOYEEから取得
            string FormatExcludeDate(string? excludeDate)
            {
                if (string.IsNullOrWhiteSpace(excludeDate))
                    return "";
                
                var trimmed = excludeDate.Trim();
                
                // YYYYMMDDフォーマット（8桁）の場合は変換
                if (trimmed.Length == 8 && DateTime.TryParseExact(trimmed, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dt))
                {
                    return dt.ToString("yyyy/MM/dd");
                }
                
                // 変換できない場合はそのまま返す
                return trimmed;
            }

            // 仮QR の場合は作業員マスタが無くても許容する
            if (emp == null)
            {
                if (req.Prefix == "2")
                {
                    // 既に ENTRY_EXIT に存在すればそれを返す
                    if (entry != null)
                    {
                        Gm02Cooperate? entryCoop = null;
                        if (!string.IsNullOrWhiteSpace(entry.CooperateCd))
                        {
                            entryCoop = await _db.Cooperates.AsNoTracking()
                                .FirstOrDefaultAsync(c => c.CooperateCd == entry.CooperateCd);
                        }

                        return Json(new
                        {
                            ok = true,
                            code = workerCd,
                            cooperateCd = entry.CooperateCd ?? "",
                            companyKana = entryCoop?.CompanyNameKana ?? "",
                            companyName = entryCoop?.CompanyName ?? (entry.CompanyName ?? ""),
                            workerFamilyKana = entry.FamilyNameKana ?? "",
                            workerFirstKana = entry.FirstNameKana ?? "",
                            workerFamilyName = entry.FamilyName ?? "",
                            workerFirstName = entry.FirstName ?? "",
                            workerKana = $"{entry.FamilyNameKana ?? ""} {entry.FirstNameKana ?? ""}",
                            workerName = (entry.FamilyName ?? "") + (entry.FirstName != null ? (" " + entry.FirstName) : ""),
                            birthYmd = entry.BirthYmd ?? "",
                            excludeDate = "",
                            entryTime = entry.EntryTime ?? "",
                            exitTime = entry.ExitTime ?? ""
                        });
                    }

                    // それ以外は作業員IDだけ表示できるように最小限の情報を返す
                    return Json(new
                    {
                        ok = true,
                        code = workerCd,
                        cooperateCd = "",
                        companyKana = "",
                        companyName = "",
                        workerFamilyKana = "",
                        workerFirstKana = "",
                        workerFamilyName = "",
                        workerFirstName = "",
                        workerKana = "仮QRコード",
                        workerName = "仮QRコード",
                        birthYmd = "",
                        excludeDate = "",
                        entryTime = "",
                        exitTime = ""
                    });
                }

                return Json(new { ok = false, message = "作業員マスタに登録がありません" });
            }

            // 必須項目チェック（マスタベースでチェック）
            if (string.IsNullOrWhiteSpace(emp.CooperateCd)
                || string.IsNullOrWhiteSpace(coop?.CompanyName)
                || string.IsNullOrWhiteSpace(emp.FamilyNameKana)
                || string.IsNullOrWhiteSpace(emp.FirstNameKana)
                || string.IsNullOrWhiteSpace(emp.BirthYmd))
            {
                return Json(new { ok = false, message = "マスタに必要な項目が不足しています" });
            }

            // 返却：GM01_EMPLOYEE及びGM02_COOPERATEから取得
            // 入退場時刻のみTT02_ENTRY_EXITから取得
            return Json(new
            {
                ok = true,
                code = workerCd,
                cooperateCd = emp.CooperateCd,
                companyKana = coop?.CompanyNameKana ?? "",
                companyName = coop?.CompanyName ?? "",
                workerFamilyKana = emp.FamilyNameKana,
                workerFirstKana = emp.FirstNameKana,
                workerFamilyName = emp.FamilyName,
                workerFirstName = emp.FirstName,
                workerKana = $"{emp.FamilyNameKana} {emp.FirstNameKana}",
                workerName = emp.DisplayName,
                birthYmd = emp.BirthYmd,
                excludeDate = FormatExcludeDate(emp.RetireYmd),
                entryTime = entry?.EntryTime ?? "",
                exitTime = entry?.ExitTime ?? ""
            });
        }

        [HttpGet]
        public async Task<IActionResult> LookupCooperate(string cooperateCd)
        {
            if (string.IsNullOrWhiteSpace(cooperateCd))
                return Json(new { ok = false, message = "協力会社コードが未指定です" });

            var cd = cooperateCd.Trim();
            var coop = await _db.Cooperates.AsNoTracking().FirstOrDefaultAsync(c => c.CooperateCd == cd);
            if (coop == null)
                return Json(new { ok = false, message = "協力会社が見つかりません" });

            return Json(new
            {
                ok = true,
                cooperateCd = coop.CooperateCd,
                companyName = coop.CompanyName ?? "",
                companyKana = coop.CompanyNameKana ?? ""
            });
        }

        public class TempRecordRequest
        {
            public string? CompanyCd { get; set; }
            public string? CompanyKana { get; set; }
            public string? CompanyName { get; set; }
            public string? WorkerName { get; set; }
            public string? WorkerKana { get; set; }
            public string? Kind { get; set; }
            public string? EntryRecord { get; set; }
            public string? ExitRecord { get; set; }
            public string? BirthDate { get; set; }
            public string? ExcludeDate { get; set; }
            public string? Reason { get; set; }
            public string? KaisaiCd { get; set; }
            public string? WorkerCd { get; set; }
            public string? QrPrefix { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> TempInput(string kind)
        {
            var kaisaiCd = HttpContext.Session.GetString("CurrentKaisaiCd");
            var kindUpper = (kind ?? "").ToUpper();

            if (string.IsNullOrEmpty(kaisaiCd))
            {
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForTempKind(kindUpper),
                        actionCd: "A02",
                        eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                        employeeCd: null,
                        cooperateCd: null,
                        familyName: null,
                        firstName: null,
                        birthYmd: null,
                        entryTime: null,
                        exitTime: null,
                        reasonCd: null,
                        sCooperateKana: null,
                        sCooperateName: null,
                        sEmployeeKanas: null,
                        sEmployeeKanan: null,
                        sEmployeeKanjis: null,
                        sEmployeeKanjin: null,
                        sBirthYmd: null,
                        sEmployeeCd: null,
                        sSelect: null,
                        jStrat: null,
                        jMaisu: null,
                        tResart: null,
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: null
                    );
                }
                catch { }

                TempData["Error"] = "イベントが確定していません。";
                return RedirectToAction("Index", "EventSelection");
            }

            var ev = await _db.KaisaiEvents.FirstOrDefaultAsync(x => x.KaisaiCd == kaisaiCd);

            if (ev == null)
            {
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForTempKind(kindUpper),
                        actionCd: "A02",
                        eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                        employeeCd: null,
                        cooperateCd: null,
                        familyName: null,
                        firstName: null,
                        birthYmd: null,
                        entryTime: null,
                        exitTime: null,
                        reasonCd: null,
                        sCooperateKana: null,
                        sCooperateName: null,
                        sEmployeeKanas: null,
                        sEmployeeKanan: null,
                        sEmployeeKanjis: null,
                        sEmployeeKanjin: null,
                        sBirthYmd: null,
                        sEmployeeCd: null,
                        sSelect: null,
                        jStrat: null,
                        jMaisu: null,
                        tResart: null,
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: null
                    );
                }
                catch { }

                TempData["Error"] = "イベント情報が取得できません。";
                return RedirectToAction("Index", "EventSelection");
            }

            ViewBag.Event = ev;
            ViewBag.CurrentKaisaiCd = kaisaiCd;
            ViewBag.LoginDisplay = $"{HttpContext.Session.GetString("BRANCH_CD")}-{HttpContext.Session.GetString("EMPLOYEE_CD")}";

            kind = kindUpper;
            if (kind != "IN" && kind != "OUT")
            {
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForTempKind(kindUpper),
                        actionCd: "A02",
                        eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                        employeeCd: null,
                        cooperateCd: null,
                        familyName: null,
                        firstName: null,
                        birthYmd: null,
                        entryTime: null,
                        exitTime: null,
                        reasonCd: null,
                        sCooperateKana: null,
                        sCooperateName: null,
                        sEmployeeKanas: null,
                        sEmployeeKanan: null,
                        sEmployeeKanjis: null,
                        sEmployeeKanjin: null,
                        sBirthYmd: null,
                        sEmployeeCd: null,
                        sSelect: null,
                        jStrat: null,
                        jMaisu: null,
                        tResart: null,
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: null
                    );
                }
                catch { }

                return RedirectToAction("Index", "EventSelection");
            }

            ViewBag.Kind = kind;
            ViewBag.ModeLabel = kind == "IN" ? "入場" : "退場";
            ViewBag.Title = "入退場登録（臨時対応窓口）";

            try
            {
                await _logService.ActionLogSaveAsync(
                    screenId: ScreenIdForTempKind(kindUpper),
                    actionCd: "A01",
                    eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                    employeeCd: null,
                    cooperateCd: null,
                    familyName: null,
                    firstName: null,
                    birthYmd: null,
                    entryTime: null,
                    exitTime: null,
                    reasonCd: null,
                    sCooperateKana: null,
                    sCooperateName: null,
                    sEmployeeKanas: null,
                    sEmployeeKanan: null,
                    sEmployeeKanjis: null,
                    sEmployeeKanjin: null,
                    sBirthYmd: null,
                    sEmployeeCd: null,
                    sSelect: null,
                    jStrat: null,
                    jMaisu: null,
                    tResart: null,
                    uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                    uTimeStamp: null
                );
            }
            catch
            {
                // ログ失敗は無視
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TempRecord([FromBody] TempRecordRequest req)
        {
            var now = DateTime.Now;
            var hhmm = now.ToString("HHmm");

            if (req == null || string.IsNullOrWhiteSpace(req.WorkerCd)
                || string.IsNullOrWhiteSpace(req.WorkerName)
                || string.IsNullOrWhiteSpace(req.WorkerKana)
                || string.IsNullOrWhiteSpace(req.BirthDate))
            {
                return Json(new { ok = false, message = "必須項目が未入力です", time = hhmm });
            }

            if (!DateTime.TryParse(req.BirthDate, out var birthDateValue))
            {
                return Json(new { ok = false, message = "生年月日が不正です", time = hhmm });
            }

            var kaisaiCd = !string.IsNullOrWhiteSpace(req.KaisaiCd)
                ? req.KaisaiCd
                : HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            if (string.IsNullOrWhiteSpace(kaisaiCd))
            {
                return Json(new { ok = false, message = "イベントが確定していません", time = hhmm });
            }

            var kind = (req.Kind ?? "IN").ToUpper();
            var tantoCd = HttpContext.Session.GetString("EMPLOYEE_CD");
            var workerCd = req.WorkerCd.Trim();

            string familyName = req.WorkerName?.Trim() ?? "";
            string firstName = "";
            if (!string.IsNullOrWhiteSpace(req.WorkerName))
            {
                var parts = req.WorkerName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    familyName = parts[0];
                    firstName = string.Join(' ', parts.Skip(1));
                }
            }

            string familyKana = "";
            string firstKana = "";
            if (!string.IsNullOrWhiteSpace(req.WorkerKana))
            {
                var parts = req.WorkerKana.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    familyKana = parts[0];
                    firstKana = string.Join(' ', parts.Skip(1));
                }
                else
                {
                    familyKana = req.WorkerKana.Trim();
                }
            }

            string? birthYmd = null;
            if (!string.IsNullOrWhiteSpace(req.BirthDate))
            {
                birthYmd = birthDateValue.ToString("yyyyMMdd");
            }

            string? entryTime = null;
            string? exitTime = null;

            if (kind == "IN")
            {
                entryTime = !string.IsNullOrWhiteSpace(req.EntryRecord) ? NormalizeTime(req.EntryRecord) : hhmm;
            }
            else if (kind == "OUT")
            {
                exitTime = !string.IsNullOrWhiteSpace(req.ExitRecord) ? NormalizeTime(req.ExitRecord) : hhmm;
            }

            var row = await _db.EntryExitLogs.FirstOrDefaultAsync(e => e.KaisaiCd == kaisaiCd && e.EmployeeCd == workerCd);

            if (kind == "IN")
            {
                if (row == null)
                {
                    string typeVal;
                    if (req.QrPrefix == "1") typeVal = "1";
                    else if (req.QrPrefix == "2") typeVal = "5";
                    else if (!string.IsNullOrWhiteSpace(workerCd) && workerCd.StartsWith("T")) typeVal = "9";
                    else typeVal = "9";

                    row = new Tt02EntryExit
                    {
                        KaisaiCd = kaisaiCd,
                        EmployeeCd = workerCd,
                        CooperateCd = req.CompanyCd,
                        CompanyName = req.CompanyName,
                        FamilyName = familyName,
                        FirstName = firstName,
                        FamilyNameKana = string.IsNullOrWhiteSpace(familyKana) ? null : familyKana,
                        FirstNameKana = string.IsNullOrWhiteSpace(firstKana) ? null : firstKana,
                        BirthYmd = birthYmd,
                        Type = typeVal,
                        EntryTime = entryTime,
                        ExitTime = exitTime,
                        ActionCd = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason,
                        TensoFlg = null,
                        TensoYmdTime = null,
                        UTantoCd = tantoCd,
                        UTimeStamp = now
                    };
                    _db.EntryExitLogs.Add(row);
                }
                else
                {
                    // 入場登録時は画面の入場記録で常に更新
                    row.EntryTime = entryTime;
                    _db.Entry(row).Property(r => r.EntryTime).IsModified = true;

                    if (string.IsNullOrWhiteSpace(row.CooperateCd) && !string.IsNullOrWhiteSpace(req.CompanyCd))
                        row.CooperateCd = req.CompanyCd;
                    if (string.IsNullOrWhiteSpace(row.CompanyName) && !string.IsNullOrWhiteSpace(req.CompanyName))
                        row.CompanyName = req.CompanyName;
                    if (string.IsNullOrWhiteSpace(row.FamilyName) && !string.IsNullOrWhiteSpace(familyName))
                        row.FamilyName = familyName;
                    if (string.IsNullOrWhiteSpace(row.FirstName) && !string.IsNullOrWhiteSpace(firstName))
                        row.FirstName = firstName;
                    if (string.IsNullOrWhiteSpace(row.FamilyNameKana) && !string.IsNullOrWhiteSpace(familyKana))
                        row.FamilyNameKana = familyKana;
                    if (string.IsNullOrWhiteSpace(row.FirstNameKana) && !string.IsNullOrWhiteSpace(firstKana))
                        row.FirstNameKana = firstKana;
                    if (string.IsNullOrWhiteSpace(row.BirthYmd) && !string.IsNullOrWhiteSpace(birthYmd))
                        row.BirthYmd = birthYmd;
                    if (!string.IsNullOrWhiteSpace(req.Reason))
                        row.ActionCd = req.Reason;

                    row.UTantoCd = tantoCd;
                    row.UTimeStamp = now;
                }
            }
            else
            {
                if (row == null)
                {
                    return Json(new { ok = false, message = "入場記録がないため退場登録できません。", time = hhmm });
                }

                if (string.IsNullOrWhiteSpace(row.EntryTime))
                {
                    return Json(new { ok = false, message = "入場時刻が登録されていないため退場登録できません。", time = hhmm });
                }

                row.ExitTime = exitTime;
                _db.Entry(row).Property(r => r.ExitTime).IsModified = true;

                if (string.IsNullOrWhiteSpace(row.CooperateCd) && !string.IsNullOrWhiteSpace(req.CompanyCd))
                    row.CooperateCd = req.CompanyCd;
                if (string.IsNullOrWhiteSpace(row.CompanyName) && !string.IsNullOrWhiteSpace(req.CompanyName))
                    row.CompanyName = req.CompanyName;
                if (string.IsNullOrWhiteSpace(row.FamilyName) && !string.IsNullOrWhiteSpace(familyName))
                    row.FamilyName = familyName;
                if (string.IsNullOrWhiteSpace(row.FirstName) && !string.IsNullOrWhiteSpace(firstName))
                    row.FirstName = firstName;
                if (string.IsNullOrWhiteSpace(row.FamilyNameKana) && !string.IsNullOrWhiteSpace(familyKana))
                    row.FamilyNameKana = familyKana;
                if (string.IsNullOrWhiteSpace(row.FirstNameKana) && !string.IsNullOrWhiteSpace(firstKana))
                    row.FirstNameKana = firstKana;
                if (string.IsNullOrWhiteSpace(row.BirthYmd) && !string.IsNullOrWhiteSpace(birthYmd))
                    row.BirthYmd = birthYmd;
                if (!string.IsNullOrWhiteSpace(req.Reason))
                    row.ActionCd = req.Reason;

                row.UTantoCd = tantoCd;
                row.UTimeStamp = now;
            }

            try
            {
                await _db.SaveChangesAsync();

                try
                {
                    await WriteOperationLogAsync("G001", kind == "IN" ? "IN" : "OUT", kaisaiCd, workerCd, null, row.EntryTime, row.ExitTime, "TOK");
                }
                catch { }

                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForTempKind(kind),
                        actionCd: "A04",
                        eventCd: string.IsNullOrWhiteSpace(kaisaiCd) ? null : kaisaiCd,
                        employeeCd: workerCd,
                        cooperateCd: req.CompanyCd,
                        familyName: familyName,
                        firstName: firstName,
                        birthYmd: birthYmd,
                        entryTime: row.EntryTime,
                        exitTime: row.ExitTime,
                        reasonCd: req.Reason,
                        sCooperateKana: null,
                        sCooperateName: null,
                        sEmployeeKanas: null,
                        sEmployeeKanan: null,
                        sEmployeeKanjis: null,
                        sEmployeeKanjin: null,
                        sBirthYmd: null,
                        sEmployeeCd: null,
                        sSelect: null,
                        jStrat: null,
                        jMaisu: null,
                        tResart: "T01",
                        uTantoCd: HttpContext.Session.GetString("EMPLOYEE_CD"),
                        uTimeStamp: null
                    );
                }
                catch { }

                return Json(new { ok = true, message = kind == "IN" ? "入場登録しました" : "退場登録しました", time = kind == "IN" ? row.EntryTime ?? hhmm : row.ExitTime ?? hhmm, code = workerCd });
            }
            catch (Exception)
            {
                return Json(new { ok = false, message = "DB書き込みエラーが発生しました。係員に問い合わせてください。", time = hhmm });
            }
        }

        private static string NormalizeTime(string? time)
        {
            if (string.IsNullOrWhiteSpace(time))
                return "";

            time = time.Trim().Replace(":", "");

            if (time.Length > 4)
                time = time.Substring(0, 4);
            while (time.Length < 4)
            {
                time = "0" + time;
            }

            return time;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            await next().ConfigureAwait(false);
        }
    }
}