using Microsoft.AspNetCore.Mvc;
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

        public ScanController(AppDbContext db, IActionLogService logService) : base(logService)
        {
            _db = db;
            _logService = logService;
        }

        // 作業員ID（GM01_EMPLOYEE.EMPLOYEE_CD）は 10桁固定（数字のみ）
        private static bool IsEmployeeCode(string code)
            => Regex.IsMatch(code ?? "", @"^\d{10}$");

        private string ScreenIdForKind(string kind)
            => (kind ?? "").ToUpper() == "IN" ? "G30" : "G50";

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

            // 作業員マスタ（仮QR の場合はマスタなしでも処理を許可する）
            var emp = await _db.Employees.FirstOrDefaultAsync(x => x.EmployeeCd == workerCd);

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
                var display = emp != null ? emp.DisplayName : workerCd;
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
                    TensoYmdTime = null
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
                var display = emp != null ? emp.DisplayName : workerCd;
                return (false, "NG", "×", "DB書き込みエラーが発生しました。係員に問い合わせてください。", display);
            }

            return (true, "OK", "〇",
                kind == "IN" ? "入場記録OK" : "退場記録OK",
                emp != null ? emp.DisplayName : workerCd);
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
                // 先頭は必ず '1' または '2' を期待する。違う場合は T03 をログして NG。
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

            // 追加: 仮QR (prefix == "2") の場合、QR内イベントがセッションのイベントと一致することを必須にする
            if (qrPrefix == "2")
            {
                var sessionKaisai = HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);
                if (string.IsNullOrWhiteSpace(overrideKaisaiFromQr) || !string.Equals(overrideKaisaiFromQr, sessionKaisai))
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

            // 仮QR（overrideKaisaiFromQr がある）を入場で利用することは許可しない
            if (!string.IsNullOrWhiteSpace(overrideKaisaiFromQr) && kind == "IN")
            {
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForKind(kind),
                        actionCd: "A04",
                        eventCd: !string.IsNullOrWhiteSpace(overrideKaisaiFromQr) ? overrideKaisaiFromQr : HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd),
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

            // 10桁チェック（不正なら従来メッセージで NG） — ログ ACTION_CD=T03
            if (qrPrefix != "2" && !IsEmployeeCode(workerCd))
            {
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForKind(kind),
                        actionCd: "A04",
                        eventCd: HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd),
                        employeeCd: workerCd,
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
                    message = "QRコードが読めません。係員に相談して下さい。（QR形式不正）",
                    code = workerCd,
                    time = hhmm
                });
            }

            // 画面から渡された KaisaiCd（あれば）を優先し、なければ QR の仮コードによる上書きを利用する
            var effectiveKaisai = !string.IsNullOrWhiteSpace(req.KaisaiCd) ? req.KaisaiCd : overrideKaisaiFromQr;

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

            // 当該開催の名簿情報（会社名・かな・生年月日 等はここに保持される想定）
            var entry = await _db.EntryExitLogs
                .FirstOrDefaultAsync(e => e.EmployeeCd == workerCd && e.KaisaiCd == kaisaiCd);

            // 仮QR の場合は作業員マスタが無くても許容する
            if (emp == null)
            {
                if (req.Prefix == "2")
                {
                    // 既に ENTRY_EXIT に存在すればそれを返す
                    if (entry != null)
                    {
                        return Json(new
                        {
                            ok = true,
                            code = workerCd,
                            cooperateCd = entry.CooperateCd ?? "",
                            companyKana = "",
                            companyName = entry.CompanyName ?? "",
                            workerFamilyKana = entry.FamilyNameKana ?? "",
                            workerFirstKana = entry.FirstNameKana ?? "",
                            workerFamilyName = entry.FamilyName ?? "",
                            workerFirstName = entry.FirstName ?? "",
                            workerKana = $"{entry.FamilyNameKana ?? ""} {entry.FirstNameKana ?? ""}",
                            workerName = (entry.FamilyName ?? "") + (entry.FirstName != null ? (" " + entry.FirstName) : "") ,
                            birthYmd = entry.BirthYmd ?? ""
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
                        workerKana = workerCd,
                        workerName = workerCd,
                        birthYmd = ""
                    });
                }

                return Json(new { ok = false, message = "作業員マスタに登録がありません" });
            }

            if (entry == null)
            {
                return Json(new { ok = false, message = "名簿情報が存在しません（当該開催）" });
            }

            // 必須項目チェック（不足していればエラーで返す）
            if (string.IsNullOrWhiteSpace(emp.CooperateCd)
                || string.IsNullOrWhiteSpace(entry.CompanyName)
                || string.IsNullOrWhiteSpace(entry.FamilyNameKana)
                || string.IsNullOrWhiteSpace(entry.FirstNameKana)
                || string.IsNullOrWhiteSpace(entry.BirthYmd))
            {
                return Json(new { ok = false, message = "名簿に必要な項目が不足しています" });
            }

            // 返却：姓・名を分けて返す（後方互換のため workerKana/workerName も含める）
            return Json(new
            {
                ok = true,
                code = workerCd,
                cooperateCd = emp.CooperateCd,
                companyKana = "", // DBに会社カナ列が見当たらないため空文字。必要ならマスタ追加してください。
                companyName = entry.CompanyName,
                workerFamilyKana = entry.FamilyNameKana,
                workerFirstKana = entry.FirstNameKana,
                workerFamilyName = emp.FamilyName,
                workerFirstName = emp.FirstName,
                // 互換
                workerKana = $"{entry.FamilyNameKana} {entry.FirstNameKana}",
                workerName = emp.DisplayName,
                birthYmd = entry.BirthYmd
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
            public string? BirthDate { get; set; } // yyyy-MM-dd expected from input[type=date]
            public string? ExcludeDate { get; set; }
            public string? Reason { get; set; }
            public string? KaisaiCd { get; set; }
            // 画面でセットされた作業員コード（必須）
            public string? WorkerCd { get; set; }
            // QR種別（'1'=通常QR, '2'=仮QR）を画面から渡せるようにする
            public string? QrPrefix { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> TempInput(string kind)
        {
            var kaisaiCd = HttpContext.Session.GetString("CurrentKaisaiCd");
            var kindUpper = (kind ?? "").ToUpper();

            if (string.IsNullOrEmpty(kaisaiCd))
            {
                // イベント選択に戻るログ（ACTION_CD：A02）
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

                TempData["Error"] = "イベントが確定していません。";
                return RedirectToAction("Index", "EventSelection");
            }

            var ev = await _db.KaisaiEvents
                .FirstOrDefaultAsync(x => x.KaisaiCd == kaisaiCd);

            if (ev == null)
            {
                // イベント選択に戻るログ（ACTION_CD：A02）
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

                TempData["Error"] = "イベント情報が取得できません。";
                return RedirectToAction("Index", "EventSelection");
            }

            ViewBag.Event = ev;

            // --- ここでセッションの開催コードを ViewBag にセットする ---
            ViewBag.CurrentKaisaiCd = kaisaiCd;

            ViewBag.LoginDisplay = $"{HttpContext.Session.GetString("BRANCH_CD")}-{HttpContext.Session.GetString("EMPLOYEE_CD")}";

            kind = kindUpper;
            if (kind != "IN" && kind != "OUT")
            {
                // イベント選択に戻るログ（ACTION_CD：A02）
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

            ViewBag.Kind = kind;
            ViewBag.ModeLabel = kind == "IN" ? "入場" : "退場";
            ViewBag.Title = "入退場登録（臨時対応窓口）";

            return View();
        }

        public async Task<IActionResult> TempRecord([FromBody] TempRecordRequest req)
        {
            var now = DateTime.Now;
            var hhmm = now.ToString("HHmm");

            // Require company code and worker code (use screen-provided worker code)
            if (req == null || string.IsNullOrWhiteSpace(req.CompanyCd) || string.IsNullOrWhiteSpace(req.WorkerCd))
            {
                return Json(new { ok = false, message = "必須項目が未入力です", time = hhmm });
            }

            // イベントコード（画面から渡されたものを優先、なければセッション）
            var kaisaiCd = !string.IsNullOrWhiteSpace(req.KaisaiCd)
                ? req.KaisaiCd
                : HttpContext.Session.GetString(SessionKeyCurrentKaisaiCd);

            if (string.IsNullOrWhiteSpace(kaisaiCd))
            {
                return Json(new { ok = false, message = "イベントが確定していません", time = hhmm });
            }

            var kind = (req.Kind ?? "IN").ToUpper();

            // Prepare worker code and split names
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

            // birth
            string? birthYmd = null;
            if (!string.IsNullOrWhiteSpace(req.BirthDate))
            {
                if (DateTime.TryParse(req.BirthDate, out var bd))
                {
                    birthYmd = bd.ToString("yyyyMMdd");
                }
            }

            // 入退場時刻処理（画面は HHmm または HH:mm）
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

            // 既存レコード確認
            var row = await _db.EntryExitLogs
                .FirstOrDefaultAsync(e => e.KaisaiCd == kaisaiCd && e.EmployeeCd == workerCd);

            if (kind == "IN")
            {
                if (row == null)
                {
                    // Type の決定: '1'=通常QR, '2'=仮QR -> 保存値は '1' / '5' / '9'
                    string typeVal;
                    if (req.QrPrefix == "1") typeVal = "1";
                    else if (req.QrPrefix == "2") typeVal = "5";
                    else if (!string.IsNullOrWhiteSpace(workerCd) && workerCd.StartsWith("T")) typeVal = "9";
                    else typeVal = "9";

                    // 新規作成（画面で与えられた作業員コードを使用）
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
                        TensoYmdTime = null
                    };
                    _db.EntryExitLogs.Add(row);
                }
                else
                {
                    // 既存レコードがあれば、EntryTime は上書きしない方針
                    if (string.IsNullOrWhiteSpace(row.EntryTime))
                    {
                        row.EntryTime = entryTime;
                        _db.Entry(row).Property(r => r.EntryTime).IsModified = true;
                    }

                    // 必要なら他項目を補完
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
                    // 事由（画面の選択）を ActionCd に保存（上書きして問題ないと判断）
                    if (!string.IsNullOrWhiteSpace(req.Reason))
                        row.ActionCd = req.Reason;
                }
            }
            else // OUT
            {
                // 退場は既存レコードがないと不可
                if (row == null)
                {
                    return Json(new { ok = false, message = "入場記録がないため退場登録できません。", time = hhmm });
                }

                // 退場時、入場時刻が空ならエラー
                if (string.IsNullOrWhiteSpace(row.EntryTime))
                {
                    return Json(new { ok = false, message = "入場時刻が登録されていないため退場登録できません。", time = hhmm });
                }

                // ExitTime は常に更新
                row.ExitTime = exitTime;
                _db.Entry(row).Property(r => r.ExitTime).IsModified = true;

                // 補完
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
                // 事由を ActionCd に保存（退場でも記録）
                if (!string.IsNullOrWhiteSpace(req.Reason))
                    row.ActionCd = req.Reason;
            }

            try
            {
                await _db.SaveChangesAsync();

                // 操作ログ
                try
                {
                    await WriteOperationLogAsync("G001", kind == "IN" ? "IN" : "OUT", kaisaiCd, workerCd, null, row.EntryTime, row.ExitTime, "TOK");
                }
                catch { }

                // アクションログ
                try
                {
                    await _logService.ActionLogSaveAsync(
                        screenId: ScreenIdForKind(kind),
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
            //修正 2026.02.20 Takada ；catch (Exception ex)からワーニング除外
            catch (Exception)
            {
                return Json(new { ok = false, message = "DB書き込みエラーが発生しました。係員に問い合わせてください。", time = hhmm });
            }
        }

        // ヘルパー：時刻フィールドを HHmm 形式に正規化
        private static string NormalizeTime(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var s = input.Trim();
            if (s.Contains(":")) s = s.Replace(":", "");
            // 最低4文字に左パッド
            s = s.PadLeft(4, '0');
            if (s.Length > 4) s = s.Substring(0, 4);
            return s;
        }
    }
}