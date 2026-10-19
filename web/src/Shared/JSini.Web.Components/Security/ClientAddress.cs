using Microsoft.AspNetCore.Http;

namespace JSini.Web.Components.Security;

/// <summary>
/// 이 창이 어디서 접속했는지. 회로(창) 하나마다 하나다.
/// 회로에는 HttpContext 가 없어 요청 값을 직접 못 읽으므로, 프리렌더가 읽어 PersistentComponentState 로 회로에 넘겨 준다(SidebarFooter).
/// X-Forwarded-For 는 클라이언트가 위조할 수 있어 사람에게 보여 주는 용도로만 쓴다. 권한 판단에 쓰지 않는다.
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

/// <summary>지금 요청을 보낸 클라이언트의 주소. X-Forwarded-For 의 첫 값이 원래 클라이언트다. 헤더가 없으면(개발 장비) 연결 주소를 쓴다. 위조할 수 있다.</summary>
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
