using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Models;
using JSini.Web.Components.Settings;

namespace JSini.Web.Components.Layout;

public partial class PushAskPopup
{
    [Inject] private NotificationClient Api { get; set; } = default!;
    [Inject] private PushEnroll Enroll { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<PushAskPopup> Log { get; set; } = default!;

    /// <summary>
    /// 휴대폰에서 <b>접어 둔 뒤 다시 권하기까지</b>.
    ///
    /// <para>
    /// 15분이다. 더 짧으면 한 업무를 보는 사이에 두 번 뜨고, 더 길면 잠깐
    /// 들렀다 나가는 사람에게는 한 번도 안 뜬 것과 같다.
    /// </para>
    /// </summary>
    private static readonly TimeSpan ReofferAfter = TimeSpan.FromMinutes(15);

    /// <summary>지금 이 브라우저의 사정. 창을 띄울지 여기서 갈린다.</summary>
    private PushBrowserResult? _browser;

    /// <summary>방금 읽어 온 내 설정. 공개 키(VAPID)가 여기 있다.</summary>
    private NotificationSettingsDto? _settings;

    private bool _open;
    private bool _busy;

    /// <summary>휴대폰·태블릿인가. 되풀이 권유가 여기서 갈린다.</summary>
    private bool _mobile;

    /// <summary>이번에 설치를 권하는가.</summary>
    private bool _askInstall;

    /// <summary>이번에 구독을 권하는가.</summary>
    private bool _askSubscribe;

    /// <summary>데스크톱에서 이 탭에 「나중에」를 눌러 두었는가.</summary>
    private bool _closedThisTab;

    /// <summary>데스크톱에서 「다시 묻지 않기」를 눌러 두었는가.</summary>
    private bool _never;

    /// <summary>휴대폰에서 접어 둔 기한(UTC). 없으면 지금 물어도 된다.</summary>
    private DateTime? _snoozeUntil;

    /// <summary>기한까지 자고 있는 시계. 부품이 사라질 때 깨워 끝낸다.</summary>
    private CancellationTokenSource? _wake;

    private string Title => (_askInstall, _askSubscribe) switch
    {
        (true, true) => "앱으로 설치하고 알림을 받으시겠습니까?",
        (true, false) => "홈 화면에 앱으로 두시겠습니까?",
        _ => "알림을 받으시겠습니까?",
    };

    /// <summary>
    /// <b>첫 렌더 뒤에</b> 살핀다. 프리렌더 중에는 JS 를 부를 수 없고
    /// (브라우저가 아직 없다) 이 창의 판단은 전부 브라우저에서 온다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // ① 브라우저 저장소. **왕복을 따로 내지 않는다** — 레이아웃이 내는
        //    공용 읽기에 열쇠 셋을 보탠 것이 전부다(`PortalBoot`).
        var saved = await Boot.ReadAsync();

        if (saved.ScreenLocked)
        {
            return;
        }

        _closedThisTab = saved.PushAskClosed;
        _never = saved.PushAskNever;
        _snoozeUntil = saved.PushAskSnoozedUntil;

        await ConsiderAsync();
    }

    /// <summary>
    /// 지금 권할 것이 있는지 보고, 있으면 창을 연다.
    ///
    /// <para>
    /// <b>부르는 자리가 둘이다</b> — 첫 렌더와, 접어 둔 기한이 지나 시계가
    /// 깨운 때(<see cref="ScheduleReofferAt"/>). 둘이 같은 판단을 써야 해서
    /// 한 벌만 둔다.
    /// </para>
    /// </summary>
    private async Task ConsiderAsync()
    {
        // ② 이 브라우저가 지금 어떤 상태인가. 휴대폰인지도 여기서 온다.
        _browser = await Enroll.StatusAsync();
        _mobile = _browser.Mobile;

        if (_mobile)
        {
            if (_snoozeUntil is { } until && DateTime.UtcNow < until)
            {
                // 아직 접어 둔 기한 안이다. 그 시각에 다시 본다.
                ScheduleReofferAt(until);
                return;
            }
        }
        else if (_closedThisTab || _never)
        {
            // 데스크톱은 한 번 묻고 만다.
            return;
        }

        _askInstall = WantsInstall;
        _askSubscribe = WantsSubscribe;

        // ③ 서버가 보낼 수 있는가, 그리고 이 사람이 스스로 끈 적은 없는가.
        //    **구독을 권할 때만 부른다** — 설치만 권하는 자리에서 이 왕복을
        //    내면 아무것도 갈리지 않으면서 게이트웨이만 두드린다.
        if (_askSubscribe)
        {
            try
            {
                _settings = await Api.GetMyPreferencesAsync();
            }
            catch (Exception ex)
            {
                // 설정을 못 읽었다고 화면을 세우지 않는다. 구독을 못 권한
                // 것뿐이고, 설치는 그대로 권할 수 있다.
                Log.LogDebug(ex, "알림 설정을 읽지 못해 구독 권유를 건너뛴다.");
                _settings = null;
            }

            // **스스로 끈 사람에게는 되묻지 않는다.** 한 번 정한 뜻을 로그인
            // 할 때마다 되묻는 것은 권유가 아니라 조르기다.
            if (_settings is not { PushAvailable: true }
                || _settings.Preference is { Saved: true, PushEnabled: false })
            {
                _askSubscribe = false;
            }
        }

        // 데스크톱에서는 **구독을 권할 때만** 이 창을 연다. 앱 설치 하나
        // 때문에 여태 안 뜨던 자리에서 창이 새로 뜨게 하지 않는다 — 거기서는
        // 탭으로 써도 알림이 온다.
        if (!_mobile && !_askSubscribe)
        {
            return;
        }

        if (!_askInstall && !_askSubscribe)
        {
            // 지금은 권할 것이 없다. 휴대폰에서 **아직 다 끝난 것이 아니면**
            // 나중에 길이 열릴 수 있으므로(설치 신호가 늦게 오는 브라우저가
            // 있다) 기한을 두고 한 번 더 본다.
            if (_mobile && !Done)
            {
                ScheduleReofferAt(DateTime.UtcNow.Add(ReofferAfter));
            }

            return;
        }

        _open = true;
        StateHasChanged();
    }

