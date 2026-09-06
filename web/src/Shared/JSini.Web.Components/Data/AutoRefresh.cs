using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

/// <summary>
/// 스스로 주기적으로 다시 읽는 화면의 뼈대. <see cref="DataPage"/> 를 잇는다.
/// </summary>
/// <remarks>
/// <para>
/// [상황판으로 띄워 두는 화면들이 있다]
/// </para>
///
/// <para>
/// 빈소 현황·간편 현황·장례 현황은 안내 데스크나 사무실 벽에 띄워 둔다.
/// <b>사람이 새로 고침을 누르러 오지 않는다</b>는 뜻이다. 옛 화면들은 저마다
/// 30~60초 시계를 돌렸는데, 옮기면서 그것이 다 빠져 화면이 띄워 둔 시각에
/// 굳어 있었다.
/// </para>
///
/// <para>
/// [조용히 읽는다 — 이것이 이 뼈대의 핵심이다]
/// </para>
///
/// <para>
/// <see cref="DataPage.LoadAsync"/> 로 다시 읽으면 안내 줄이 깜빡이고
/// 「조회 결과가 없습니다」가 저절로 떴다 사라진다. 상황판에서 그 깜빡임은
/// <b>누가 무언가 눌렀다는 신호로 읽힌다.</b> 그래서 자동 조회는 안내 줄을
/// 건드리지 않는다.
/// </para>
///
/// <para>
/// 실패해도 마찬가지다. 사람이 보고 있지 않을 때 빨간 줄을 띄워 봐야 소용이
/// 없고, 그 순간에는 <b>마지막으로 성공한 화면</b>이 더 쓸모 있다.
/// 다음 차례에 다시 해 본다.
/// </para>
/// </remarks>
public abstract class AutoRefreshPage : DataPage, IDisposable
{
    private Timer? _timer;

    /// <summary>얼마나 자주 다시 읽을지. 화면이 정한다.</summary>
    protected virtual TimeSpan RefreshInterval => TimeSpan.FromSeconds(30);

    /// <summary>
    /// 자동으로 다시 읽을 때 부르는 것. <b>안내 줄을 건드리지 않게</b> 짠다 —
    /// 자료만 갈아 끼우고 <c>StateHasChanged</c> 는 이 뼈대가 부른다.
    /// </summary>
    protected abstract Task RefreshAsync();

    /// <summary>마지막으로 성공한 시각. 화면이 「몇 시 기준인지」를 보여 줄 수 있다.</summary>
    protected DateTime? RefreshedAt { get; private set; }

    /// <summary>시계를 켠다. 화면의 <c>OnInitializedAsync</c> 끝에서 부른다.</summary>
    protected void StartAutoRefresh()
    {
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
                RefreshedAt = DateTime.Now;
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // 회로가 이미 끊겼다. 시계가 한 박자 늦게 도는 것뿐이다.
        }
        catch (Exception)
        {
            // 스스로 도는 조회다. 실패하면 마지막으로 성공한 화면을 그대로 둔다 —
            // 까닭은 이 클래스 머리말에 있다.
        }
    }

    /// <summary>
    /// 시계를 놓는다. <b>빠뜨리면 화면을 닫아도 조회가 계속 나간다.</b>
    /// </summary>
    public virtual void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        GC.SuppressFinalize(this);
    }
}
