using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.Models;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Settings;

/// <summary>
/// 이 브라우저를 웹푸시 구독에 <b>등록하는 절차 한 벌</b>.
/// </summary>
/// <remarks>
/// <para>
/// [왜 절차를 따로 떼어 두나]
/// </para>
///
/// <para>
/// 이 절차를 부르는 자리가 둘이다 — 알림 판의 「이 브라우저에서 알림 받기」
/// (<see cref="NotificationPanel"/>)와 로그인 뒤에 스스로 뜨는 권유 창
/// (<c>PushAskPopup</c>). 그런데 이 절차에는 <b>순서를 틀리면 조용히 깨지는
/// 대목이 셋</b> 있다.
/// </para>
///
/// <list type="number">
///   <item>
///     브라우저에서 <b>먼저 만들고</b> 서버에 올린다. 반대로 하면 서버에는
///     있는데 브라우저에는 없는 구독이 남고, 그쪽은 발송이 계속 실패하면서도
///     화면에는 등록된 것으로 보인다.
///   </item>
///   <item>
///     서버가 안 받으면 <b>브라우저 쪽도 되돌린다.</b> 남겨 두면 다시
///     등록을 눌러도 「이미 있다」로 통과해 버려 영영 못 고친다.
///   </item>
///   <item>
///     구독은 <b>「받겠다」는 뜻</b>이다. 푸시 스위치가 꺼져 있으면 함께 켠다 —
///     그러지 않으면 등록은 됐는데 아무것도 오지 않는 상태로 남고, 화면만
///     봐서는 정상으로 보인다.
///   </item>
/// </list>
///
/// <para>
/// 이 셋을 부르는 자리마다 한 벌씩 적으면 <b>반드시 갈라진다</b>. 그래서
/// 절차는 여기 하나뿐이고, 부르는 쪽은 결과를 받아 말만 한다.
/// </para>
///
/// <para>
/// [권한 요청은 사용자 클릭에서 이어져야 한다]
/// </para>
///
/// <para>
/// <see cref="SubscribeAsync"/> 안에서 브라우저가 알림 권한을 묻는다
/// (<c>Notification.requestPermission</c>). 그 물음은 <b>사람이 누른 그
/// 사슬에서만</b> 받아들여진다 — 화면이 뜨자마자 부르면 브라우저가 조용히
/// 거절한다. 그러니 <b>단추의 <c>Click</c> 에서 부른다.</b> 타이머나
/// <c>OnAfterRender</c> 에서 부르지 않는다.
/// </para>
///
/// <para>
/// 회로를 한 번 건너가지만(Blazor Server) 그 왕복은 보통 수십 ms 라 브라우저가
/// 인정하는 활성 시간(수 초) 안에 들어온다. 알림 판이 여태 그렇게 해 왔고
/// 실제로 받아진다.
/// </para>
/// </remarks>
public sealed class PushEnroll(
    NotificationClient api,
    IJSRuntime js,
    ILogger<PushEnroll> logger)
{
    /// <summary>
    /// 지금 이 브라우저의 사정(지원 여부 · 권한 · 구독 · 실행 방식).
    ///
    /// <para>
    /// <b>예외를 삼킨다.</b> 스크립트가 아직 안 실렸거나 브라우저가 막아 둔
    /// 경우인데, 그때 올바른 동작은 「이 브라우저는 못 받는다」로 보는 것이지
    /// 화면을 세우는 것이 아니다.
    /// </para>
    /// </summary>
    public async Task<PushBrowserResult> StatusAsync()
    {
        try
        {
            return await js.InvokeAsync<PushBrowserResult>("jsiniPwa.status")
                ?? Unsupported;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogDebug(ex, "브라우저의 푸시 상태를 읽지 못했다. 못 받는 것으로 본다.");
            return Unsupported;
        }
    }

    private static PushBrowserResult Unsupported =>
        new() { Supported = false, Permission = "unsupported" };

    /// <summary>
    /// 이 브라우저를 등록한다. 머리말의 순서 셋을 지킨다.
    /// </summary>
    /// <param name="settings">
    /// 방금 읽어 온 내 알림 설정. <b>공개 키(VAPID)와 지금 스위치 상태</b>가
    /// 여기 들어 있다 — 부르는 쪽이 이미 읽어 둔 것을 넘긴다(왕복을 또 내지
    /// 않으려는 것이다).
    /// </param>
    public async Task<PushEnrollResult> SubscribeAsync(NotificationSettingsDto settings)
    {
        var key = settings.VapidPublicKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            return new PushEnrollResult(false, "서버에 푸시 발송 키(VAPID)가 없어 등록할 수 없습니다.", NoticeTone.Warning);
        }

        // ① 브라우저에서 먼저 만든다. 여기서 권한을 묻는다.
        PushBrowserResult? made;

        try
        {
            made = await js.InvokeAsync<PushBrowserResult>("jsiniPwa.subscribe", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "브라우저에서 구독을 만들지 못했다.");
            return new PushEnrollResult(false, $"브라우저에서 구독을 만들지 못했습니다: {ex.Message}", NoticeTone.Error);
        }

        if (made is not { Ok: true })
        {
            // 권한을 거절한 경우가 대부분이다. 까닭은 pwa.js 가 담아 준다.
            return new PushEnrollResult(false, made?.Error ?? "브라우저에서 구독을 만들지 못했습니다.", NoticeTone.Warning);
        }

        // ② 서버에 올린다. 못 올리면 브라우저 쪽도 되돌린다.
        try
        {
            await api.RegisterPushSubscriptionAsync(new PushSubscribeRequest
            {
                Endpoint = made.Endpoint,
                P256dh = made.P256dh,
                Auth = made.Auth,
                Metadata = made.Metadata ?? new(),
            });
        }
        catch (ApiException ex)
        {
            try
            {
                await js.InvokeAsync<PushBrowserResult>("jsiniPwa.unsubscribe");
            }
            catch (Exception undo)
            {
                // 되돌리기까지 실패한 것은 사용자에게 말할 것이 아니다 —
                // 할 수 있는 일이 없고, 다음 등록 때 같은 구독을 다시 쓴다.
                logger.LogDebug(undo, "서버 등록 실패 뒤 브라우저 구독을 되돌리지 못했다.");
            }

            return new PushEnrollResult(false, $"등록 정보를 서버에 올리지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }

        // ③ 스위치가 꺼져 있으면 함께 켠다.
        if (!settings.Preference.PushEnabled)
        {
            settings.Preference.PushEnabled = true;

            try
            {
                await api.SaveMyPreferencesAsync(settings.Preference);
                settings.Preference.Saved = true;
                settings.Preference.UpdatedAt = DateTime.UtcNow;
            }
            catch (ApiException ex)
            {
                // 등록 자체는 됐다. 스위치만 못 켠 것이라 **실패로 말하지
                // 않는다** — 다만 그대로 두면 아무것도 안 오므로 말은 해 준다.
                logger.LogWarning(ex, "구독은 만들었으나 푸시 스위치를 켜지 못했다.");

                return new PushEnrollResult(true,
                    "이 브라우저를 등록했습니다. 다만 푸시 스위치를 켜지 못했습니다 — 환경설정에서 켜 주십시오.",
                    NoticeTone.Warning);
            }
        }

        return new PushEnrollResult(true, "이 브라우저를 등록했습니다. 이제 알림이 옵니다.", NoticeTone.Info);
    }
}

/// <summary>
/// 등록 결과. <b>말까지 담아 돌려준다</b> — 성공·실패의 문구를 부르는 쪽마다
/// 적으면 같은 일에 두 가지 말이 생긴다.
/// </summary>
/// <param name="Ok">등록됐는가. 스위치를 못 켠 경우도 <c>true</c> 다(등록은 됐다).</param>
/// <param name="Message">사용자에게 보일 한 줄.</param>
/// <param name="Tone">그 한 줄의 무게. 토스트가 색과 머무는 시간을 정한다.</param>
public sealed record PushEnrollResult(bool Ok, string Message, NoticeTone Tone);
