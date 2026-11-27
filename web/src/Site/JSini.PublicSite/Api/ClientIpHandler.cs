namespace JSini.PublicSite.Api;

/// <summary>
/// 게이트웨이로 나가는 요청에 방문자의 주소를 실어 보낸다. 익명 쓰기(public-write)는 IP 당 분당 3회로 조이는데, 이 사이트가 서버에서 대신 부르면 게이트웨이가 보는 것은 컨테이너 하나뿐이라 방문자 전체가 한 통을 쓰게 된다.
/// 웹이 공유 프로젝트를 참조하지 않으므로(배포 일정 분리) JSini.Web.Http.ClientIpHandler 와 같은 일을 여기 한 벌 더 둔다. 못 얻으면 헤더를 붙이지 않는다. 위조할 수 있어 속도 제한에만 쓴다.
/// </summary>
public sealed class ClientIpHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    private const int MaxLength = 45;
    private const string HeaderName = "X-Forwarded-For";
    private const string VisitorUaHeader = "X-Visitor-User-Agent";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(HeaderName) && Resolve() is { } ip)
        {
            request.Headers.TryAddWithoutValidation(HeaderName, ip);
        }

        // 문의에 남는 User-Agent 가 이 서버의 것이 되지 않게 방문자 것을 따로 싣는다(스팸 추적용, 길이는 받는 쪽이 자른다).
        if (accessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
            && !request.Headers.Contains(VisitorUaHeader))
        {
            request.Headers.TryAddWithoutValidation(VisitorUaHeader, ua);
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
            // nginx 가 실제 접속 주소를 맨 뒤에 덧붙인다. 앞쪽 값은 방문자가 지어낼 수 있다.
            var last = forwarded.Split(',')[^1].Trim();
            if (last.Length > 0) return Truncate(last);
        }

        var remote = http.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remote) ? null : Truncate(remote);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxLength ? value : value[..MaxLength];
}
