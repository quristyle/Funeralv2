using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 팝업으로 띄울 공지를 읽는다.
/// </summary>
/// <remarks>
/// <para>
/// 공지는 포털관리가 <b>관리</b>하지만 <b>보이는 것은 모든 화면</b>이다 —
/// 로그인한 뒤의 레이아웃과 로그인 화면 둘 다. 그래서 관리 화면의
/// <c>AdminClient</c> 가 아니라 여기 있다. 셸은 업무 모듈을 이름으로 알지
/// 못하므로 모듈에 두면 레이아웃이 못 쓴다.
/// </para>
///
/// <para>
/// <b><c>JSini.Web.Http</c> 가 아니라 여기 있는 이유</b> — 그 프로젝트는
/// 아무것도 참조하지 않는다(csproj 머리말). DTO 를 알아야 하는 클라이언트는
/// <c>MenuProvider</c> 와 같이 이쪽에 둔다.
/// </para>
///
/// <para>
/// [갈래가 둘인 이유]
/// </para>
///
/// <list type="bullet">
///   <item>
///     <see cref="GetPopupAsync"/> — 로그인한 사용자용. 공개 공지까지 함께 온다.
///   </item>
///   <item>
///     <see cref="GetPublicPopupAsync"/> — 로그인 화면용. <c>is_public</c> 인 것만.
///     <b>토큰을 붙이지 않는 클라이언트로 부른다</b> — 로그인 화면에는 붙일
///     토큰이 없고, 붙이는 클라이언트로 부르면 만료된 토큰 하나 때문에
///     401 → 갱신 시도 → 로그인 화면에서 또 로그인으로 튕기는 길이 열린다.
///   </item>
/// </list>
/// </remarks>
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
