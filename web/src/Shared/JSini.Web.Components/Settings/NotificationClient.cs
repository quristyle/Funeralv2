using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Settings;

/// <summary>로그인한 본인의 알림 설정과 웹푸시 구독을 다룬다. 설정 화면이 포털관리와 장례식장 둘이라 업무 모듈끼리 참조하지 않도록 공용 자리에 둔다(NoticeClient 와 같은 까닭).
/// 주소에는 notification 과 notifications 가 둘 다 있다 — 앞엣것은 게이트웨이 접두사, 뒤엣것은 서비스 안의 묶음이라 하나를 빠뜨리면 404 인데 화면에는 설정을 못 읽었다로만 보인다.</summary>
public sealed class NotificationClient(GatewayClient gateway)
{
    /// <summary>내 설정. 설정과 함께 푸시를 보낼 수 있는 서버인지(pushAvailable), 구독에 쓸 공개 키, 등록된 기기가 온다.</summary>
    public Task<NotificationSettingsDto?> GetMyPreferencesAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<NotificationSettingsDto>("notification/notifications/preferences/me", ct);

    /// <summary>설정을 저장한다. 감싸지 않고 설정만 보낸다.</summary>
    public Task SaveMyPreferencesAsync(NotificationPreferenceDto pref, CancellationToken ct = default)
        => gateway.PutAsync("notification/notifications/preferences/me", pref, ct);

    /// <summary>좌표만 저장한다. 설정 전체를 보내는 SaveMyPreferencesAsync 와 주소는 같고 싣는 것이 다르다 — 위치 저장은 뒤에서도 돌아(30분마다) 전체를 보내면 다른 탭에서 방금 바꾼 스위치를 되돌린다. weatherLocated 가 방금 잰 것임을 알려 「확인한 때」를 찍게 한다.</summary>
    public Task SaveMyLocationAsync(LocationUpdateDto update, CancellationToken ct = default)
        => gateway.PutAsync("notification/notifications/preferences/me", update, ct);

    /// <summary>등록된 기기 목록. <b>배열이 아니라 <c>{ items, count }</c></b> 다.</summary>
    public Task<PushSubscriptionListDto?> GetMySubscriptionsAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<PushSubscriptionListDto>("notification/notifications/subscriptions/me", ct);

    /// <summary>시험 발송. 내 기기로 한 통 보낸다. 서버는 보낸 것이 없어도 성공 계열(202)로 답하고 까닭을 message 에 담으므로, 그것을 안 보고 「보냈습니다」라 하면 알림이 안 오는 사람이 기기를 의심한다.</summary>
    public Task<PushSendResultDto?> SendTestPushAsync(CancellationToken ct = default)
        => gateway.PostAsync<PushSendResultDto>("notification/notifications/push/test", new { }, ct);

    /// <summary>이 브라우저의 푸시 구독을 등록한다. 구독은 브라우저가 만들고(pwa.js), 여기서는 결과를 서버에 옮길 뿐이다. 주인은 보내지 않는다 — 서버가 로그인한 계정으로 정하고, 다른 주인은 403 으로 막는다(남의 이름으로 구독을 만들면 그 사람 알림을 가로챈다).</summary>
    public Task RegisterPushSubscriptionAsync(
        PushSubscribeRequest request, CancellationToken ct = default)
        => gateway.PostAsync("notification/notifications/subscriptions", request, ct);

    /// <summary>알림 한 건을 읽음으로 찍는다. 열쇠는 발송 한 번(batch_id)이라 기기가 여럿이어도 한 번에 찍힌다. 알림을 눌러 들어온 사람을 위해 공용 자리에 있다 — 서비스워커가 열 주소에 표시를 얹고 PushClickRead 가 이 호출을 한다. 포털관리의 내 알림함에도 같은 호출이 있지만 목록과 한 벌인 화면용이라 그대로 둔다.</summary>
    public Task MarkInboxReadAsync(string id, CancellationToken ct = default)
        => gateway.PostAsync(
            $"notification/notifications/inbox/{Uri.EscapeDataString(id)}/read", new { }, ct);

    /// <summary>구독을 지운다. 열쇠는 endpoint 다 — 브라우저에서 끊기 전에 그 값을 꺼내 두어야 한다. 못 지우면 서버에 죽은 구독이 남아 발송마다 실패가 쌓인다.</summary>
    public Task RemovePushSubscriptionAsync(string endpoint, CancellationToken ct = default)
        => gateway.DeleteAsync(
            $"notification/notifications/subscriptions?endpoint={Uri.EscapeDataString(endpoint)}", ct);

    /// <summary>한 지점의 지금 날씨와 예보. 알림 서비스가 아니라 생활과환경이 답한다(life/weather/point). 위치를 잡은 직후 「여기가 맞나」를 그 자리에서 보여 줘야 하는데 좌표를 지역 이름으로 풀 쪽은 생활과환경뿐이다. 업무 모듈을 참조하는 것이 아니라 게이트웨이 주소 한 줄이다.</summary>
    public Task<PointWeatherDto?> GetPointWeatherAsync(
        double lat, double lon, CancellationToken ct = default)
        => gateway.GetOneAsync<PointWeatherDto>(
            FormattableString.Invariant($"life/weather/point?lat={lat}&lon={lon}"), ct);

    /// <summary>좌표 하나가 어느 동네인가. 위 GetPointWeatherAsync 와 달리 기상청을 거치지 않는다 — 표를 한 번 훑는 일이다. 설정 화면이 열릴 때마다 부르므로 날씨까지 받으면 기상청이 느린 날 이름이 안 뜬다.</summary>
    public Task<PointPlaceDto?> GetPointPlaceAsync(
        double lat, double lon, CancellationToken ct = default)
        => gateway.GetOneAsync<PointPlaceDto>(
            FormattableString.Invariant($"life/weather/place?lat={lat}&lon={lon}"), ct);

    public Task<NotificationUnreadDto?> GetUnreadCountAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<NotificationUnreadDto>("notification/notifications/inbox/unread-count", ct);

    public Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(
        bool unreadOnly = false,
        CancellationToken ct = default)
        => gateway.GetFlexibleListAsync<NotificationDto>(
            "notification/notifications/inbox" + (unreadOnly ? "?unreadOnly=true" : ""), ct);

    public Task MarkAllInboxReadAsync(CancellationToken ct = default)
        => gateway.PostAsync("notification/notifications/inbox/read-all", new { }, ct);

    /// <summary>알림함에서 한 건을 치운다. 읽음과 다르다 — 읽음은 봤다이고 이쪽은 안 볼 것이다. 읽음으로 찍어 두면 안 읽은 것만 풀 때 도로 나온다. 서버는 줄을 지우지 않고 deleted_at 만 찍는다 — 같은 표가 발송 기록이라 받는 사람의 정리가 푸시 현황의 건수를 깎으면 안 된다.</summary>
    public Task DeleteInboxAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync(
            $"notification/notifications/inbox/{Uri.EscapeDataString(id)}", ct);
}