    /// <summary>
    /// 설치를 권할 자리인가. <b>단추를 놓을 수 있는가와는 다른 물음</b>이다 —
    /// 사파리는 권하되 단추 대신 길을 적어 준다(머리말의 갈래 표).
    /// </summary>
    private bool WantsInstall =>
        _browser is { Standalone: false, Installed: false }
        && _browser.InstallHint is "prompt" or "ios";

    /// <summary>구독을 권할 자리인가. 차단으로 굳었으면 여기서 할 일이 없다.</summary>
    private bool WantsSubscribe =>
        _browser is { Supported: true, Subscribed: false } && _browser.Permission != "denied";

    /// <summary>
    /// 이 브라우저에서 <b>더 할 일이 없는가</b>. 앱으로 열려 있고 구독도
    /// 되어 있으면 이 창은 다시 뜨지 않는다 — 되풀이 권유가 멈추는 자리다.
    /// </summary>
    private bool Done =>
        _browser is { Subscribed: true } && (_browser.Standalone || _browser.Installed);

    /// <summary>
    /// 「앱 설치」. <b>이 클릭에서 브라우저의 설치 창이 이어진다</b> — 화면이
    /// 저절로 부르면 조용히 거절된다(알림 권한과 같다).
    /// </summary>
    private async Task InstallAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            PushBrowserResult? result = null;

