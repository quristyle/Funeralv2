namespace JSini.PublicSite.Api;

/// <summary>
/// 게이트웨이로 나가는 요청에 <b>원래 방문자의 주소</b>를 실어 보낸다.
/// </summary>
/// <remarks>
/// <para>
/// [없으면 문의 폼이 사이트 전체 공용 통이 된다]
/// </para>
///
/// <para>
/// 게이트웨이는 익명 쓰기(<c>public-write</c>)를 <b>IP 당 분당 3회</b>로 조이고,
/// 그 IP 를 <c>X-Forwarded-For</c> 에서 읽는다. Vue 시절에는 브라우저가
/// 게이트웨이를 직접 불렀고 nginx 가 그 헤더를 붙여 주었다.
/// </para>
///
/// <para>
/// <b>이 사이트가 서버에서 대신 부르면서 그 전제가 깨졌다</b> — 게이트웨이가
/// 보는 것은 언제나 이 컨테이너 하나다. 그러면 분당 3회가 방문자 전체의 합이
/// 되어, 한 사람이 문의를 세 번 보내면 그 분에는 아무도 못 보낸다. 게다가
/// 실패 이유를 자세히 알려 주지 않는 화면이라(<c>SiteApi.SubmitInquiryAsync</c>)
/// 막힌 사람은 무엇이 잘못됐는지 알 길이 없다.
/// </para>
///
/// <para>
/// [왜 포털 것을 가져다 쓰지 않나]
/// </para>
///
/// <para>
/// 이 앱은 <c>web/</c> 의 공유 프로젝트를 하나도 참조하지 않는다 — 참조가
/// 생기는 순간 공개 사이트가 업무 포털의 배포 일정에 묶인다(csproj 머리말).
/// 그래서 <c>JSini.Web.Http.ClientIpHandler</c> 와 같은 일을 여기 한 벌 더 둔다.
/// 저장소 규칙대로 <b>두 곳이 쓰면 복제, 세 번째부터 승격</b>이다.
/// </para>
///
/// <para>
/// 못 얻으면 헤더를 붙이지 않는다. 가짜 값을 넣으면 그것이 통 열쇠가 되어
/// 값을 못 얻은 요청들이 한 통에 뭉친다. 헤더가 없으면 게이트웨이는 연결
/// 주소로 떨어지는데 그쪽이 더 사실에 가깝다.
/// </para>
///
/// <para>
/// <b>이 값은 위조할 수 있다.</b> 속도 제한과 기록에만 쓰고 권한 판단에 쓰지 않는다.
/// </para>
/// </remarks>
public sealed class ClientIpHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    private const int MaxLength = 45;
    private const string HeaderName = "X-Forwarded-For";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(HeaderName) && Resolve() is { } ip)
        {
            request.Headers.TryAddWithoutValidation(HeaderName, ip);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private string? Resolve()
    {
        var http = accessor.HttpContext;
        if (http is null) return null;

        var forwarded = http.Request.Headers[HeaderName].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            // 첫 값이 원래 클라이언트다. 뒤로 갈수록 중간 프록시다.
            var first = forwarded.Split(',')[0].Trim();
            if (first.Length > 0) return Truncate(first);
        }

        var remote = http.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remote) ? null : Truncate(remote);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxLength ? value : value[..MaxLength];
}
