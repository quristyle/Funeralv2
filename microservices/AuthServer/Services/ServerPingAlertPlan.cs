namespace AuthServer.Services;

/// <summary>Ping 한 번의 결과로 무엇을 알릴지.</summary>
public enum ServerPingAlertKind
{
    /// <summary>알릴 것 없음.</summary>
    None,

    /// <summary>연달아 실패해 두절로 확정했다.</summary>
    Down,

    /// <summary>두절이 이어진다 — 되풀이 알림.</summary>
    StillDown,

    /// <summary>두절을 알린 뒤 다시 응답한다.</summary>
    Recovered,
}

/// <summary>보낼 알림과, 그 두절이 시작된 때(첫 실패 시각).</summary>
public readonly record struct ServerPingAlert(ServerPingAlertKind Kind, DateTimeOffset? DownSince);

/// <summary>
/// 내부 장비 Ping 알림의 상태 기계. 바깥을 부르지 않고 상태만 든다.
/// 한 번 실패로는 알리지 않고(회선이 한 번 튀는 것) <see cref="FailThreshold"/> 번 연달아 실패해야 두절로 친다.
/// 복구 알림은 두절 알림을 실제로 보낸 뒤에만 낸다 — 알리지 않은 두절의 복구는 받는 사람에게 영문 모를 알림이다.
/// </summary>
public sealed class ServerPingAlertPlan(int failThreshold, TimeSpan repeatInterval)
{
    /// <summary>두절로 확정하는 연속 실패 횟수.</summary>
    public int FailThreshold { get; } = Math.Max(1, failThreshold);

    /// <summary>두절이 이어질 때 다시 알리는 간격. 0 이하면 되풀이하지 않는다.</summary>
    public TimeSpan RepeatInterval { get; } = repeatInterval;

    private int _failures;
    private DateTimeOffset? _firstFailureAt;
    private DateTimeOffset? _lastAlertAt;

    /// <summary>두절로 확정되어 알린 상태인가.</summary>
    public bool IsDown => _lastAlertAt is not null;

    /// <summary>Ping 결과 하나를 넣고 보낼 알림을 받는다.</summary>
    public ServerPingAlert Observe(bool alive, DateTimeOffset now)
    {
        if (alive)
        {
            var alert = IsDown
                ? new ServerPingAlert(ServerPingAlertKind.Recovered, _firstFailureAt)
                : new ServerPingAlert(ServerPingAlertKind.None, null);

            _failures = 0;
            _firstFailureAt = null;
            _lastAlertAt = null;
            return alert;
        }

        _failures++;
        _firstFailureAt ??= now;

        if (!IsDown)
        {
            if (_failures < FailThreshold) return new(ServerPingAlertKind.None, _firstFailureAt);

            _lastAlertAt = now;
            return new(ServerPingAlertKind.Down, _firstFailureAt);
        }

        if (RepeatInterval > TimeSpan.Zero && now - _lastAlertAt!.Value >= RepeatInterval)
        {
            _lastAlertAt = now;
            return new(ServerPingAlertKind.StillDown, _firstFailureAt);
        }

        return new(ServerPingAlertKind.None, _firstFailureAt);
    }
}