            try
            {
                result = await Js.InvokeAsync<PushBrowserResult>("jsiniPwa.install");
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "설치 창을 띄우지 못했다.");
                Toasts.Show($"설치 창을 띄우지 못했습니다: {ex.Message}", NoticeTone.Warning);
            }

            if (result is { Ok: true })
            {
                Toasts.Show("앱으로 설치했습니다. 홈 화면의 아이콘으로 여시면 창을 닫아 두어도 알림이 옵니다.", NoticeTone.Info);
            }
            else if (result is not null)
            {
                // 설치 창에서 취소한 경우가 대부분이다. 까닭은 pwa.js 가 담아 준다.
                Toasts.Show(result.Error ?? "설치를 마치지 못했습니다.", NoticeTone.Warning);
            }

            await AfterActionAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// 「알림 받기」. <b>이 클릭에서 브라우저 권한 요청이 이어진다</b> —
    /// 절차는 <see cref="PushEnroll"/> 이 갖고 있다(알림 판과 같은 한 벌).
    /// </summary>
    private async Task SubscribeAsync()
    {
        if (_busy || _settings is null)
        {
            return;
        }

        _busy = true;

        try
        {
            var result = await Enroll.SubscribeAsync(_settings);

            Toasts.Show(result.Message, result.Tone);

            await AfterActionAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// 단추를 누른 뒤. <b>상태를 다시 읽어 남은 일만 남긴다.</b>
    ///
    /// <para>
    /// 설치와 구독을 한 창에서 다루므로 <b>하나를 마쳤다고 닫지 않는다</b> —
    /// 설치를 끝낸 사람에게 남은 구독 단추를 그 자리에서 마저 보여 준다.
    /// 둘 다 끝났으면 그때 닫는다.
    /// </para>
    ///
    /// <para>
    /// 실패했을 때도 여기로 온다. 그때는 상태가 그대로라 창이 열려 있고,
    /// 까닭은 토스트에 떴다 — 주소창에서 풀고 다시 누를 수 있어야 한다.
    /// </para>
    /// </summary>
    private async Task AfterActionAsync()
    {
        _browser = await Enroll.StatusAsync();

        _askInstall = WantsInstall;
        _askSubscribe = _askSubscribe && WantsSubscribe;

        if (_askInstall || _askSubscribe)
        {
            StateHasChanged();
            return;
        }

        _open = false;

        // 데스크톱에서만 표시를 남긴다. 휴대폰은 **끝났으면 애초에 안 뜨고**
        // (`Done`), 안 끝났으면 기한을 두고 다시 물어야 한다.
        if (!_mobile)
        {
            await RememberAsync(session: true);
        }
        else if (!Done)
        {
            await SnoozeAsync();
        }

        StateHasChanged();
    }

    /// <summary>
    /// 「나중에」와 X.
    ///
    /// <para>
    /// 데스크톱에서는 <b>이 탭에서만</b> 그만 묻고, 휴대폰에서는
    /// <see cref="ReofferAfter"/> 만큼 <b>접어 둔다</b> — 거절이 아니라
    /// 미루기다(머리말).
    /// </para>
    /// </summary>
    private async Task LaterAsync(bool visible)
    {
        if (visible)
        {
            return;
        }

        _open = false;

        if (_mobile)
        {
            await SnoozeAsync();
        }
        else
        {
            await RememberAsync(session: true);
        }
    }

    /// <summary>「다시 묻지 않기」. 이 브라우저에 영영 적어 둔다(데스크톱만).</summary>
    private async Task NeverAsync()
    {
        _open = false;
        await RememberAsync(session: false);

        // **아무 말도 하지 않는다.** 끈 것이 아니라 안 묻기로 한 것이라
        // 알릴 결과가 없고, 토스트를 띄우면 방금 치운 말이 다시 뜬다.
    }

    /// <summary>
    /// 기한을 적어 두고 그때 다시 뜨게 한다. <b>기한 글자를 굽는 일은
    /// <see cref="PortalBoot"/> 가 한다</b> — 열쇠와 꼴을 아는 자리가 둘이면
    /// 한쪽만 고치는 날이 온다.
    /// </summary>
    private async Task SnoozeAsync()
    {
        _snoozeUntil = await Boot.SnoozePushAskAsync(ReofferAfter);
        ScheduleReofferAt(_snoozeUntil.Value);
    }

    /// <summary>
    /// <paramref name="until"/> 까지 한 번 자고 일어나 다시 살핀다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>촘촘히 훑지 않는다.</b> 1분마다 상태를 읽으면 회로 하나가 한 시간에
    /// 왕복 예순을 낸다 — 이 창이 기다리는 것은 「기한이 지났는가」 하나뿐이라
    /// 그 시각에 한 번만 깨면 된다.
    /// </para>
    /// <para>
    /// 앞서 걸어 둔 시계가 있으면 걷는다. 겹쳐 두면 창이 두 번 열린다.
    /// </para>
    /// </remarks>
    private void ScheduleReofferAt(DateTime until)
    {
        _wake?.Cancel();
        _wake?.Dispose();
        _wake = new CancellationTokenSource();

        _ = ReofferAsync(until, _wake.Token);
    }

    private async Task ReofferAsync(DateTime until, CancellationToken token)
    {
        try
        {
            var wait = until - DateTime.UtcNow;

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, token);
            }

            // **회로의 차례로 돌아와서** 화면을 건드린다. 시계는 회로 밖에서
            // 도므로 여기서 바로 그리면 렌더링이 엉킨다.
            await InvokeAsync(async () =>
            {
                if (_open || _busy)
                {
                    return;
                }

                _snoozeUntil = null;
                await ConsiderAsync();
            });
        }
        catch (OperationCanceledException)
        {
            // 부품이 사라졌다. 할 일이 없다.
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException)
        {
            // 회로가 먼저 끊겼다. 다음에 열 때 처음부터 다시 살핀다.
            Log.LogDebug(ex, "설치·구독 권유를 다시 띄우려는데 회로가 이미 끊겼다.");
        }
        catch (Exception ex)
        {
            Log.LogDebug(ex, "설치·구독 권유를 다시 띄우지 못했다.");
        }
    }

    /// <summary>
    /// 닫았다는 표시를 브라우저에 남긴다(데스크톱).
    ///
    /// <para>
    /// <b>부품 수명에 기대지 않는다.</b> 업무를 옮기면 레이아웃이 통째로 다시
    /// 만들어져 <c>firstRender</c> 가 또 참이 된다 — 공지 팝업이 같은 곳을
    /// 밟았다(<see cref="NoticeAutoPopup"/> 머리말).
    /// </para>
    /// </summary>
    private async Task RememberAsync(bool session)
    {
        try
        {
            if (session)
            {
                await Js.InvokeVoidAsync("sessionStorage.setItem", PortalBoot.PushAskClosedKey, "1");
            }
            else
            {
                await Js.InvokeVoidAsync("localStorage.setItem", PortalBoot.PushAskNeverKey, "1");
            }
        }
        catch (JSException ex)
        {
            // 사생활 보호 모드에서는 setItem 이 던진다. 이번 탭에서 한 번 더
            // 뜨는 것이 전부라 사용자에게 말할 일이 아니다.
            Log.LogDebug(ex, "구독 권유를 닫았다는 표시를 남기지 못했습니다.");
        }
    }

    /// <summary>
    /// 자고 있는 시계를 깨워 끝낸다. <b>없으면 업무를 옮길 때마다 시계가
    /// 하나씩 쌓인다</b> — 레이아웃은 전환마다 새로 만들어진다.
    /// </summary>
    public void Dispose()
    {
        _wake?.Cancel();
        _wake?.Dispose();
        _wake = null;
    }
}
