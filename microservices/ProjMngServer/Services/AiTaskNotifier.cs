using System.Net.Http.Json;
using System.Text;

using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 끝난 작업을 메일로 알린다 (설계 8-2).
/// </summary>
/// <remarks>
/// <para>
/// <b>실행기가 아니라 서버가 보낸다.</b> 실행기가 보내면 작업은 끝났는데
/// 메일 때문에 실행 슬롯이 묶이고, 메일 실패가 실행 실패처럼 보인다 —
/// 그 둘은 다른 일이다.
/// </para>
/// <para>
/// <b>큐가 아니라 직발송(<c>/emails/send</c>)을 쓴다.</b> 큐 쪽
/// (<c>/notification/email</c>)은 결과를 「넣었다」까지만 알 수 있고, 무엇보다
/// 우리가 피하기로 한 <c>run_script</c> 큐를 다시 탄다(설계 6.2).
/// 「결과를 메일로 받겠다」는 기능에서 보내졌는지 모르면 절반만 있는 것이다.
/// </para>
/// <para>
/// <b>못 보내도 작업 상태를 바꾸지 않는다.</b> 사유만 남기고 화면이 그것을
/// 말한다 — 배포 도구가 정한 규칙과 같다(보고에 실패해도 배포는 계속한다).
/// </para>
/// </remarks>
public sealed class AiTaskNotifier(
    IConfiguration configuration, IHttpClientFactory http,
    AiRunSummaryWriter summaries, ILogger<AiTaskNotifier> logger)
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>
    /// 알림 서비스 주소. 같은 장비 안이라 루프백이다.
    /// </summary>
    private readonly string _notifyUrl =
        configuration["AiTasks:NotifyUrl"] is { Length: > 0 } u ? u : "http://127.0.0.1:5460";

    /// <summary>화면에서 그 건을 열 수 있는 주소. 메일 끝에 붙인다.</summary>
    private readonly string? _portalUrl = configuration["AiTasks:PortalUrl"];

    /// <summary>
    /// 결과 앱푸시를 <b>시킨 사람 말고도</b> 받는 역할. 비우면 시킨 사람에게만 간다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 있나.</b> 결과 알림은 오래도록 <c>notify_to</c> 또는 <c>cre_id</c>
    /// 한 사람에게만 갔다. 그런데 실제로 이 포털에서 AI 작업을 살피는 사람은
    /// 시킨 사람 하나가 아니라 <b>시스템 관리자 전원</b>이다 — 실행기가 멈췄는지,
    /// 남의 지시가 실패로 끝났는지를 알아야 하는 쪽이 그들이다. 받는 길이
    /// 없으면 「AI 작업 지시」 화면을 스스로 열어 보기 전까지 아무것도 모른다.
    /// </para>
    /// <para>
    /// <b>역할을 사람으로 푸는 것은 알림 서비스다</b>(<c>SendPushDto.Roles</c>) —
    /// 역할표는 포털 계정 DB(<c>jsiniportal</c> 의 <c>scom.role_accounts</c>)에 있고
    /// 프로젝트관리는 다른 데이터베이스를 봐서 여기서는 조회조차 못 한다.
    /// 시킨 사람이 그 역할이어도 <b>한 번만</b> 간다(저쪽이 겹친 주인을 접는다).
    /// </para>
    /// <para>
    /// 「요청이 올라왔다」 알림(<see cref="AiRequestAlerter"/>)과 <b>목록을 따로
    /// 둔다.</b> 그쪽은 올라온 요청을 <b>처리할</b> 사람이고 이쪽은 끝난 일을
    /// <b>지켜볼</b> 사람이라, 한 값으로 묶으면 한쪽을 줄이려다 다른 쪽이 멎는다.
    /// </para>
    /// </remarks>
    private readonly string[] _resultRoles =
        configuration.GetSection("AiTasks:ResultNotifyRoles").Get<string[]>()
        ?? ["SYSTEM_ADMINISTRATOR"];

    /// <summary>
    /// 그 건 하나를 펴 놓는 화면의 <b>상대 주소</b>
    /// (<c>web/.../Pages/AiTaskViewPage.razor</c> 의 <c>@@page</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>앱알림과 메일이 같은 한 줄을 쓴다.</b> 두 벌로 두면 한쪽만 고쳐져
    /// 어긋나고, 어긋나는 쪽은 언제나 「알림을 눌렀더니 엉뚱한 화면」이다.
    /// </para>
    /// <para>
    /// 질의 문자열(<c>?task=</c>)이 아니라 경로에 번호를 싣는다. 포털의 탭
    /// 줄이 <b>질의 문자열을 뗀 경로로</b> 탭을 가르므로, 질의로 실으면 여러
    /// 건을 열어도 탭이 하나만 선다.
    /// </para>
    /// </remarks>
    private static string TaskUrl(long taskKey) => $"/projmng/ai/task/{taskKey}";

    /// <summary>
    /// 기록에 적을 <b>받는 쪽</b> 한 줄. 사람 하나가 아니라 「시킨 사람 + 지켜보는
    /// 역할」이 되었으므로, 로그가 여전히 아이디 하나만 말하면 <b>역할로 간 몫이
    /// 통째로 안 보인다</b> — 「왜 안 왔나」를 되짚을 때 가장 먼저 보는 줄이다.
    /// </summary>
    private string Who(Target target)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(target.ToUser))
        {
            parts.Add(target.ToUser!);
        }

        if (_resultRoles.Length > 0)
        {
            parts.Add($"역할 {string.Join("·", _resultRoles)}");
        }

        return parts.Count == 0 ? "(없음)" : string.Join(" + ", parts);
    }

    /// <summary>
    /// 이 실행의 <b>앱푸시</b>를 보낸다. <b>끝난 직후 맨 먼저 부르는 걸음</b>이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [왜 메일과 갈라 놓았나 — 푸시가 요약을 기다리고 서 있었다]
    /// </para>
    /// <para>
    /// 예전에는 한 함수가 메일과 푸시를 차례로 보냈고, 그 함수는 끝난 실행의
    /// <b>처리 요약과 자동 제목이 다 만들어진 뒤에야</b> 불렸다
    /// (<see cref="AiRunSummaryWriter"/> · <see cref="AiTaskTitler"/>, 둘 다 모델을
    /// 부른다). 그래서 작업이 끝난 시각과 푸시가 나간 시각 사이가
    /// <b>운영 실측으로 중앙 13초 · 상위 10% 45초 · 최대 177초</b>였다
    /// (<c>scom.push_send_logs</c> 와 <c>ai_task_run.finished_at</c> 대조, 2026-09-25).
    /// 받는 사람에게 그것은 그냥 <b>「푸시가 느려졌다」</b>이다.
    /// </para>
    /// <para>
    /// <b>푸시에 실리는 것은 상태와 제목뿐이다</b> — 요약은 메일 본문에만 들어간다.
    /// 기다릴 까닭이 없으므로 푸시를 앞으로 빼고, 요약을 싣는 메일만 뒤에 남긴다.
    /// </para>
    /// <para>
    /// 대신 제목은 <b>AI 가 다시 짓기 전의 것</b>이 실린다. 그것은 사람이 적은
    /// 제목이거나(그대로가 맞다) 지시문의 첫 제목 줄에서 뽑은 것이라
    /// (<c>AiTaskService.NormalizeAsync</c>) 알림 한 줄로는 충분히 읽힌다.
    /// 다시 지어진 제목은 <b>누르고 들어간 화면</b>과 목록에 있다.
    /// </para>
    /// </remarks>
    public async Task SendPushAsync(long runKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            // **이 실행의 알림 사유를 여기서 비운다.** 푸시가 알림의 첫 걸음이라
            // 앞선 실행이 남긴 사유를 지우는 자리도 여기다 — 뒤따르는 메일은
            // 덧붙이기만 한다(<see cref="MarkAsync"/> 의 reset).
            await MarkAsync(db, runKey, null, reset: true);

            var target = await LoadAsync(db, runKey, wantEmail: false, ct);

            if (target is null)
            {
                return;
            }

            var row = target.Row;

            // 「받는 사람」에 남의 메일 주소를 적은 건은 **그 사람에게는** 푸시로
            // 보낼 곳이 없다 — 우리가 아는 것은 포털 아이디뿐이고 그 칸은 주소다.
            // 그래도 지켜보는 역할(<see cref="_resultRoles"/>)이 있으면 그쪽으로는
            // 간다. 둘 다 없을 때만 조용히 끝낸다.
            var owners = string.IsNullOrWhiteSpace(target.ToUser)
                ? Array.Empty<object>()
                : new object[] { new { ownerType = "jsini", ownerKey = target.ToUser } };

            if (owners.Length == 0 && _resultRoles.Length == 0)
            {
                return;
            }

            var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"{_notifyUrl.TrimEnd('/')}/notifications/push")
            {
                Content = JsonContent.Create(new
                {
                    owners,

                    // **시킨 사람 말고 지켜보는 사람도 받는다**(<see cref="_resultRoles"/>).
                    // 겹치면 저쪽이 접으므로 여기서 덜어 내지 않는다.
                    roles = _resultRoles,

                    message = new
                    {
                        title = "AI 작업 끝남",
                        body = $"[{StatusText(row.TaskStatus)}] {row.Title}",
                        // **그 건 하나를 펴 놓는 주소다.** 예전에는 목록
                        // (`/projmng/ai/tasks`)으로 보냈고, 누른 사람이
                        // 편집기를 받은 뒤 목록에서 그 건을 눈으로 다시
                        // 찾아야 했다. 메일의 단추도 같은 주소를 쓴다.
                        url = TaskUrl(row.TaskKey),

                        // **아이콘에 지시한 사람의 얼굴을 띄운다.**
                        //
                        // 아이디만 넘기고 사진은 알림 서비스가 푼다 — 이 DB
                        // (projmng)에는 사람의 사진도 메일 주소도 없다(위 주석).
                        // 사진이 없는 계정이면 저쪽이 사람 형상 그림자를 쓴다.
                        //
                        // **받는 사람(`ToUser`)이 아니라 `CreId` 다.** 지금은
                        // 둘이 같지만, 「받는 사람」 칸을 적어 남에게 보내게
                        // 되면 갈린다 — 그때 아이콘이 답해야 하는 것은
                        // 「누가 시킨 일인가」 쪽이다.
                        iconOwnerKey = row.CreId?.Trim(),

                        // **두 시간 지나면 배달하지 않는다.**
                        //
                        // 이 알림이 이 포털에서 가장 잦다 — 최근 일주일
                        // 발송 254건 중 241건이 이것이다. 웹푸시는 브라우저가
                        // 꺼져 있으면 푸시 서비스가 들고 기다렸다가 다시 켤 때
                        // 한꺼번에 내보내므로, 수명을 안 주면(라이브러리 기본
                        // 28일) 며칠 쉬고 온 사람의 화면에 수십 개가 쏟아진다.
                        //
                        // 끝난 작업은 작업 화면과 「내 알림함」에 그대로
                        // 있으므로 배달을 포기해도 잃는 것이 없다. 사정은
                        // docs/push-delivery.md 참고.
                        ttlSeconds = 7200
                    }
                }),
            };

            req.Headers.Add("X-User-Id", "AI_TASK");

            using var res = await client.SendAsync(req, ct);

            if (res.IsSuccessStatusCode)
            {
                // **「보냈다」와 「보낼 곳이 없었다」는 다른 것이다.**
                // 2xx 만 보고 넘기면 후자가 여기서 사라진다 —
                // 까닭은 <see cref="PushOutcome"/> 머리말에 있다.
                var body = await res.Content.ReadAsStringAsync(ct);
                var notSent = PushOutcome.NotSent(res.StatusCode, body);

                if (notSent is null)
                {
                    logger.LogInformation(
                        "작업 {TaskKey} 결과를 {To} 에게 PWA로 보냈습니다.", row.TaskKey, Who(target));
                }
                else
                {
                    await MarkAsync(db, runKey, $"PWA 미발송: {notSent}".Trim(), reset: false);
                    logger.LogWarning(
                        "작업 {TaskKey} 결과 PWA 알림이 {To} 에게 한 건도 가지 않았습니다: {Why}",
                        row.TaskKey, Who(target), notSent);
                }
            }
            else
            {
                var why = await res.Content.ReadAsStringAsync(ct);
                await MarkAsync(db, runKey, $"PWA 실패 (HTTP {(int)res.StatusCode}): {Reason(why)}".Trim(), reset: false);
            }
        }
        catch (Exception ex)
        {
            // **작업 상태는 건드리지 않는다.** 알림이 안 갔다고 성공한 일이
            // 실패가 되면 안 된다.
            logger.LogWarning(ex, "결과 앱푸시를 보내지 못했습니다 (run {RunKey}).", runKey);

            // **덮어쓴다.** 푸시는 이 실행의 첫 걸음이라 여기서 나는 예외는 언제나
            // 이번 사유다 — 이어 붙이면 위의 비우기(reset)에 닿기도 전에 터진 갈래
            // (DB 가 안 열리는 등)에서 **앞 실행의 사유 뒤에 이번 것이 붙는다.**
            await TryMarkAsync(runKey, ex.Message, reset: true);
        }
    }

    /// <summary>
    /// 이 실행의 결과를 <b>메일로</b> 보낸다. <b>보낼 이유가 없으면 조용히 끝낸다.</b>
    /// </summary>
    /// <remarks>
    /// <b>이것만 요약을 기다린다.</b> 메일 본문의 「무엇을 했다나」 칸이 처리 요약을
    /// 싣기 때문이다. 앱푸시는 그것을 안 쓰므로 먼저 나간다
    /// (<see cref="SendPushAsync"/>).
    /// </remarks>
    public async Task SendMailAsync(long runKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            var target = await LoadAsync(db, runKey, wantEmail: true, ct);

            if (target is null)
            {
                return;
            }

            var row = target.Row;
            var to = target.To;

            // 사람이 적은 값이 주소가 아니면 **여기서 말한다.** 저쪽까지 갔다
            // 오면 「받는 사람이 없습니다」가 되어 어느 칸이 틀렸는지가 흐려진다.
            if (!string.IsNullOrWhiteSpace(to)
                && to.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Any(one => !System.Net.Mail.MailAddress.TryCreate(one, out _)))
            {
                await MarkAsync(db, runKey,
                    $"「받는 사람」이 메일 주소가 아닙니다: {to} — 비워 두면 요청한 사람에게 갑니다.",
                    reset: false);
                return;
            }

            // **처리 요약을 만드는 자리는 여기가 아니다.** 완료 처리가 알림과
            // 무관하게 먼저 만들어 적어 둔다(<c>AiRunService.SummarizeThenNotifyAsync</c>
            // → <see cref="AiRunSummaryWriter"/>). 그 자리가 여기로 돌아오면 요약의
            // 수명이 위 관문들(받기 꺼짐·받는 사람 없음·주소 틀림)에 매달려서
            // **알림을 끈 사람에게만 요약이 없는** 상태가 된다.
            //
            // 아래 한 줄은 **되읽기이고, 없을 때의 재청일 뿐**이다 — 앞에서 만들다
            // 실패했더라도 메일 본문의 「무엇을 했다나」 칸은 채워 보내는 편이 낫다.
            // 그래서 관문 밑에 있어도 위의 걱정에 걸리지 않는다.
            var summary = AiResultSummary.Parse(row.SummaryText)
                ?? await summaries.EnsureAsync(runKey, ct: ct);

            var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"{_notifyUrl.TrimEnd('/')}/emails/send")
            {
                Content = JsonContent.Create(new
                {
                    to,
                    toUser = target.ToUser,
                    subject = $"[AI 작업] {row.Title} — {StatusText(row.TaskStatus)}",
                    body = Body(row, summary),
                    // 저쪽 DTO 의 속성 이름은 `Html` 이다. `isHtml` 로 적으면
                    // 붙지 않고 조용히 기본값이 쓰인다 — 그러면 본문이 태그
                    // 그대로 보인다.
                    html = true,
                }),
            };

            // 서비스 간 직접 호출이라 자기 이름을 적어 보낸다
            req.Headers.Add("X-User-Id", "AI_TASK");

            using var res = await client.SendAsync(req, ct);

            if (res.IsSuccessStatusCode)
            {
                logger.LogInformation("작업 {TaskKey} 결과를 {To} 에게 메일로 보냈습니다.", row.TaskKey, to ?? target.ToUser);
            }
            else
            {
                var why = await res.Content.ReadAsStringAsync(ct);
                await MarkAsync(db, runKey,
                    $"메일 실패 (HTTP {(int)res.StatusCode}): {Reason(why)}".Trim(), reset: false);
            }
        }
        catch (Exception ex)
        {
            // **작업 상태는 건드리지 않는다.** 메일이 안 갔다고 성공한 일이
            // 실패가 되면 안 된다.
            logger.LogWarning(ex, "결과 메일을 보내지 못했습니다 (run {RunKey}).", runKey);

            // 메일은 뒤 걸음이라 앞이 남긴 사유를 지우지 않는다.
            await TryMarkAsync(runKey, ex.Message, reset: false);
        }
    }

    /// <summary>알림 한 통을 만들 때 필요한 것 — 읽어 온 줄과 받는 곳.</summary>
    /// <param name="Row">실행·작업을 한 줄로 편 것.</param>
    /// <param name="To">사람이 적은 메일 주소. 비어 있으면 <paramref name="ToUser"/> 가 찬다.</param>
    /// <param name="ToUser">요청한 사람의 포털 아이디. 주소를 적었으면 <c>null</c>.</param>
    private sealed record Target(MailRow Row, string? To, string? ToUser);

    /// <summary>
    /// 보낼 줄을 읽고 <b>보낼 이유가 있는지</b>까지 가린다. 없으면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <b>두 길(푸시·메일)이 같은 관문을 쓴다.</b> 갈래마다 따로 적으면 한쪽만
    /// 고쳐져 「메일은 왔는데 푸시는 안 온다」가 된다 — 받는 사람이 가장
    /// 설명하기 어려워하는 갈래다.
    /// </remarks>
    /// <param name="wantEmail">메일 길인가. 아니면 앱푸시 길이다.</param>
    private async Task<Target?> LoadAsync(
        NpgsqlConnection db, long runKey, bool wantEmail, CancellationToken ct)
    {
        var row = await db.QuerySingleOrDefaultAsync<MailRow>(new CommandDefinition("""
            SELECT t.task_key      AS TaskKey,
                   t.title         AS Title,
                   t.notify_email  AS NotifyEmail,
                   t.notify_pwa    AS NotifyPwa,
                   t.notify_to     AS NotifyTo,
                   t.notify_when   AS NotifyWhen,
                   t.task_status   AS TaskStatus,
                   t.duration_ms   AS DurationMs,
                   t.pushed_commit AS PushedCommit,
                   t.cre_id        AS CreId,
                   b.target_nm     AS TargetNm,
                   r.seq           AS Seq,
                   r.exit_code     AS ExitCode,
                   r.result_text   AS ResultText,
                   r.diff_stat     AS DiffStat,
                   r.error_summary AS ErrorSummary,
                   r.git_branch    AS GitBranch,
                   r.instruction   AS Instruction,
                   r.summary_text  AS SummaryText
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t   ON t.task_key = r.task_key
              LEFT JOIN projmng.ai_target b ON b.target_key = t.target_key
             WHERE r.run_key = @runKey
            """, new { runKey }, cancellationToken: ct));

        if (row is null)
        {
            return null;
        }

        if (!(wantEmail ? row.NotifyEmail : row.NotifyPwa))
        {
            return null;
        }

        // 「언제 보내나」를 본다. 실패만 받겠다고 한 사람에게 성공 메일을
        // 보내면 그 옵션이 있으나 마나다.
        var ok = row.TaskStatus == "succeeded";

        if ((row.NotifyWhen == "on_success" && !ok)
            || (row.NotifyWhen == "on_failure" && ok))
        {
            return null;
        }

        // **주소와 아이디를 구분해서 보낸다.**
        //
        // 이 DB(projmng)에는 사람의 메일 주소가 없다 — 계정은 scom 에 있고
        // 그쪽은 알림 서비스의 DB 다. 그래서 「요청한 사람에게」는 아이디를
        // toUser 로 넘겨 저쪽에서 풀게 한다.
        //
        // 예전에는 아이디(cre_id)를 그대로 to 에 실었다. 주소 꼴이 아니라서
        // 알림 서비스가 걸러 버렸고 **「받는 사람」을 비워 둔 건은 한 통도
        // 나가지 않았다** — notify_error 에 HTTP 400 만 쌓였다.
        var to = row.NotifyTo?.Trim();
        var toUser = string.IsNullOrWhiteSpace(to) ? row.CreId?.Trim() : null;

        if (string.IsNullOrWhiteSpace(to) && string.IsNullOrWhiteSpace(toUser))
        {
            await MarkAsync(db, runKey, "받는 사람을 알 수 없습니다.", reset: false);
            return null;
        }

        return new Target(row, to, toUser);
    }

    /// <summary>
    /// 사유를 남겨 보되 <b>남기지 못해도 그만인</b> 자리. 이미 예외를 처리하는
    /// 중이라 여기서 또 던지면 남길 곳이 없다.
    /// </summary>
    private async Task TryMarkAsync(long runKey, string? error, bool reset)
    {
        try
        {
            await using var db = new NpgsqlConnection(_connectionString);
            await MarkAsync(db, runKey, error, reset);
        }
        catch
        {
            // 로그로 끝낸다.
        }
    }

    /// <summary>
    /// 실패 응답에서 사람이 읽을 한 줄만 꺼낸다. 못 꺼내면 빈 문자열이다 —
    /// <b>JSON 덩어리를 그대로 화면에 올리지 않는다.</b>
    /// </summary>
    private static string Reason(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("message", out var m)
                && m.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // JSON 이 아니면 아래에서 앞부분만 자른다.
        }

        return body.Trim()[..Math.Min(body.Trim().Length, 200)];
    }

    /// <summary>
    /// 이 실행의 알림 사유(<c>ai_task.notify_error</c>)를 적는다.
    /// </summary>
    /// <remarks>
    /// <b>걸음이 둘이라 덮어쓰기와 덧붙이기를 가른다.</b> 앱푸시가 먼저 나가고
    /// (<see cref="SendPushAsync"/>) 메일이 나중에 나가는데, 뒤엣것이 그냥 덮으면
    /// <b>「푸시는 안 갔고 메일은 갔다」가 아무 데도 안 남는다.</b> 그래서 첫
    /// 걸음만 <paramref name="reset"/> 으로 비우고 나머지는 <c> / </c> 로 잇는다.
    /// </remarks>
    /// <param name="reset">
    /// 참이면 앞의 것을 지우고 이 값만 남긴다(새 실행의 첫 걸음).
    /// 거짓이면 뒤에 잇는다 — <paramref name="error"/> 가 비면 아무것도 안 한다.
    /// </param>
    private static async Task MarkAsync(
        NpgsqlConnection db, long runKey, string? error, bool reset)
    {
        var clipped = string.IsNullOrWhiteSpace(error)
            ? null
            : error[..Math.Min(error.Length, 480)];

        if (reset)
        {
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task
                   SET notify_error = @error
                 WHERE task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
                """, new { runKey, error = clipped });
            return;
        }

        if (clipped is null)
        {
            // 잘 갔다는 것은 적지 않는다 — 앞 걸음이 남긴 사유를 지우게 된다.
            return;
        }

        // **이미 적힌 말은 다시 적지 않는다.** 두 걸음이 같은 관문에 걸리는 갈래가
        // 있다(받는 사람을 알 수 없음 — 푸시도 메일도 같은 말을 한다). 그대로
        // 이으면 화면에 같은 문장이 두 번 뜬다.
        //
        // `LIKE` 가 아니라 `POSITION` 이다 — 사유에 `%` 나 `_` 가 섞여 들어오면
        // (HTTP 응답을 그대로 실어 나르는 자리가 있다) 패턴으로 읽혀 엉뚱하게 맞는다.
        await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET notify_error = CASE
                     WHEN POSITION(@error IN COALESCE(notify_error, '')) > 0 THEN notify_error
                     ELSE LEFT(CONCAT_WS(' / ', NULLIF(notify_error, ''), @error), 480)
                   END
             WHERE task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
            """, new { runKey, error = clipped });
    }

    /// <summary>
    /// 메일 한 통. <b>맨 위 요약만 읽어도 다 알 수 있게</b> 짠다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 메일은 대개 휴대폰에서 열린다.</b> 「빠른 지시」 화면의 존재
    /// 이유가 그것이고, 그 화면에서 보낸 건은 답을 오직 메일로만 본다.
    /// 그런데 아래로 스크롤해야 알 수 있는 메일은 사실상 안 읽힌다 —
    /// 제목과 첫 화면에서 끝나야 한다.
    /// </para>
    /// <para>
    /// 그래서 <b>요약이 먼저고 전문이 뒤</b>다. 요약에는 다섯만 담는다.
    /// </para>
    /// <list type="number">
    ///   <item><description><b>어떻게 됐나</b> — 완료·실패와 대상·시간</description></item>
    ///   <item><description><b>무엇을 시켰나</b> — 지시문 앞부분.
    ///   <b>이것이 한동안 빠져 있었다</b>: 답만 있고 물음이 없으면, 며칠 뒤에
    ///   열어 본 사람은 이 메일이 무엇에 대한 것인지 알 수 없다</description></item>
    ///   <item><description><b>무엇을 했다나</b> — <b>AI 가 다시 써 준 답.</b>
    ///   예전에는 결과문의 앞 몇 줄을 그대로 떴는데, 실행기의 답은 「먼저 …를
    ///   찾아보겠습니다」로 시작하고 결론은 끝에 있는 일이 잦았다 —
    ///   <b>가장 잘 보여야 할 자리에 서론이 앉았다.</b>
    ///   (<see cref="AiResultSummarizer"/>. 정리에 실패하면 옛 방식으로 돌아간다)</description></item>
    ///   <item><description><b>무엇이 바뀌었나</b> — 파일 수와 증감</description></item>
    ///   <item><description><b>내가 할 일이 있나</b> — 배포됐다 · 커밋만 있다 ·
    ///   사람이 봐야 한다</description></item>
    /// </list>
    /// <para>
    /// <b>로그 전문은 붙이지 않는다.</b> 수천 줄이 메일함에 쌓이고, 마스킹을
    /// 거친 것이라도 메일은 한 번 나가면 회수할 수 없다. 링크로 보낸다.
    /// </para>
    /// <para>
    /// <b>평문이 아니라 HTML 로 보낸다 — 보고서 꼴이다.</b> 평문일 때는
    /// <c>■</c> 와 <c>═══</c> 로 칸을 흉내 냈는데, 메일 앱마다 글꼴 폭이 달라
    /// 그 줄맞춤이 다 어긋났다. 특히 휴대폰에서는 한 줄이 접히면서 라벨과 값이
    /// 섞여 <b>무엇이 답이고 무엇이 다음 항목인지</b>가 사라졌다.
    /// 표와 색이 그 일을 대신하면 접혀도 칸이 남는다.
    /// </para>
    /// <para>
    /// <b>스타일은 전부 인라인이고 바깥틀은 표다.</b> 메일 앱은 <c>&lt;style&gt;</c>
    /// 블록과 <c>flex</c>·<c>grid</c> 를 지우거나 무시한다 — 웹에서 쓰던 방식으로
    /// 짜면 열자마자 칸이 다 풀린다. 폭은 640px 로 묶고 그 안에서만 접히게 한다.
    /// </para>
    /// <para>
    /// <b>결과문·지시문은 반드시 HTML 이스케이프한다.</b> AI 의 답에는 코드가
    /// 섞여 들어오고, 거기 <c>&lt;div&gt;</c> 하나만 있어도 그 아래 보고서가
    /// 통째로 무너진다. 마스킹 → 이스케이프 → 붙이기 순서를 지킨다.
    /// </para>
    /// </remarks>
    private string Body(MailRow r, AiResultSummary? summary)
    {
        var accent = Accent(r.TaskStatus);
        var tint = Tint(r.TaskStatus);
        var sb = new StringBuilder();

        // ── 바깥틀 · 머리 ───────────────────────────────────
        sb.Append($"""
            <!DOCTYPE html>
            <html lang="ko">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Esc(Headline(r))}</title>
            </head>
            <body style="margin:0;padding:0;background:#f1f3f5;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f1f3f5;">
            <tr><td align="center" style="padding:16px 10px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:640px;background:#ffffff;border:1px solid #e3e6ea;border-radius:10px;font-family:-apple-system,BlinkMacSystemFont,'Apple SD Gothic Neo','Malgun Gothic',sans-serif;color:#212529;">
            <tr><td style="background:{accent};padding:18px 20px;border-radius:9px 9px 0 0;">
              <div style="font-size:11px;letter-spacing:.06em;color:#ffffff;opacity:.85;">AI 작업 결과 보고</div>
              <div style="font-size:19px;line-height:1.35;font-weight:700;color:#ffffff;padding-top:5px;">{Esc(Headline(r))}</div>
              <div style="font-size:12.5px;color:#ffffff;opacity:.9;padding-top:7px;">{Esc(SubHead(r))}</div>
            </td></tr>
            """);

        // ── 요약 ────────────────────────────────────────────
        sb.Append("""
            <tr><td style="padding:20px 20px 0 20px;">
              <div style="font-size:11.5px;font-weight:700;color:#868e96;letter-spacing:.06em;padding-bottom:10px;">요약</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="font-size:13.5px;line-height:1.6;">
            """);

        sb.Append(SummaryRow("결과", $"""<span style="font-weight:600;">{Esc(StatusText(r.TaskStatus))}</span> · {r.Seq}차{Esc(Took(r))}"""));
        sb.Append(SummaryRow("대상", Esc(r.TargetNm)));

        // **무엇을 시켰나.** 답만 있고 물음이 없으면 며칠 뒤의 나는
        // 이 메일이 무엇에 대한 것인지 알 수 없다.
        sb.Append(SummaryRow("시킨 것", Lines(Gist(r.Instruction, lines: 3)) ?? Muted("적힌 것이 없습니다")));
        // **정리된 것이 있으면 그것을 쓴다.** 한 문장 + 점 몇 개로 나뉘어 있어
        // 라벨 하나에 여러 줄이 뭉쳐 있던 예전 꼴보다 훨씬 빨리 읽힌다.
        sb.Append(summary is not null
            ? SummaryRow("AI 의 답", Said(summary))
            : SummaryRow("AI 의 답", Lines(Gist(r.ResultText, lines: 5)) ?? Muted("아무 말 없이 끝났습니다")));

        sb.Append(SummaryRow("바뀐 것", Esc(ChangeGist(r))));

        // 「확인할 것」은 AI 가 짚은 것이고, 아래의 「할 일」은 **기계가 아는 사실**
        // (배포됐나 · 커밋만 있나)이다. 둘은 근거가 달라서 한 칸에 뭉치지 않는다.
        if (summary is { Checks.Count: > 0 })
        {
            sb.Append(SummaryRow("확인할 것", Bullets(summary.Checks)));
        }

        sb.Append("</table>");

        // **「할 일」만 칸 밖으로 낸다.** 요약에서 사람이 제일 알고 싶은 것이고,
        // 다른 줄과 같은 크기로 있으면 스쳐 지나간다.
        sb.Append($"""
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:14px;background:{tint};border-left:3px solid {accent};border-radius:0 6px 6px 0;">
                <tr><td style="padding:11px 13px;font-size:13.5px;line-height:1.55;">
                  <div style="font-size:11.5px;font-weight:700;color:#868e96;letter-spacing:.06em;">할 일</div>
                  <div style="padding-top:3px;font-weight:600;">{Esc(NextStep(r))}</div>
                </td></tr>
              </table>
            </td></tr>
            """);

        // ── 전문 ────────────────────────────────────────────
        sb.Append(Divider("전문"));

        if (!string.IsNullOrWhiteSpace(r.Instruction))
        {
            sb.Append(Block("시킨 것", SecretMask.Apply(r.Instruction)));
        }

        // 결과문이 비어 있으면 **그 사실을 적는다.** 빈 메일을 보내면
        // 받는 사람이 메일 사고로 읽는다.
        //
        // 위에 정리본을 올렸을 때는 **여기가 「원문」임을 밝힌다.** 같은 라벨이
        // 두 번 나오면 둘이 어긋나 보일 때 어느 쪽이 손댄 것인지 알 수 없다.
        var said = summary is null ? "AI 의 답" : "AI 의 답 (원문)";

        sb.Append(string.IsNullOrWhiteSpace(r.ResultText)
            ? Block(said, "(AI 가 아무 말 없이 끝났습니다. 화면에서 로그를 보십시오.)")
            : Block(said, SecretMask.Apply(r.ResultText)));

        // push 한 건은 **따로 적는다.** 같은 「완료」로 뭉개면 코드만 고친 건과
        // 운영이 바뀐 건을 구분할 수 없다(설계 9.3).
        if (!string.IsNullOrWhiteSpace(r.PushedCommit))
        {
            sb.Append(Note("배포", $"""운영에 올라갔습니다 — <span style="font-family:ui-monospace,SFMono-Regular,Consolas,monospace;">{Esc(r.PushedCommit)}</span>"""));
        }
        else if (!string.IsNullOrWhiteSpace(r.GitBranch))
        {
            sb.Append(Note("브랜치", $"""<span style="font-family:ui-monospace,SFMono-Regular,Consolas,monospace;">{Esc(r.GitBranch)}</span> (아직 올리지 않았습니다)"""));
        }

        if (!string.IsNullOrWhiteSpace(r.DiffStat))
        {
            sb.Append(Block("바뀐 파일", r.DiffStat));
        }

        if (!string.IsNullOrWhiteSpace(r.ErrorSummary))
        {
            // 오류는 **붉은 칸**으로 낸다. 전문 속에 같은 회색으로 섞여 있으면
            // 실패 메일에서 정작 사유를 못 찾는다.
            sb.Append($"""
                <tr><td style="padding:16px 20px 0 20px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#fff5f5;border:1px solid #ffc9c9;border-radius:6px;">
                    <tr><td style="padding:11px 13px;font-size:13px;line-height:1.6;color:#c92a2a;">
                      <div style="font-size:11.5px;font-weight:700;letter-spacing:.06em;">오류</div>
                      <div style="padding-top:3px;word-break:break-word;">{Esc(r.ErrorSummary)}</div>
                    </td></tr>
                  </table>
                </td></tr>
                """);
        }

        // ── 화면으로 가는 단추 ──────────────────────────────
        if (!string.IsNullOrWhiteSpace(_portalUrl))
        {
            var link = $"{_portalUrl.TrimEnd('/')}{TaskUrl(r.TaskKey)}";

            sb.Append($"""
                <tr><td align="center" style="padding:20px 20px 4px 20px;">
                  <a href="{Esc(link)}" style="display:inline-block;padding:11px 22px;background:{accent};color:#ffffff;font-size:13.5px;font-weight:600;text-decoration:none;border-radius:6px;">화면에서 보기</a>
                </td></tr>
                """);
        }

        // ── 꼬리 ────────────────────────────────────────────
        sb.Append("""
            <tr><td style="padding:16px 20px 20px 20px;">
              <div style="border-top:1px solid #e9ecef;padding-top:12px;font-size:11.5px;line-height:1.6;color:#adb5bd;">
                작업에서 「끝나면 메일로 받기」를 켜 두어 보내진 메일입니다.
                로그 전문은 길어서 붙이지 않습니다 — 화면에서 보십시오.
              </div>
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    /// <summary>머리의 둘째 줄 — 대상과 몇 차, 얼마나 걸렸나.</summary>
    private static string SubHead(MailRow r)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(r.TargetNm))
        {
            parts.Add(r.TargetNm!);
        }

        parts.Add($"{r.Seq}차");

        if (Took(r) is { Length: > 0 } took)
        {
            parts.Add(took.TrimStart(' ', '·').Trim());
        }

        return string.Join(" · ", parts);
    }

    /// <summary>요약 한 줄. 라벨 칸은 고정 폭이라 <b>값이 접혀도 칸이 남는다.</b></summary>
    private static string SummaryRow(string label, string valueHtml) => $"""
        <tr>
          <td valign="top" style="width:72px;padding:4px 12px 4px 0;color:#868e96;white-space:nowrap;">{label}</td>
          <td valign="top" style="padding:4px 0;word-break:break-word;">{valueHtml}</td>
        </tr>
        """;

    /// <summary>「전문」처럼 아래가 다른 것임을 알리는 가로선.</summary>
    private static string Divider(string label) => $"""
        <tr><td style="padding:20px 20px 0 20px;">
          <div style="border-top:1px solid #e9ecef;padding-top:14px;font-size:11.5px;font-weight:700;color:#868e96;letter-spacing:.06em;">{label}</div>
        </td></tr>
        """;

    /// <summary>
    /// 긴 글 한 덩이. <b>줄바꿈과 사이 띄움을 그대로 둔다</b> — AI 의 답에는
    /// 표와 목록이 섞여 있어 그것이 무너지면 읽을 수 없다.
    /// </summary>
    private static string Block(string title, string? text) => $"""
        <tr><td style="padding:12px 20px 0 20px;">
          <div style="font-size:12px;font-weight:600;color:#495057;padding-bottom:6px;">{title}</div>
          <div style="white-space:pre-wrap;word-break:break-word;font-family:ui-monospace,SFMono-Regular,Consolas,'D2Coding',monospace;font-size:12.5px;line-height:1.65;color:#343a40;background:#f8f9fa;border:1px solid #e9ecef;border-radius:6px;padding:12px 13px;">{Esc(text?.Trim())}</div>
        </td></tr>
        """;

    /// <summary>한 줄짜리 알림 칸 — 배포·브랜치처럼 짧은 사실.</summary>
    private static string Note(string label, string valueHtml) => $"""
        <tr><td style="padding:14px 20px 0 20px;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f8f9fa;border:1px solid #e9ecef;border-radius:6px;">
            <tr>
              <td valign="top" style="width:60px;padding:10px 0 10px 13px;font-size:12px;font-weight:600;color:#868e96;white-space:nowrap;">{label}</td>
              <td valign="top" style="padding:10px 13px 10px 8px;font-size:13px;line-height:1.55;word-break:break-word;">{valueHtml}</td>
            </tr>
          </table>
        </td></tr>
        """;

    /// <summary>
    /// 정리된 답 한 칸 — <b>한 문장 먼저, 그 아래 점 몇 개.</b>
    /// </summary>
    /// <remarks>
    /// 첫 줄만 굵게 둔다. 전부 굵게 하면 강조가 사라지고, 전부 보통이면
    /// <b>결론과 항목이 같은 무게로 보여</b> 다시 읽어야 알 수 있다.
    /// </remarks>
    private static string Said(AiResultSummary summary)
    {
        var sb = new StringBuilder();

        if (summary.Headline.Length > 0)
        {
            sb.Append($"""<span style="font-weight:600;">{Esc(summary.Headline)}</span>""");
        }

        if (summary.Points.Count > 0)
        {
            sb.Append(summary.Headline.Length > 0 ? """<div style="height:4px;"></div>""" : string.Empty);
            sb.Append(Bullets(summary.Points));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 점 찍힌 여러 줄. <b>&lt;ul&gt; 을 쓰지 않는다</b> — 메일 앱마다 들여쓰기와
    /// 점 모양이 제각각이라 다른 칸과 왼쪽이 맞지 않는다. div 로 직접 그린다.
    /// </summary>
    private static string Bullets(IReadOnlyList<string> items)
    {
        var sb = new StringBuilder();

        foreach (var item in items)
        {
            sb.Append($"""
                <div style="padding-top:2px;padding-left:11px;text-indent:-11px;">· {Esc(item)}</div>
                """);
        }

        return sb.ToString();
    }

    /// <summary>요약 칸에 들어갈 여러 줄. 없으면 <c>null</c> 이다.</summary>
    private static string? Lines(IReadOnlyList<string>? lines) =>
        lines is { Count: > 0 }
            ? string.Join("<br>", lines.Select(Esc))
            : null;

    /// <summary>「없다」를 적는 말. <b>빈 칸으로 두지 않는다</b> — 빠진 것처럼 보인다.</summary>
    private static string Muted(string text) =>
        $"""<span style="color:#adb5bd;">({Esc(text)})</span>""";

    /// <summary>
    /// HTML 로 나갈 글자를 가린다. <b>이것을 빠뜨리면 보고서가 통째로 무너진다</b> —
    /// AI 의 답에는 코드가 섞여 들어온다.
    /// </summary>
    private static string Esc(string? text) =>
        System.Net.WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>상태 색. 머리띠·단추·「할 일」 선이 같은 색을 쓴다.</summary>
    private static string Accent(string? status) => status switch
    {
        "succeeded" => "#2b8a3e",
        "failed" or "timeout" => "#c92a2a",
        _ => "#495057",
    };

    /// <summary>상태 색의 옅은 것 — 「할 일」 칸 바탕이다.</summary>
    private static string Tint(string? status) => status switch
    {
        "succeeded" => "#f4fbf6",
        "failed" or "timeout" => "#fff5f5",
        _ => "#f8f9fa",
    };

    /// <summary>
    /// 요약의 첫 줄. <b>이 한 줄만 읽어도 「무엇이 어떻게 됐는지」가 나와야 한다.</b>
    /// </summary>
    private static string Headline(MailRow r)
    {
        var what = Trim(r.Title, 52);

        return r.TaskStatus switch
        {
            "succeeded" when !string.IsNullOrWhiteSpace(r.PushedCommit)
                => $"완료 · 운영에 올라감 — {what}",
            "succeeded" when !string.IsNullOrWhiteSpace(r.DiffStat)
                => $"완료 · 고친 것 있음 — {what}",
            "succeeded" => $"완료 — {what}",
            "failed" => $"실패 — {what}",
            "timeout" => $"시간초과 — {what}",
            "canceled" => $"취소됨 — {what}",
            "interrupted" => $"중단됨 — {what}",
            _ => $"{StatusText(r.TaskStatus)} — {what}",
        };
    }

    private static string Took(MailRow r) =>
        r.DurationMs is { } ms and > 0
            ? $" · {TimeSpan.FromMilliseconds(ms):h\\:mm\\:ss}"
            : string.Empty;

    /// <summary>
    /// 긴 글에서 <b>앞쪽 몇 줄만</b> 뽑아 한 덩이로 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이제는 되돌아가는 자리다.</b> 요약은 AI 가 다시 써 주고
    /// (<see cref="AiResultSummarizer"/>), 이 함수는 <b>그것을 못 받았을 때</b>만
    /// 쓰인다 — AI 가 안 떠 있거나, 느리거나, JSON 을 안 준 경우다.
    /// </para>
    /// <para>
    /// 오래 이것이 유일한 방법이었던 이유는 「메일 한 통마다 모델을 한 번 더
    /// 부르고, 그 호출이 실패하면 메일이 안 나간다」였다. 그 걱정을 없앤 것이
    /// 바로 이 함수가 남아 있다는 사실이다 — <b>요약이 실패해도 메일은 나간다.</b>
    /// </para>
    /// <para>
    /// 표 구분선(<c>|---|</c>)이나 밑줄(<c>====</c>) 같은 <b>내용 없는 줄은
    /// 건너뛴다.</b> 그것부터 세면 요약 다섯 줄이 장식으로 다 찬다.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string>? Gist(string? text, int lines)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var picked = SecretMask.Apply(text)!
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !Decoration(l))
            .Select(Plain)
            .Where(l => l.Length > 0)
            .Take(lines)
            .Select(l => Trim(l, 110))
            .ToList();

        if (picked.Count == 0)
        {
            return null;
        }

        // **줄을 그대로 돌려준다.** 예전에는 여기서 공백으로 들여쓰기를 붙여
        // 라벨과 값을 맞췄는데, 그 줄맞춤은 글꼴 폭에 기대는 것이라 메일 앱이
        // 바뀌면 다 어긋났다. 이제 표가 칸을 잡으므로 줄만 주면 된다.
        return picked;
    }

    /// <summary>
    /// 마크다운 머리표를 뗀다. <b>요약에서는 <c>##</c> 나 <c>-</c> 가 뜻을 더하지
    /// 않는다</b> — 글자 수만 먹고, 라벨 뒤에 붙으면 오히려 어수선하다.
    /// </summary>
    private static string Plain(string line) =>
        line.TrimStart('#', '>', '*', '-', ' ').Trim();

    /// <summary>내용 없는 꾸밈 줄인가 — <c>---</c> · <c>===</c> · <c>|---|</c> 따위.</summary>
    private static bool Decoration(string line) =>
        line.All(c => c is '-' or '=' or '|' or ':' or '_' or '*' or '#' or ' ' or '─' or '═');

    /// <summary>
    /// 무엇이 바뀌었나 한 줄. <c>git diff --stat</c> 의 <b>마지막 요약 줄</b>이
    /// 이미 그 답이라 그것을 쓴다.
    /// </summary>
    private static string ChangeGist(MailRow r)
    {
        if (string.IsNullOrWhiteSpace(r.DiffStat))
        {
            return "없음 (파일을 고치지 않았습니다)";
        }

        var rows = r.DiffStat
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // 마지막 줄이 "3 files changed, 120 insertions(+), 4 deletions(-)" 다.
        var tail = rows.LastOrDefault(l => l.Contains("changed", StringComparison.OrdinalIgnoreCase));

        // 요약 줄이 잘려 없을 수도 있다(480자 상한). 그때는 줄 수로 센다.
        return tail is not null
            ? Trim(tail, 110)
            : $"파일 {rows.Count}개";
    }

    /// <summary>
    /// <b>내가 할 일이 있나.</b> 요약에서 사람이 제일 알고 싶은 칸이다 —
    /// 「읽고 끝」인지 「가서 봐야 하는지」가 여기서 갈린다.
    /// </summary>
    private string NextStep(MailRow r)
    {
        if (r.TaskStatus != "succeeded")
        {
            return string.IsNullOrWhiteSpace(r.ErrorSummary)
                ? "화면에서 로그를 보십시오."
                : $"화면에서 로그를 보십시오 — {Trim(r.ErrorSummary, 90)}";
        }

        if (!string.IsNullOrWhiteSpace(r.PushedCommit))
        {
            return "없습니다. 이미 올라갔고 배포가 돕니다.";
        }

        if (!string.IsNullOrWhiteSpace(r.DiffStat))
        {
            return string.IsNullOrWhiteSpace(r.GitBranch)
                ? "고친 것이 작업 자리에 있습니다. 사람이 확인해 올려야 합니다."
                : $"브랜치 {r.GitBranch} 에 커밋만 있습니다. 사람이 확인해 올려야 합니다.";
        }

        return "없습니다. 읽어 보기만 하면 됩니다.";
    }

    private static string Trim(string? text, int max)
    {
        var t = (text ?? string.Empty).Trim();

        return t.Length <= max ? t : t[..max] + "…";
    }

    private static string StatusText(string? status) => status switch
    {
        "succeeded" => "완료",
        "failed" => "실패",
        "timeout" => "시간초과",
        "canceled" => "취소",
        "interrupted" => "중단",
        _ => status ?? string.Empty,
    };

    private sealed class MailRow
    {
        public long TaskKey { get; set; }
        public string? Title { get; set; }
        public bool NotifyEmail { get; set; }
        public bool NotifyPwa { get; set; }
        public string? NotifyTo { get; set; }
        public string? NotifyWhen { get; set; }
        public string? TaskStatus { get; set; }
        public long? DurationMs { get; set; }
        public string? PushedCommit { get; set; }
        public string? CreId { get; set; }
        public string? TargetNm { get; set; }
        public int Seq { get; set; }
        public int? ExitCode { get; set; }
        public string? ResultText { get; set; }
        public string? DiffStat { get; set; }
        public string? ErrorSummary { get; set; }
        public string? GitBranch { get; set; }

        /// <summary>그때 실제로 준 지시문. <b>요약에 「무엇을 시켰나」를 적으려면 필요하다.</b></summary>
        public string? Instruction { get; set; }

        /// <summary>
        /// AI 가 다시 써 준 요약. <b>이미 있으면 모델을 또 부르지 않는다.</b>
        /// </summary>
        public string? SummaryText { get; set; }
    }
}
