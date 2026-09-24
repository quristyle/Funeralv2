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
}
