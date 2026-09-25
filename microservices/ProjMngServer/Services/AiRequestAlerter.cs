using System.Net.Http.Json;

using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 「AI 작업 요청」이 올라오면 <b>관리자에게 앱 푸시를 보낸다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 필요한가.</b> 일반 사용자가 올린 요청(<see cref="AiTask.IsUserRequest"/>)은
/// 저장만 되고 <b>아무 데서도 안 돈다</b> — 대상과 AI 를 관리자가 채워 주어야
/// 비로소 실행된다. 그런데 올라온 것을 알려 주는 길이 하나도 없어서, 관리자가
/// 「AI 작업 지시」 화면을 스스로 열어 보기 전까지 그 요청은 <b>아무 일도
/// 일어나지 않은 채 쌓여 있었다.</b> 올린 사람 쪽에서는 그것이 「무시당한 것」과
/// 구분되지 않는다.
/// </para>
///
/// <para>
/// <b>보낼 사람을 여기서 풀지 않는다.</b> 관리자가 누구인지는 포털 계정 DB
/// (<c>jsiniportal</c> 의 <c>scom.role_accounts</c>)에 있고, 프로젝트관리는 아예
/// 다른 데이터베이스(<c>projmng</c>)를 본다 — 여기서는 조회조차 할 수 없다.
/// 그래서 <b>역할 이름만 실어 보내고</b> 알림 서비스가 사람으로 편다
/// (<c>NotificationServer</c> 의 <c>SendPushDto.Roles</c>).
/// </para>
///
/// <para>
/// <b>올린 사람은 뺀다.</b> 관리자도 이 화면에서 요청을 올릴 수 있는데(역할 셋
/// 모두 「AI 작업 요청」 메뉴를 갖고 있다), 자기가 방금 누른 일로 자기 휴대폰이
/// 울리면 받는 쪽은 알림을 꺼 버린다.
/// </para>
///
/// <para>
/// <b>사람을 기다리게 하지 않는다.</b> 「요청을 올렸다」는 DB 에 한 줄을 적는
/// 일이고 그것으로 이미 끝이다 — 알림은 그 뒤에 따라붙는 편의라, 그것 때문에
/// 저장 화면이 묶여 있을 이유가 없다(<see cref="AiTaskBell"/> 와 같은 판단이다).
/// 그래서 <see cref="Fire"/> 는 기다리지 않고, 못 보낸 알림은 <b>다시 시도하지
/// 않는다</b> — 요청 자체는 이미 화면에 남아 있다.
/// </para>
/// </remarks>
public sealed class AiRequestAlerter(
    IConfiguration configuration, IHttpClientFactory http, ILogger<AiRequestAlerter> logger)
{
    /// <summary>알림 서비스 주소. 같은 장비 안이라 루프백이다.</summary>
    private readonly string _notifyUrl =
        configuration["AiTasks:NotifyUrl"] is { Length: > 0 } u ? u : "http://127.0.0.1:5460";

    /// <summary>
    /// 알릴 역할. 비우면 <b>아무에게도 안 간다</b> — 설정을 지워 기능을 끌 수 있다.
    /// </summary>
    /// <remarks>
    /// 기본값은 「AI 작업 지시」 메뉴(<c>PM_AI_TASKS</c>)를 가진 역할 셋이다.
    /// 올라온 요청을 실제로 처리하는 화면이 그것이므로, <b>그 화면을 볼 수 있는
    /// 사람</b>이 곧 이 알림을 받아야 하는 사람이다.
    /// </remarks>
    private readonly string[] _roles =
        configuration.GetSection("AiTasks:RequestNotifyRoles").Get<string[]>()
        ?? ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR", "PROJMNG_ADMIN"];

    /// <summary>
    /// 그 건 하나를 펴 놓는 화면. <b>결과 알림과 같은 한 줄을 쓴다</b>
    /// (<see cref="AiTaskNotifier"/>) — 두 벌로 두면 한쪽만 고쳐져 어긋난다.
    /// </summary>
    private static string TaskUrl(long taskKey) => $"/projmng/ai/task/{taskKey}";

    /// <summary>
    /// 올린 사람이 <b>자기 요청을 펴 보는 화면</b>. 관리자용 상세와 주소가
    /// 다르다 — 그쪽은 일반 사용자에게 메뉴가 없다.
    /// </summary>
    /// <remarks>
    /// 번호를 물음표 뒤에 실어 <b>그 건이 열린 채로</b> 뜨게 한다
    /// (<c>AiRequestList</c> 의 <c>Task</c> 매개변수). 목록만 열어 주면
    /// 알림을 누른 사람이 어느 줄에 말이 붙었는지 눈으로 다시 찾아야 한다.
    /// </remarks>
    private static string RequestUrl(long taskKey) => $"/projmng/ai/request?task={taskKey}";

    /// <summary>
    /// 알림을 띄우고 <b>기다리지 않는다.</b> 실패해도 부른 쪽은 알지 못한다 —
    /// 알아야 할 쪽은 로그다(머리말).
    /// </summary>
    /// <param name="task">방금 저장된 요청.</param>
    /// <param name="userName">올린 사람의 이름. 없으면 아이디로 적는다.</param>
    public void Fire(AiTask? task, string? userName = null)
    {
        if (task is null) return;

        // **요청의 취소 토큰을 쓰지 않는다.** 응답이 끝나는 순간 그 토큰은
        // 취소되고, 그러면 이 알림은 언제나 보내다 말게 된다.
        _ = Task.Run(async () =>
        {
            try
            {
                await SendAsync(task, userName, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "작업 요청 {TaskKey} 알림을 보내지 못했습니다.", task.TaskKey);
            }
        });
    }

    /// <summary>실제로 보낸다. 시험에서 결과를 보려고 따로 열어 둔다.</summary>
    public async Task SendAsync(AiTask task, string? userName, CancellationToken ct)
    {
        if (_roles.Length == 0)
        {
            logger.LogDebug("AiTasks:RequestNotifyRoles 가 비어 작업 요청 알림을 건너뜁니다.");
            return;
        }

        var who = string.IsNullOrWhiteSpace(userName) ? task.CreId?.Trim() : userName.Trim();
        var title = string.IsNullOrWhiteSpace(task.Title) ? "(제목 없음)" : task.Title.Trim();

        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"{_notifyUrl.TrimEnd('/')}/notifications/push")
        {
            Content = JsonContent.Create(new
            {
                roles = _roles,

                // 올린 사람이 관리자여도 자기 것으로는 안 울린다(머리말).
                excludeOwnerKeys = string.IsNullOrWhiteSpace(task.CreId)
                    ? Array.Empty<string>()
                    : new[] { task.CreId.Trim() },

                message = new
                {
                    title = "새 AI 작업 요청",

                    // 잠금화면에서는 두 줄이 전부다. **누가 올렸나**와 **무엇을
                    // 부탁했나** 둘만 담는다 — 나머지는 눌러 들어가면 있다.
                    body = string.IsNullOrWhiteSpace(who) ? title : $"{who} 님 — {title}",

                    url = TaskUrl(task.TaskKey),

                    // 아이콘은 **올린 사람의 얼굴**이다. 받는 사람이 가장 먼저
                    // 묻는 것이 「누가 부탁했나」라서, 사진 한 장이 본문보다 빠르다.
                    iconOwnerKey = task.CreId?.Trim(),

                    // **건마다 다른 태그다.** 고정값을 쓰면 요청 둘이 연달아
                    // 올라올 때 뒤엣것이 앞엣것을 덮어 하나만 보인다.
                    tag = $"ai-request-{task.TaskKey}",
                }
            }),
        };

        // 게이트웨이를 거치지 않고 직접 부르므로 신원을 스스로 적는다.
        // 발송 기록의 「보낸 이」가 이 값이다.
        req.Headers.Add("X-User-Id", "AI_TASK");

        var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        using var res = await client.SendAsync(req, ct);

        if (res.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "작업 요청 {TaskKey} 를 역할 {Roles} 에게 알렸습니다.",
                task.TaskKey, string.Join(",", _roles));
        }
        else
        {
            var why = await res.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "작업 요청 {TaskKey} 알림 실패 (HTTP {Status}): {Why}",
                task.TaskKey, (int)res.StatusCode, why);
        }
    }

    // ── 남길말 ──────────────────────────────────────────────
    //
    // 설계는 `AiTaskNoteService` 머리말. 요청 하나에 관리자와 올린 사람이
    // 번갈아 말을 남기고, **적힌 쪽이 아니라 상대에게** 알림이 간다.

    /// <summary>
    /// <b>남긴 말을 상대에게 알린다.</b> 기다리지 않는다 — 말은 이미 표에 있다.
    /// </summary>
    /// <param name="task">말이 붙은 요청.</param>
    /// <param name="note">방금 저장된 말.</param>
    /// <param name="writerName">적은 사람의 이름. 없으면 아이디로 적는다.</param>
    public void FireNote(AiTask? task, AiTaskNote? note, string? writerName = null)
    {
        if (task is null || note is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await SendNoteAsync(task, note, writerName, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex, "작업 요청 {TaskKey} 의 남긴말 알림을 보내지 못했습니다.", task.TaskKey);
            }
        });
    }

    /// <summary>실제로 보낸다. 시험에서 결과를 보려고 따로 열어 둔다.</summary>
    /// <remarks>
    /// <para>
    /// <b>가는 곳이 둘이다.</b> 관리자가 적었으면 <b>올린 사람 한 명</b>에게
    /// 가고(<c>owners</c>), 올린 사람이 적었으면 <b>관리자 역할 전원</b>에게
    /// 간다(<c>roles</c>) — 뒤엣것이 없으면 사용자가 되물어도 아무도 모른다.
    /// </para>
    /// <para>
    /// <b>줄에서 겹치게 둔다</b>(<c>topic</c>). 한 요청에 말이 여러 줄
    /// 달리는 동안 휴대폰이 꺼져 있으면, 겹치지 않을 때 그 줄 수만큼이
    /// 한꺼번에 쏟아진다. 열어 보면 어차피 붙은 말이 다 보이므로 마지막
    /// 하나면 된다(docs/push-delivery.md).
    /// </para>
    /// <para>
    /// <b>수명은 하루다.</b> 결과 알림(두 시간)보다 길게 잡는 이유는, 그쪽은
    /// 지나고 나면 화면에서 확인하면 그만인 통보지만 이 말은 <b>답을
    /// 기다리는 말</b>이라 늦게라도 닿는 편이 낫기 때문이다.
    /// </para>
    /// </remarks>
    public async Task SendNoteAsync(
        AiTask task, AiTaskNote note, string? writerName, CancellationToken ct)
    {
        var owner = task.CreId?.Trim();
        var writer = note.CreId?.Trim();

        // 올린 사람이 적었나. `AiTaskNoteService` 가 넣을 때 정해 둔 값을
        // 그대로 믿는다 — 같은 비교를 두 벌 두면 한쪽만 고쳐져 어긋난다.
        var byOwner = note.IsOwner;

        if (!byOwner && string.IsNullOrWhiteSpace(owner))
        {
            logger.LogDebug("작업 요청 {TaskKey} 에 올린 사람이 없어 남긴말 알림을 건너뜁니다.", task.TaskKey);
            return;
        }

        if (byOwner && _roles.Length == 0)
        {
            logger.LogDebug("AiTasks:RequestNotifyRoles 가 비어 남긴말 알림을 건너뜁니다.");
            return;
        }

        var who = string.IsNullOrWhiteSpace(writerName) ? writer : writerName.Trim();
        var title = string.IsNullOrWhiteSpace(task.Title) ? "(제목 없음)" : task.Title.Trim();

        // 잠금화면에는 두 줄이 전부다. **누가 무슨 말을 남겼나**를 담고
        // 어느 요청인지는 그 뒤에 붙인다 — 눌러 들어가면 전문이 있다.
        var said = Shorten(note.Contents, 60);

        // 받는 쪽을 먼저 세운다. **삼항 안에서 만들지 않는다** — 익명 형식의
        // 배열과 빈 배열은 형이 달라 그 자리에서 합쳐지지 않는다.
        var owners = new List<object>();

        if (!byOwner)
        {
            owners.Add(new { ownerType = "jsini", ownerKey = owner! });
        }

        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"{_notifyUrl.TrimEnd('/')}/notifications/push")
        {
            Content = JsonContent.Create(new
            {
                owners,

                roles = byOwner ? _roles : null,

                // 적은 사람 본인에게는 안 울린다. 관리자가 관리자 역할로
                // 묶여 있을 때 자기 글로 자기 휴대폰이 울리는 것을 막는다.
                excludeOwnerKeys = string.IsNullOrWhiteSpace(writer)
                    ? Array.Empty<string>()
                    : new[] { writer },

                message = new
                {
                    // **가는 쪽에 맞춘 제목이다.** 올린 사람이 받는 것은
                    // 「내가 부탁한 건에 답이 왔다」이고, 관리자가 받는 것은
                    // 「그 사람이 무언가를 덧붙였다」다.
                    title = byOwner ? "AI 작업 요청에 남긴 말" : "AI 작업 요청에 답이 달렸습니다",

                    body = string.IsNullOrWhiteSpace(who)
                        ? $"{title} — {said}"
                        : $"{who} 님: {said}",

                    // **가는 사람에 따라 주소가 갈린다.** 올린 사람에게는
                    // 자기 요청 화면이고(메뉴가 그것뿐이다), 관리자에게는
                    // 대상·AI 를 채울 수 있는 상세 화면이다.
                    url = byOwner ? TaskUrl(task.TaskKey) : RequestUrl(task.TaskKey),

                    // 아이콘은 **말을 남긴 사람의 얼굴**이다 — 받는 사람이
                    // 가장 먼저 묻는 것이 「누가 뭐래」라서.
                    iconOwnerKey = writer,

                    // 창은 건마다 따로 뜨고(태그), 밀려 있는 줄은 요청마다
                    // 하나로 줄인다(토픽). 위 머리말 참고.
                    tag = $"ai-note-{note.NoteKey}",
                    topic = $"ai-note-t{task.TaskKey}",

                    ttlSeconds = 86400,
                }
            }),
        };

        req.Headers.Add("X-User-Id", "AI_TASK");

        var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        using var res = await client.SendAsync(req, ct);

        if (res.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "작업 요청 {TaskKey} 의 남긴말 {NoteKey} 를 {To} 에게 알렸습니다.",
                task.TaskKey, note.NoteKey, byOwner ? string.Join(",", _roles) : owner);
        }
        else
        {
            var why = await res.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "작업 요청 {TaskKey} 남긴말 알림 실패 (HTTP {Status}): {Why}",
                task.TaskKey, (int)res.StatusCode, why);
        }
    }

    /// <summary>
    /// 알림 본문에 실을 만큼만 자른다. <b>줄바꿈을 공백으로 편다</b> —
    /// 잠금화면은 어차피 두 줄이라 원문의 줄 모양이 남아 봐야 자리만 먹는다.
    /// </summary>
    private static string Shorten(string? text, int max)
    {
        var one = string.Join(' ', (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return one.Length <= max ? one : $"{one[..max]}…";
    }
}
