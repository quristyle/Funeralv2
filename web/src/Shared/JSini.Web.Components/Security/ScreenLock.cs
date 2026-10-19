using Microsoft.JSInterop;

namespace JSini.Web.Components.Security;

/// <summary>
/// 잠금화면의 상태. 회로(창) 하나마다 하나다. vben 의 LockScreen 을 옮겼다(D7). 자리를 비울 때 덮고, 돌아와 비밀번호나 지문으로 푼다.
/// <b>보안 경계가 아니다.</b> 잠긴 동안에도 토큰이 살아 있어 개발자 도구로 우회할 수 있다. 막으려는 것은 지나가는 사람이다.
/// 표시는 <c>localStorage</c> 에 둔다. 세션 저장소면 PWA 를 껐다 켜는 것만으로 덮개가 걷히므로 잠금이 아니다. 지우는 곳은 로그인 화면(<c>Login.razor</c> 의 <c>jsiniLock.forget</c>)이다. 안 지우면 잠금화면에서 로그아웃한 사람이 같은 비밀번호를 두 번 치게 된다.
/// </summary>
public sealed class ScreenLock(IJSRuntime js, Layout.PortalBoot boot)
{
    /// <summary>브라우저에 남기는 표시. 정본은 <see cref="Layout.PortalBoot.ScreenLockedKey"/> 다. 열쇠를 두 곳에 적으면 한쪽만 고치는 날이 오고, 증상은 「잠갔는데 새로고침하면 풀린다」다.</summary>
    private const string StorageKey = Layout.PortalBoot.ScreenLockedKey;

    private bool _restored;

    /// <summary>지금 잠겨 있는가.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>잠기거나 풀렸을 때. 레이아웃이 다시 그리려고 듣는다.</summary>
    public event Action? Changed;

    /// <summary>브라우저에 남은 표시를 읽어 상태를 되살린다. 회로가 붙은 뒤(OnAfterRenderAsync) 불러야 한다. 값은 PortalBoot 가 다른 값과 한 왕복으로 읽어 온다. 못 읽으면 안 잠긴 것으로 본다. 읽기 실패로 덮으면 풀 길이 없다.</summary>
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
                await js.InvokeVoidAsync("localStorage.setItem", StorageKey, "1");
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            }
        }
        catch (JSException)
        {
            // 화면은 이미 덮였거나 걷혔다. 표시를 못 남긴 것뿐이라 새로고침하면 풀린다.
        }

        Changed?.Invoke();
    }
}
