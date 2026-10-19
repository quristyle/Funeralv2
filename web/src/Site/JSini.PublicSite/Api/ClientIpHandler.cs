namespace JSini.PublicSite.Api;

/// <summary>
/// 게이트웨이로 나가는 요청에 방문자의 주소를 실어 보낸다. 익명 쓰기(public-write)는 IP 당 분당 3회로 조이는데, 이 사이트가 서버에서 대신 부르면 게이트웨이가 보는 것은 컨테이너 하나뿐이라 방문자 전체가 한 통을 쓰게 된다.
/// 웹이 공유 프로젝트를 참조하지 않으므로(배포 일정 분리) JSini.Web.Http.ClientIpHandler 와 같은 일을 여기 한 벌 더 둔다. 못 얻으면 헤더를 붙이지 않는다. 위조할 수 있어 속도 제한에만 쓴다.
/// </summary>
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
