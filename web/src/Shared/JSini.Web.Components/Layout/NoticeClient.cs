using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Layout;

/// <summary>팝업으로 띄울 공지를 읽는다. 공지는 포털관리가 관리하지만 보이는 것은 로그인한 뒤의 레이아웃과 로그인 화면 둘 다라서 여기 둔다 — 셸은 업무 모듈을 이름으로 알지 못해 모듈에 두면 레이아웃이 못 쓴다.
/// GetPopupAsync 는 로그인한 사용자용(공개 공지까지 온다). GetPublicPopupAsync 는 로그인 화면용으로 is_public 만 오고, 토큰을 붙이지 않는 클라이언트로 부른다 — 로그인 화면에는 토큰이 없고 만료된 토큰 하나로 401 → 갱신 → 로그인으로 튕기는 길이 열린다.</summary>
public sealed class NoticeClient
{
    private readonly GatewayClient _gateway;
    private readonly GatewayClient _anonymous;

    public NoticeClient(GatewayClient gateway, IHttpClientFactory factory)
    {
        _gateway = gateway;

        // 같은 봉투를 벗기는 일이라 GatewayClient 를 그대로 쓴다. 다른 것은
        // 토큰을 붙이는 핸들러가 걸려 있지 않은 HttpClient 라는 점뿐이다.
        _anonymous = new GatewayClient(
            factory.CreateClient(ServiceCollectionExtensions.AnonymousClientName));
    }

    /// <summary>로그인한 사용자에게 띄울 공지. 공개 공지도 함께 온다.</summary>
    public Task<IReadOnlyList<NoticeDto>> GetPopupAsync(CancellationToken ct = default) =>
        _gateway.GetListAsync<NoticeDto>("auth/notices/popup", ct);

    /// <summary>로그인하지 않아도 보이는 공지.</summary>
    public Task<IReadOnlyList<NoticeDto>> GetPublicPopupAsync(CancellationToken ct = default) =>
        _anonymous.GetListAsync<NoticeDto>("auth/notices/popup/public", ct);
}
