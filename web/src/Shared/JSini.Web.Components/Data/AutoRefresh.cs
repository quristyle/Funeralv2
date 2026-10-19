using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

/// <summary>스스로 주기적으로 다시 읽는 화면의 뼈대(DataPage 를 잇는다). 상황판으로 띄워 두는 화면은 사람이 새로 고침을 누르러 오지 않으므로 시계가 필요하다.
/// 자동 조회는 안내 줄을 건드리지 않는다 — 깜빡이면 누가 눌렀다는 신호로 읽히고, 실패해도 빨간 줄 대신 마지막으로 성공한 화면이 더 쓸모 있다.</summary>
public abstract class AutoRefreshPage : DataPage, IDisposable
{
    private Timer? _timer;

    /// <summary>얼마나 자주 다시 읽을지. 화면이 정한다.</summary>
    protected virtual TimeSpan RefreshInterval => TimeSpan.FromSeconds(30);

    /// <summary>자동으로 다시 읽을 때 부르는 것. 안내 줄을 건드리지 않게 짠다 — 자료만 갈아 끼우고 StateHasChanged 는 이 뼈대가 부른다.</summary>
    protected abstract Task RefreshAsync();

    /// <summary>마지막으로 성공한 시각. 화면이 「몇 시 기준인지」를 보여 줄 수 있다.</summary>
    protected DateTime? RefreshedAt { get; private set; }

    /// <summary>시계를 켠다. 화면의 OnInitializedAsync 끝에서 부른다. 프리렌더에서는 켜지 않는다 — 곧 버려질 화면에 시계가 달려 발화하면 이미 없는 화면을 다시 그리려 든다. 회로가 붙으면 화면이 새로 만들어져 그때 다시 부르므로 실제로 도는 시계는 하나다.</summary>
    protected void StartAutoRefresh()
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        _timer?.Dispose();
        _timer = new Timer(_ => _ = TickAsync(), null, RefreshInterval, RefreshInterval);
    }

    private async Task TickAsync()
    {
        try
        {
            await InvokeAsync(async () =>
            {
                await RefreshAsync();
                RefreshedAt = AppTime.UtcNow;
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // 회로가 이미 끊겼다. 시계가 한 박자 늦게 도는 것뿐이다.
        }
        catch (Exception)
        {
            // 스스로 도는 조회다. 실패하면 마지막으로 성공한 화면을 그대로 둔다.
        }
    }

    /// <summary>시계를 끈다. 화면이 사람에게 자동 갱신을 맡길 때 쓴다 — 켜면 StartAutoRefresh 로 간다. 화면을 떠날 때는 Dispose 가 같은 일을 한다.</summary>
    protected void StopAutoRefresh()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>시계를 놓는다. 빠뜨리면 화면을 닫아도 조회가 계속 나간다.</summary>
    public virtual void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        GC.SuppressFinalize(this);
    }
}
