using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Settings;

/// <summary>
/// 로그인한 <b>본인의</b> 알림 설정과 웹푸시 구독을 다룬다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 업무 모듈이 아니라 여기 있나]
/// </para>
///
/// <para>
/// 이 설정을 보여 주는 화면이 둘이다 — 포털관리의 「알림 설정」과 장례식장의
/// 「환경설정」. 둘 다 <b>내 계정의 설정</b>이라 자료도 동작도 같아야 하는데,
/// 업무 모듈끼리는 서로 참조하지 않으므로 한쪽에 두면 다른 쪽이 못 쓴다.
/// <c>NoticeClient</c> 를 여기 둔 것과 같은 까닭이다.
/// </para>
///
/// <para>
/// [주소에 <c>notification</c> 과 <c>notifications</c> 가 둘 다 있다]
/// </para>
///
/// <para>
/// 앞엣것은 <b>게이트웨이 접두사</b>(어느 서비스로 보낼지)이고 뒤엣것은
/// <b>서비스 안의 묶음</b>이다. 하나를 빠뜨리면 404 인데 화면에는
/// 「설정을 읽지 못했습니다」로만 보인다 — 실제로 네 화면이 한동안 그랬다.
/// </para>
/// </remarks>
public sealed class NotificationClient(GatewayClient gateway)
{
    /// <summary>
    /// 내 설정. <b>설정만 오는 것이 아니다</b> — 푸시를 보낼 수 있는 서버인지
    /// (<c>pushAvailable</c>), 구독에 쓸 공개 키, 등록된 기기가 함께 온다.
    /// </summary>
    public Task<NotificationSettingsDto?> GetMyPreferencesAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<NotificationSettingsDto>("notification/notifications/preferences/me", ct);

    /// <summary>
    /// 설정을 저장한다. <b>감싸지 않고 설정만</b> 보낸다 — 서버가 받는 것은
    /// 응답의 <c>preference</c> 자리에 해당하는 모양이다.
    /// </summary>
    public Task SaveMyPreferencesAsync(NotificationPreferenceDto pref, CancellationToken ct = default)
        => gateway.PutAsync("notification/notifications/preferences/me", pref, ct);

    /// <summary>
    /// <b>좌표만</b> 저장한다. 설정 전체를 보내는 위 <see cref="SaveMyPreferencesAsync"/>
    /// 와 주소는 같고 <b>싣는 것이 다르다</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 서버는 「준 것만 바꾼다」라서 실은 것 밖의 스위치는 그대로 남는다. 위치를
    /// 저장하는 길이 <b>뒤에서도 돌기 때문에</b>(포털을 열어 두면 세 시간마다
    /// 조용히 다시 잰다) 이 구분이 필요하다 — 전체를 보내면 다른 탭에서 방금
    /// 바꾼 스위치를 그 저장이 되돌린다.
    /// </para>
    /// <para>
    /// 실린 <c>weatherLocated</c> 가 서버에게 「방금 잰 것」임을 알린다.
    /// 그것이 있어야 「확인한 때」가 찍힌다.
    /// </para>
    /// </remarks>
    public Task SaveMyLocationAsync(LocationUpdateDto update, CancellationToken ct = default)
        => gateway.PutAsync("notification/notifications/preferences/me", update, ct);

    /// <summary>등록된 기기 목록. <b>배열이 아니라 <c>{ items, count }</c></b> 다.</summary>
    public Task<PushSubscriptionListDto?> GetMySubscriptionsAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<PushSubscriptionListDto>("notification/notifications/subscriptions/me", ct);

    /// <summary>
    /// 시험 발송. 내 기기로 한 통 보낸다.
    ///
    /// <para>
    /// <b>결과를 받아 온다.</b> 서버는 보낸 것이 없어도 성공 계열로 답하고(202)
    /// 까닭을 <c>message</c> 에 담는다 — 그것을 안 보고 「보냈습니다」라고 말하면
    /// 알림이 안 오는 사람은 자기 기기를 의심하게 된다.
    /// </para>
    /// </summary>
    public Task<PushSendResultDto?> SendTestPushAsync(CancellationToken ct = default)
        => gateway.PostAsync<PushSendResultDto>("notification/notifications/push/test", new { }, ct);

