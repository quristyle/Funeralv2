using Microsoft.AspNetCore.Http;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이로 나가는 요청에 <b>원래 클라이언트의 주소</b>를 실어 보낸다.
/// </summary>
/// <remarks>
/// <para>
/// [없으면 속도 제한이 전원 공용 통이 된다 — 실제로 밟았다]
/// </para>
///
/// <para>
/// 게이트웨이는 로그인(<c>auth-attempts</c> · IP 당 분당 10회)과 익명 쓰기
/// (<c>public-write</c> · 분당 3회)를 <b>IP 로 갈라</b> 센다. 그 IP 를
/// <c>X-Forwarded-For</c> 에서 읽는다.
/// </para>
///
/// <para>
/// Vue 시절에는 브라우저가 게이트웨이를 직접 불렀고 nginx 가 그 헤더를 붙여
/// 주었으므로 사람마다 통이 달랐다. <b>프론트가 BFF 가 되면서 그 전제가
/// 깨졌다</b> — 게이트웨이가 보는 것은 언제나 포털 컨테이너 하나다. 그러면
/// 분당 10회가 <b>전체 사용자의 합</b>이 되어, 열한 번째 사람이 비밀번호를
/// 틀리는 순간 나머지 전원이 429 로 막힌다. 잠기는 방향이라 특히 나쁘다.
/// </para>
///
/// <para>
/// [회로에는 <c>HttpContext</c> 가 없다]
/// </para>
///
/// <para>
/// Blazor Server 에서 화면이 사는 곳은 요청이 아니라 회로라, 회로가 붙은 뒤에는
/// <c>IHttpContextAccessor</c> 가 <c>null</c> 이다. 그래서 이 핸들러는
/// <b>정적 SSR 요청에서만</b> 값을 얻는다. 그것으로 충분하다 — 게이트웨이가
/// IP 로 세는 경로(로그인 · 가입 신청 · 비밀번호 찾기 · 문의)가 전부 정적
/// SSR 이기 때문이다. 회로에서 나가는 업무 API 호출에는 속도 제한이 없다.
/// </para>
///
/// <para>
/// 못 얻으면 <b>헤더를 붙이지 않는다.</b> 빈 값이나 가짜 값을 넣으면 게이트웨이가
/// 그것을 통 열쇠로 삼아, 값을 못 얻은 요청들이 한 통에 뭉친다. 헤더가 없으면
/// 게이트웨이는 연결 주소로 떨어지는데 그쪽이 더 사실에 가깝다.
/// </para>
///
/// <para>
/// <b>덧붙이지 않고 그대로 넘긴다.</b> 표준 <c>X-Forwarded-For</c> 는 프록시를
/// 지날 때마다 쉼표로 잇는 값이지만, 게이트웨이는 <b>첫 값</b>만 읽는다. 우리가
/// 읽은 것도 이미 그 첫 값(nginx 가 넣은 원래 클라이언트)이라 그대로 보내는 것이
/// 가장 단순하고, 중간에 우리 컨테이너 주소가 끼어들 자리가 없다.
/// </para>
///
/// <para>
/// <b>이 값은 위조할 수 있다.</b> 클라이언트가 보낸 헤더를 nginx 가 덧붙이는
/// 방식이라 앞에 아무 값이나 심을 수 있다. 그래서 속도 제한과 기록에만 쓰고
/// 권한 판단에 쓰지 않는다 — <c>ClientAddress</c> 와 AuthServer 의
/// <c>ResolveClientIp</c> 에 같은 주의가 적혀 있다.
/// </para>
/// </remarks>
public sealed class ClientIpHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    /// <summary>기록 칸과 맞춘다. 비정상적으로 긴 값은 자른다.</summary>
    private const int MaxLength = 45;

    private const string HeaderName = "X-Forwarded-For";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 부르는 쪽이 이미 정했으면 존중한다. 서버끼리 부르는 경로가 생겼을 때
        // 여기서 덮어쓰면 그쪽이 조용히 틀린다.
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
