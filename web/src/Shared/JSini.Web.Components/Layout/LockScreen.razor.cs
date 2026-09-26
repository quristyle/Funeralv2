using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using JSini.Web.Http;
using JSini.Web.Components.Security;

namespace JSini.Web.Components.Layout;

public partial class LockScreen
{
    [Inject] private ScreenLock Lock { get; set; } = default!;
    [Inject] private GatewayClient Gateway { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>덮개에 띄우는 이름. 누구 화면인지 알려 준다.</summary>
    [Parameter]
    public string? UserName { get; set; }

    private string _password = string.Empty;
    private string? _error;
    private bool _busy;

    private string _name = string.Empty;

    /// <summary>이 브라우저가 기기 확인을 할 수 있는가. 잠긴 뒤에 한 번 묻는다.</summary>
    private LockPasskeyBrowserDto? _browser;

    /// <summary>이미 물어봤는가. 다시 그릴 때마다 왕복을 하나 더 쓰지 않는다.</summary>
    private bool _browserAsked;

    /// <summary>
    /// 이 계정에 등록된 기기가 하나도 없다는 것을 <b>눌러 보고</b> 알았다.
    /// 그 뒤로는 단추를 감춘다 — 남겨 두면 누를 때마다 같은 말만 나온다.
    /// </summary>
    private bool _passkeyGone;

    /// <summary>지문 단추를 그릴 것인가.</summary>
    /// <remarks>
    /// <c>PlatformAuthenticator</c> 까지 보는 것은 잠금화면이 **이 기기 앞에
    /// 선 사람**을 상대하기 때문이다. 보안 열쇠(USB)만 있는 경우까지 단추를
    /// 띄우면 자리를 비웠다 돌아온 사람이 꽂을 것을 찾게 된다 — 등록
    /// 화면(「내 정보 → 보안 설정」)이 그 둘을 가르지 않는 것과 다르다.
    /// </remarks>
    private bool PasskeyReady =>
        !_passkeyGone && _browser is { Supported: true, PlatformAuthenticator: true };

    /// <summary>지문 단추를 비밀번호 칸 <b>위로</b> 올릴 것인가.</summary>
    private bool PasskeyFirst => PasskeyReady && _browser?.Priority == "passkey";

    protected override void OnInitialized() => Lock.Changed += OnLockChanged;

    protected override void OnParametersSet() => _name = UserName ?? string.Empty;

    /// <summary>
    /// 잠긴 뒤에 <b>한 번만</b> 브라우저에게 묻는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>잠기기 전에 묻지 않는다.</b> 이 부품은 로그인한 모든 화면에 얹혀
    /// 있어서, 미리 물으면 아무도 잠그지 않는 날에도 사람마다 왕복이 하나씩
    /// 는다(web/CLAUDE.md 「`PortalBoot` 한 곳으로 모은다」가 줄이려던 그 왕복이다).
    /// </para>
    /// <para>
    /// <see cref="OnAfterRenderAsync"/> 인 것은 <b>프리렌더 중에는 JS 를 부를
    /// 수 없기</b> 때문이다. 새로고침으로 잠긴 채 되살아나는 길도 결국 여기를
    /// 지난다 — 그때는 <c>ScreenLock.RestoreAsync</c> 가 상태를 바꾸면서
    /// 다시 그리게 하고, 그 다음 차례가 이 메서드다.
    /// </para>
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Lock.IsLocked || _browserAsked)
        {
            return;
        }

        _browserAsked = true;

        try
        {
            _browser = await JS.InvokeAsync<LockPasskeyBrowserDto>("jsiniPasskey.status");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            // 못 물었으면 **없는 것으로 본다.** 비밀번호 칸은 그대로 있으므로
            // 사람이 갇히지 않는다. 반대로 있다고 치고 단추를 띄우면 누를
            // 때마다 아무 일도 일어나지 않는다.
            _browser = null;
        }