    /// <summary>
    /// 이 브라우저의 푸시 구독을 등록한다.
    ///
    /// <para>
    /// 구독 자체는 브라우저가 만든다(셸의 <c>wwwroot/js/pwa.js</c> 의
    /// <c>jsiniPwa.subscribe</c>). 여기서는 그 결과를 서버에 옮길 뿐이다.
    /// </para>
    ///
    /// <para>
    /// <b>주인을 보내지 않는다.</b> 서버가 로그인한 계정으로 정하고, 다른
    /// 주인을 지정하면 403 으로 막는다 — 남의 이름으로 구독을 만들 수 있으면
    /// 그 사람 알림을 가로챌 수 있다.
    /// </para>
    /// </summary>
    public Task RegisterPushSubscriptionAsync(
        PushSubscribeRequest request, CancellationToken ct = default)
        => gateway.PostAsync("notification/notifications/subscriptions", request, ct);

    /// <summary>
    /// 알림 한 건을 <b>읽음</b>으로 찍는다. 열쇠는 「발송 한 번」
    /// (<c>batch_id</c>)이라 기기가 여럿이어도 한 번에 찍힌다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>알림을 눌러 들어온 사람을 위해 여기 있다.</b> 서비스워커가 열 주소에
    /// 표시를 얹어 주고(<c>push-sw.js</c> 의 <c>withReadMark</c>), 레이아웃에
    /// 늘 실려 있는 <c>PushClickRead</c> 가 그것을 보고 이 호출을 한다 —
    /// 그래서 <b>업무 모듈이 아니라 공용 자리</b>다. 알림이 데려가는 화면은
    /// 모듈을 가리지 않는다.
    /// </para>
    /// <para>
    /// 포털관리의 「내 알림함」에도 같은 호출이 있다(<c>AdminClient</c> 의
    /// <c>MarkNotificationReadAsync</c>). 그쪽은 목록·통계와 한 벌로 묶인
    /// 화면용 클라이언트라 그대로 두었다 — 주소가 같은 한 줄이다.
    /// </para>
    /// </remarks>
    public Task MarkInboxReadAsync(string id, CancellationToken ct = default)
        => gateway.PostAsync(
            $"notification/notifications/inbox/{Uri.EscapeDataString(id)}/read", new { }, ct);

    /// <summary>
    /// 구독을 지운다. 열쇠는 <paramref name="endpoint"/> 다.
    ///
    /// <b>브라우저에서 끊기 전에 그 값을 꺼내 두어야 한다</b> — 끊고 나면
    /// 알 수 없다. 못 지우면 서버에 죽은 구독이 남아 발송마다 실패가 쌓인다.
    /// </summary>
    public Task RemovePushSubscriptionAsync(string endpoint, CancellationToken ct = default)
        => gateway.DeleteAsync(
            $"notification/notifications/subscriptions?endpoint={Uri.EscapeDataString(endpoint)}", ct);

    /// <summary>
    /// 한 지점의 지금 날씨와 예보. <b>알림 서비스가 아니라 생활과환경이 답한다</b>
    /// (<c>life/weather/point</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// [왜 날씨를 알림 클라이언트가 부르나]
    /// </para>
    /// <para>
    /// 「내 위치 날씨」 설정이 알림 설정 화면에 있기 때문이다. 위치를 잡은 직후
    /// <b>「여기가 맞나」를 그 자리에서 보여 줘야</b> 하는데, 그러려면 좌표를
    /// 지역 이름으로 풀어 줄 쪽이 필요하고 그것을 아는 것은 생활과환경뿐이다
    /// (격자표도 기상청 인증키도 거기 있다).
    /// </para>
    /// <para>
    /// <b>업무 모듈을 참조하는 것이 아니다.</b> 게이트웨이 주소 한 줄이라
    /// 생활과환경 화면(<c>JSini.Web.LifeEnv</c>)과는 아무 관계가 없다 — 이 판은
    /// 포털관리와 장례식장 두 화면이 쓰고, 둘 다 그 모듈을 참조하지 않는다.
    /// </para>
    /// </remarks>
    public Task<PointWeatherDto?> GetPointWeatherAsync(
        double lat, double lon, CancellationToken ct = default)
        => gateway.GetOneAsync<PointWeatherDto>(
            FormattableString.Invariant($"life/weather/point?lat={lat}&lon={lon}"), ct);
}
