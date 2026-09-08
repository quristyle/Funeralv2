using Microsoft.AspNetCore.Http;

namespace JSini.Web.Components.Security;

/// <summary>
/// 이 창이 어디서 접속했는지. <b>회로 하나(=창 하나)마다 하나다.</b>
///
/// [회로에는 <c>HttpContext</c> 가 없다]
///
/// 주소는 <b>요청</b>에 붙어 오는 값인데, Blazor Server 에서 화면이 사는 곳은
/// 요청이 아니라 회로다. 회로가 붙은 뒤에 <c>IHttpContextAccessor</c> 를 물으면
/// <c>null</c> 이거나(운이 좋으면) 엉뚱한 요청을 보게 된다.
///
/// 그래서 <b>프리렌더가 읽어 회로에 물려준다</b> —
/// <c>PersistentComponentState</c> 를 쓰는 <c>SidebarFooter</c> 가 그 자리다.
/// 크기 모드(<c>SizeModeScope</c>)가 이미 같은 길을 쓰고 있다.
///
/// [이 값으로 무엇을 판단해서는 안 되나]
///
/// <b>클라이언트가 보낸 헤더라 위조할 수 있다.</b> 프록시가 <c>X-Forwarded-For</c>
/// 를 덧붙이는 방식이라 앞에 아무 값이나 심어 둘 수 있다. 그래서 이 값은
/// <b>사람에게 보여 주는 용도로만</b> 쓴다 — 권한 판단에 쓰지 않는다.
/// AuthServer 의 <c>ResolveClientIp</c> 가 접속 기록을 남길 때 같은 이유로
/// 같은 주의를 적어 두었고, 여기 방식도 그쪽과 맞췄다.
/// </summary>
public sealed class ClientAddress
{
    /// <summary>기록·표시용 칸이므로 비정상적으로 긴 값은 자른다.</summary>
    private const int MaxLength = 45;

    /// <summary>
    /// 보여 줄 주소. 알아내지 못했으면 <c>null</c>.
    /// </summary>
    public string? Current { get; private set; }

    /// <summary>
    /// 한 번만 받는다. <b>두 번째부터 무시한다</b> — 프리렌더가 넣은 값을
    /// 회로가 다시 넣으려 할 때 <c>null</c> 로 덮어쓰지 않게 한다.
    /// </summary>
    public void Seed(string? address)
    {
        if (Current is null && !string.IsNullOrWhiteSpace(address))
        {
            Current = address;
        }
    }

    /// <summary>
    /// 지금 요청을 보낸 클라이언트의 주소.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>X-Forwarded-For</c> 의 <b>첫 값</b>이 원래 클라이언트다(뒤로 갈수록
    /// 중간 프록시다). 포털은 nginx 뒤에 있으므로 <c>RemoteIpAddress</c> 를
    /// 그대로 쓰면 <b>모든 사용자가 같은 주소로 보인다</b> — 프록시 주소다.
    /// </para>
    ///
    /// <para>
    /// 그 헤더가 없으면(개발 장비처럼 직접 부르는 경우) 연결 주소를 쓴다.
    /// 그때는 <c>::1</c> 이나 <c>127.0.0.1</c> 이 보이는데, 그것이 사실이므로
    /// 감추지 않는다.
    /// </para>
    ///
    /// <para>
    /// <b>이 값은 위조할 수 있다.</b> 클래스 머리말 참고.
    /// </para>
    /// </remarks>
    public static string? From(HttpContext? http)
    {
        if (http is null)
        {
            return null;
        }

        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();

            if (first.Length > 0)
            {
                return Truncate(first);
            }
        }

        return Truncate(http.Connection.RemoteIpAddress?.ToString());
    }

    private static string? Truncate(string? value) =>
        value is null || value.Length <= MaxLength ? value : value[..MaxLength];
}
