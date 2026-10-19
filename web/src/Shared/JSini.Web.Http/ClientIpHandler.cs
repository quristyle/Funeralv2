using Microsoft.AspNetCore.Http;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이로 나가는 요청에 원래 클라이언트의 주소를 실어 보낸다. 게이트웨이는 로그인(auth-attempts)과 익명 쓰기(public-write)를 IP 로 갈라 세므로, 주소가 없으면 전원이 포털 컨테이너 하나로 묶여 열한 번째 사람부터 막힌다.
/// 이전 포털은 브라우저가 게이트웨이를 직접 불러 사람마다 통이 달랐다. BFF 가 되면서 그 전제가 깨졌다.
/// 회로에는 HttpContext 가 없어 정적 SSR 요청에서만 값을 얻는다. 속도 제한 경로가 전부 정적 SSR 이라 그것으로 충분하다. 못 얻으면 헤더를 붙이지 않는다. 가짜 값은 못 얻은 요청들을 한 통에 뭉친다.
/// 값은 덧붙이지 않고 첫 값을 그대로 넘긴다. 게이트웨이는 첫 값만 읽는다. 위조할 수 있어 속도 제한과 기록에만 쓰고 권한 판단에는 쓰지 않는다.
/// </summary>
public sealed class ClientIpHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    /// <summary>기록 칸과 맞춘다. 비정상적으로 긴 값은 자른다.</summary>
    private const int MaxLength = 45;

    private const string HeaderName = "X-Forwarded-For";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 부르는 쪽이 이미 정했으면 존중한다. 서버끼리 부르는 경로에서 여기가 덮어쓰면 그쪽이 조용히 틀린다.
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
            var first = forwarded.Split(',')[0].Trim();
            if (first.Length > 0) return Truncate(first);
        }

        var remote = http.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remote) ? null : Truncate(remote);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxLength ? value : value[..MaxLength];
}
