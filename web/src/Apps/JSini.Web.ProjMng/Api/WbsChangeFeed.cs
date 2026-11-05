using Microsoft.Extensions.Logging;

namespace JSini.Web.ProjMng.Api;

public enum WbsChangeKind
{
    Created,
    Updated,
    Deleted,
}

/// <summary>일정(dev_wbs) 한 줄이 바뀌었다는 소식. <c>Source</c> 는 바꾼 쪽의 <see cref="WbsClient"/> 라 화면이 제가 바꾼 것을 가려낸다.
/// 바꾼 사람(<c>ActorLogin</c> · <c>ActorName</c>)은 모르면 null 이다. 지운 소식에는 번호만 있다.</summary>
public sealed record WbsChange(
    WbsChangeKind Kind,
    int WbsId,
    int? PrjRid,
    string? Title,
    DateTime? Start,
    DateTime? End,
    object Source,
    string? ActorLogin,
    string? ActorName);

/// <summary>일정이 바뀐 것을 포털 안의 모든 회로에 알린다. 싱글턴이다 — 포털은 한 프로세스라 다른 사람이 등록한 것도 여기를 지난다.
/// 받는 쪽은 다른 스레드에서 불리므로 InvokeAsync 로 넘겨 받아야 한다.</summary>
public sealed class WbsChangeFeed(ILogger<WbsChangeFeed> logger)
{
    public event Action<WbsChange>? Changed;

    public void Publish(WbsChange change)
    {
        var handlers = Changed;
        if (handlers is null) return;

        // 받는 쪽 하나가 던져도 저장한 사람의 흐름과 다른 받는 쪽은 멀쩡해야 한다.
        foreach (var handler in handlers.GetInvocationList().Cast<Action<WbsChange>>())
        {
            try
            {
                handler(change);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "일정 변경 알림을 받는 쪽이 던졌습니다 — {WbsId}", change.WbsId);
            }
        }
    }
}