        StateHasChanged();
    }

    private void OnLockChanged()
    {
        if (!Lock.IsLocked)
        {
            // 걷힐 때 손에 든 것을 지운다. 남겨 두면 다음에 잠글 때
            // 지난번 오류 문구가 그대로 떠 있다.
            _password = string.Empty;
            _error = null;

            // **기기가 없다는 판정은 남기지 않는다.** 보안 설정에서 지문을
            // 등록하고 다시 잠그는 것이 흔한 순서인데, 남겨 두면 그 사람에게
            // 단추가 영영 안 보인다(회로가 살아 있는 동안 계속).
            _passkeyGone = false;
        }

        InvokeAsync(StateHasChanged);
    }

    /// <summary>엔터로도 풀린다. 폼이 없으므로 여기서 직접 받는다.</summary>
    private Task OnKeyDownAsync(KeyboardEventArgs e) =>
        e.Key is "Enter" or "NumpadEnter" ? UnlockAsync() : Task.CompletedTask;

    private async Task UnlockAsync()
    {
        if (_busy)
        {
            return;
        }

        _error = null;

        if (string.IsNullOrEmpty(_password))
        {
            _error = "비밀번호를 입력해 주세요.";
            return;
        }

        _busy = true;
        try
        {
            var matched = await Gateway.PostAsync<bool>(
                "auth/user/verify-password", new { password = _password });

            if (matched)
            {
                await Lock.UnlockAsync();
                return;
            }

            _error = "비밀번호가 맞지 않습니다.";
            _password = string.Empty;
        }
        catch (ApiException ex)
        {
            // 시도 제한(429)에 걸리면 그 말을 그대로 보여 준다.
            // 「비밀번호가 틀렸다」로 뭉뚱그리면 몇 분을 더 헛되이 두드린다.
            _error = ex.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// 지문·얼굴로 푼다. 왕복이 셋이다 — 도전값 받기 · 기기에게 서명 받기 ·
    /// 서명 확인받기.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 가운데만 브라우저에서 돌고 나머지 둘은 회로가 게이트웨이를 부른다.
    /// <b>토큰이 브라우저로 내려가는 자리가 없다</b>(web/CLAUDE.md 「인증 — BFF」).
    /// 로그인 화면이 셸의 <c>/passkey/options</c> 를 거치는 것은 그 화면에
    /// 회로가 없어서지, 그쪽이 더 옳아서가 아니다.
    /// </para>
    /// <para>
    /// <b>실패해도 비밀번호 칸을 지우지 않는다.</b> 지문을 대려다 취소한
    /// 사람이 치던 비밀번호를 잃으면, 고친 것이 아니라 하나 더 망가뜨린 것이다.
    /// </para>
    /// </remarks>
    private async Task UnlockWithPasskeyAsync()
    {
        if (_busy)
        {
            return;
        }

        _error = null;
        _busy = true;

        try
        {
            var options = await Gateway.PostAsync<LockPasskeyOptionsDto>(LockPasskey.OptionsPath, null);

            if (options is null)
            {
                _error = "지금은 기기 인증을 쓸 수 없습니다. 비밀번호로 풀어 주세요.";
                return;
            }

            var signed = await JS.InvokeAsync<LockPasskeyAssertionDto>("jsiniPasskey.verify", options);

            if (signed is not { Ok: true })
            {
                // 이 계정에 등록된 기기가 없다는 답이면 단추를 접는다.
                _passkeyGone = signed?.Empty ?? false;

                // 까닭은 `passkey.js` 가 이미 사람 말로 옮겨 두었다.
                _error = signed?.Error ?? "기기 인증에 실패했습니다.";
                return;
            }

            var matched = await Gateway.PostAsync<bool>(LockPasskey.VerifyPath, new
            {
                signed.SessionId,
                signed.CredentialId,
                signed.AuthenticatorData,
                signed.ClientDataJson,
                signed.Signature,
                signed.UserHandle,
            });

            if (matched)
            {
                await Lock.UnlockAsync();
                return;
            }

            // 서버가 무엇을 걸렀는지는 **일부러 말하지 않는다** — 「등록된
            // 기기가 아니다」와 「서명이 틀렸다」를 갈라 주면 잠긴 화면 앞에서
            // 남의 기기를 골라내는 데 쓰인다. 기록에는 까닭이 남아 있다.
            _error = "기기를 확인하지 못했습니다. 비밀번호로 풀어 주세요.";
        }
        catch (ApiException ex)
        {
            // 시도 제한(429)이 비밀번호와 같은 통이다. 그 말을 그대로 보여 준다.
            _error = ex.Message;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            _error = "기기 확인을 열지 못했습니다. 비밀번호로 풀어 주세요.";
        }
        finally
        {
            _busy = false;
        }
    }

    public void Dispose() => Lock.Changed -= OnLockChanged;
}
