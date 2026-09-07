using Microsoft.JSInterop;

namespace JSini.Web.Components.Security;

/// <summary>
/// 잠금화면의 상태. <b>회로 하나(=창 하나)마다 하나다.</b>
///
/// <para>
/// vben 의 <c>LockScreen</c> 을 옮긴 것이다(D7). 자리를 비울 때 화면을 덮고,
/// 돌아와서 비밀번호를 넣어야 풀린다.
/// </para>
///
/// <para>
/// <b>이것은 보안 경계가 아니다.</b> 잠긴 동안에도 토큰은 그대로 살아 있고
/// API 는 여전히 부를 수 있다 — 브라우저 개발자 도구를 열면 우회할 수 있다.
/// 막으려는 것은 <b>지나가는 사람</b>이지 공격자가 아니다. vben 도 같은 수준이었다.
/// 진짜로 끊어야 하면 잠그지 말고 로그아웃해야 하고, 그 말을 화면에도 적어 두었다.
/// </para>
///
/// <para>
/// <b>왜 브라우저에 남기나</b> — 회로 안에만 두면 새로고침 한 번으로 풀린다.
/// 그러면 「덮어 둔다」는 목적조차 못 한다. 그래서 <c>sessionStorage</c> 에
/// 표시를 남기고 회로가 새로 붙을 때 다시 읽는다. <c>localStorage</c> 가 아닌
/// 이유는 창을 닫으면 잠금도 함께 사라져야 하기 때문이다 — 새 창을 열었는데
/// 지난주에 잠가 둔 화면이 뜨면 그건 고장으로 읽힌다.
/// </para>
/// </summary>
public sealed class ScreenLock(IJSRuntime js, Layout.PortalBoot boot)
{
    /// <summary>
    /// 브라우저에 남기는 표시.
    ///
    /// <para>
    /// <b>정본은 <see cref="Layout.PortalBoot.ScreenLockedKey"/> 다.</b> 읽는 일은
    /// 그쪽이 다른 값들과 함께 한 왕복으로 하고, 여기서는 쓰는 데만 쓴다.
    /// 열쇠를 두 곳에 적으면 한쪽만 고치는 날이 오고, 그때 증상은
    /// 「잠갔는데 새로고침하면 풀린다」다.
    /// </para>
    /// </summary>
    private const string StorageKey = Layout.PortalBoot.ScreenLockedKey;

    private bool _restored;

    /// <summary>지금 잠겨 있는가.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>잠기거나 풀렸을 때. 레이아웃이 다시 그리려고 듣는다.</summary>
    public event Action? Changed;

    /// <summary>
    /// 브라우저에 남은 표시를 읽어 상태를 되살린다.
    /// <b>회로가 붙은 뒤에</b> 불러야 한다(<c>OnAfterRenderAsync</c>) —
    /// 프리렌더 중에는 JS 를 부를 수 없다.
    /// </summary>
    /// <remarks>
    /// 저장소를 직접 읽지 않는다. <see cref="Layout.PortalBoot"/> 가 다른 값들과
    /// 함께 <b>한 왕복으로</b> 읽어 온 것을 받는다 — 왜 그렇게 했는지는 그
    /// 클래스 머리말에 있다. 못 읽었으면 「안 잠긴 것」으로 온다. 그것이
    /// 맞는 기본값이다 — 읽기 실패로 화면을 덮으면 풀 방법이 없는 상태에 갇힌다.
    /// </remarks>
    public async Task RestoreAsync()
    {
        if (_restored)
        {
            return;
        }
        _restored = true;

        var state = await boot.ReadAsync();

        if (state.ScreenLocked && !IsLocked)
        {
            IsLocked = true;
            Changed?.Invoke();
        }
    }

    public Task LockAsync() => SetAsync(true);

    public Task UnlockAsync() => SetAsync(false);

    private async Task SetAsync(bool locked)
    {
        if (IsLocked == locked)
        {
            return;
        }

        IsLocked = locked;
        _restored = true;

        try
        {
            if (locked)
            {
                await js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, "1");
            }
            else
            {
                await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
            }
        }
        catch (JSException)
        {
            // 화면은 이미 덮였거나 걷혔다. 표시를 못 남긴 것뿐이라
            // 새로고침하면 풀린다 — 그것으로 사용자를 막지는 않는다.
        }

        Changed?.Invoke();
    }
}
