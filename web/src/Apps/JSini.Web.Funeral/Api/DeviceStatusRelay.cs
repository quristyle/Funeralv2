using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Funeral.Api;

/// <summary>장비 상태 방송을 열린 화면들에 나눠 준다. 포털 프로세스가 DeviceHub 에 한 번만 붙는다(싱글턴) — 관리자 수만큼 연결이 생기지 않게 하려고다.
/// 첫 구독자에 붙고 마지막 구독자가 나가도 끊지 않는다 — 화면을 들락거릴 때마다 끊으면 방송을 놓친다. 허브에 못 붙어도 예외는 올리지 않는다(목록의 마지막 값이 남는다).</summary>
public sealed class DeviceStatusRelay(IConfiguration configuration, ILogger<DeviceStatusRelay> logger)
    : IAsyncDisposable
{
    /// <summary>지금 듣고 있는 화면들. 화면이 사라지면 스스로 빠진다.</summary>
    private readonly List<Action<string, string>> _listeners = [];

    /// <summary>호실 배정이 바뀌었다는 방송을 듣는 화면들(빈소현황).</summary>
    private readonly List<Action<string>> _assignmentListeners = [];

    private readonly SemaphoreSlim _gate = new(1, 1);

    private HubConnection? _hub;

    /// <summary>상태 변화를 듣는다. 돌려주는 것을 Dispose 해야 그만 듣는다 — 화면이 사라질 때 반드시 부른다. 안 그러면 죽은 회로가 구독 목록에 남는다.</summary>
    /// <param name="onChanged">장비 코드와 새 상태(<c>ONLINE</c> · <c>OFFLINE</c>)</param>
    public IDisposable Subscribe(Action<string, string> onChanged)
    {
        lock (_listeners)
        {
            _listeners.Add(onChanged);
        }

        // 첫 구독자면 여기서 붙는다. 기다리지 않는다 — 화면이 뜨는 것을
        // 허브 연결이 붙잡고 있을 이유가 없다.
        _ = EnsureConnectedAsync();

        return new Subscription(this, onChanged);
    }

    /// <summary>호실 배정이 바뀌는 방송(고인 등록·호실 이동·출상·취소). 인자는 바뀐 호실 식별자다.
    /// 바뀐 칸만 고칠 수 없어(고인·상주·장비가 통째로 달라진다) 빈소현황이 듣고 다시 읽는다.</summary>
    public IDisposable SubscribeAssignments(Action<string> onChanged)
    {
        lock (_assignmentListeners)
        {
            _assignmentListeners.Add(onChanged);
        }

        _ = EnsureConnectedAsync();

        return new AssignmentSubscription(this, onChanged);
    }

    private async Task EnsureConnectedAsync()
    {
        if (_hub is not null)
        {
            return;
        }

        await _gate.WaitAsync();

        try
        {
            if (_hub is not null)
            {
                return;
            }

            var baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";

            if (!baseUrl.EndsWith('/'))
            {
                baseUrl += "/";
            }

            var hub = new HubConnectionBuilder()
                .WithUrl($"{baseUrl}funeral/hubs/device")

                // 게이트웨이가 이 경로에는 요청 수 제한을 걸지 않는다 —
                // 재연결이 몰릴 때 negotiate 가 거부되면 통로가 끊긴다.
                .WithAutomaticReconnect([
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                ])
                .Build();

            hub.On<string, string>("DeviceStatusChanged", (deviceCode, status) =>
            {
                Action<string, string>[] listeners;

                lock (_listeners)
                {
                    listeners = [.. _listeners];
                }

                foreach (var listener in listeners)
                {
                    try
                    {
                        listener(deviceCode, status);
                    }
                    catch (Exception ex)
                    {
                        // 화면 하나가 죽어도 나머지에는 전해야 한다.
                        logger.LogDebug(ex, "장비 상태를 화면에 전하지 못했다: {Code}", deviceCode);
                    }
                }
            });

            hub.On<string>("RoomAssignmentChanged", roomId =>
            {
                Action<string>[] listeners;

                lock (_assignmentListeners)
                {
                    listeners = [.. _assignmentListeners];
                }

                foreach (var listener in listeners)
                {
                    try
                    {
                        listener(roomId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "호실 배정 변경을 화면에 전하지 못했다: {RoomId}", roomId);
                    }
                }
            });

            await hub.StartAsync();
            _hub = hub;

            logger.LogInformation("장비 상태 허브에 붙었다: {Url}", $"{baseUrl}funeral/hubs/device");
        }
        catch (Exception ex)
        {
            // 상태 표시는 곁다리다. 화면은 떠야 한다.
            logger.LogWarning(ex, "장비 상태 허브에 붙지 못했다. 상태는 조회한 시점 값으로 남는다.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
        }

        _gate.Dispose();
    }

    private sealed class Subscription(DeviceStatusRelay relay, Action<string, string> handler) : IDisposable
    {
        public void Dispose()
        {
            lock (relay._listeners)
            {
                relay._listeners.Remove(handler);
            }
        }
    }

    private sealed class AssignmentSubscription(DeviceStatusRelay relay, Action<string> handler) : IDisposable
    {
        public void Dispose()
        {
            lock (relay._assignmentListeners)
            {
                relay._assignmentListeners.Remove(handler);
            }
        }
    }
}
