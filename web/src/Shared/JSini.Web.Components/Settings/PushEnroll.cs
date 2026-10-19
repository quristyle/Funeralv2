using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.Models;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Settings;

/// <summary>이 브라우저를 웹푸시 구독에 등록하는 절차 한 벌. 부르는 자리는 알림 판과 로그인 뒤 권유 창 둘이다 — 순서를 틀리면 조용히 깨지는 대목이 셋 있다.
/// (1) 브라우저에서 먼저 만들고 서버에 올린다 — 반대면 서버에만 남은 죽은 구독이 발송마다 실패한다. (2) 서버가 안 받으면 브라우저 쪽도 되돌린다 — 남겨 두면 다시 눌러도 이미 있다로 통과해 못 고친다. (3) 구독은 받겠다는 뜻이라 푸시 스위치가 꺼져 있으면 함께 켠다.
/// 권한 요청은 사람이 누른 사슬에서만 받아들여지므로 단추의 Click 에서 부른다. 회로를 한 번 건너가지만 왕복은 수십 ms라 브라우저의 활성 시간 안에 들어온다.</summary>
public sealed class PushEnroll(
    NotificationClient api,
    IJSRuntime js,
    ILogger<PushEnroll> logger)
{
    /// <summary>지금 이 브라우저의 사정(지원 여부 · 권한 · 구독 · 실행 방식). 예외는 삼킨다 — 스크립트가 아직 안 실렸거나 막힌 경우에 올바른 동작은 이 브라우저는 못 받는다로 보는 것이지 화면을 세우는 것이 아니다.</summary>
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

    /// <summary>이 브라우저를 등록한다. 머리말의 순서 셋을 지킨다.</summary>
    /// <param name="settings">방금 읽어 온 내 알림 설정. 공개 키(VAPID)와 지금 스위치 상태가 들어 있어 부르는 쪽이 읽어 둔 것을 넘긴다(왕복을 또 내지 않으려고).</param>
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

/// <summary>등록 결과. 말까지 담아 돌려준다 — 성공·실패의 문구를 부르는 쪽마다 적으면 같은 일에 두 가지 말이 생긴다.</summary>
/// <param name="Ok">등록됐는가. 스위치를 못 켠 경우도 true 다 — 등록은 됐다.</param>
/// <param name="Message">사용자에게 보일 한 줄.</param>
/// <param name="Tone">그 한 줄의 무게. 토스트가 색과 머무는 시간을 정한다.</param>
public sealed record PushEnrollResult(bool Ok, string Message, NoticeTone Tone);
