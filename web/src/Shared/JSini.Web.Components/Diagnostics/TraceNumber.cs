using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JSini.Web.Components.Diagnostics;

/// <summary>
/// 오류 화면이 사람에게 보여 주는 <b>추적 번호</b>를 짓는다.
/// </summary>
/// <remarks>
/// <para>
/// 번호를 읽는 곳이 둘이다 — 화면(<c>Error.razor</c>)과 기록을 보내는 쪽
/// (<see cref="PortalErrorReporter"/>). <b>둘이 다른 값을 쓰면 이 기능 전체가
/// 조용히 쓸모없어진다</b>: 사용자는 번호를 불러 주는데 표에는 그 번호가 없다.
/// 그래서 한 곳에 둔다.
/// </para>
///
/// <para>
/// [두 모양이 있다]
/// </para>
///
/// <para>
/// <c>Activity.Current?.Id</c> 는 W3C traceparent(<c>00-{32}-{16}-00</c>)다.
/// 운영에서 실제로 나오는 것이 이것이다. 다만 요청마다 Activity 가 서는 것은
/// 듣는 쪽이 있을 때뿐이라, 없으면 <c>HttpContext.TraceIdentifier</c>
/// (<c>0HN7…:00000003</c>)로 떨어진다. 서버 쪽 조회가 둘 다 받아 준다.
/// </para>
/// </remarks>
public static class TraceNumber
{
    /// <summary>지금 요청의 추적 번호. 둘 다 없으면 <c>null</c>.</summary>
    public static string? Of(HttpContext? http) =>
        Activity.Current?.Id ?? http?.TraceIdentifier;

    /// <summary>
    /// 번호에서 조회 열쇠(가운데 32자리)를 뽑는다.
    ///
    /// <para>
    /// 서버도 같은 일을 한다(<c>PortalErrorEndpoints.NormalizeTrace</c>).
    /// 여기서도 하는 이유는 <b>보내는 쪽이 이미 쪼개 두면 서버가 모양을 잘못
    /// 읽을 자리가 없기</b> 때문이다 — 번호 모양이 둘이라 한쪽만 고치면
    /// 어긋난다.
    /// </para>
    /// </summary>
    public static string? KeyOf(string? traceNumber)
    {
        var text = traceNumber?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        if (text.StartsWith("00-", StringComparison.Ordinal))
        {
            var parts = text.Split('-');
            if (parts.Length >= 2 && parts[1].Length > 0) return parts[1].ToLowerInvariant();
        }

        return text.ToLowerInvariant();
    }
}
