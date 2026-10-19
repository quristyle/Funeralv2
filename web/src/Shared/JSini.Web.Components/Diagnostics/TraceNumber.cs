using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JSini.Web.Components.Diagnostics;

/// <summary>
/// 오류 화면이 사람에게 보여 주는 추적 번호를 짓는다. 화면(Error.razor)과 기록(PortalErrorReporter)이 같은 값을 써야 한다. 다르면 사용자가 부르는 번호가 표에 없다.
/// Activity.Current?.Id(W3C traceparent)가 운영에서 나오는 모양이고, Activity 가 없으면 HttpContext.TraceIdentifier 로 떨어진다. 서버 조회가 둘 다 받는다.
/// </summary>
public static class TraceNumber
{
    /// <summary>지금 요청의 추적 번호. 둘 다 없으면 <c>null</c>.</summary>
    public static string? Of(HttpContext? http) =>
        Activity.Current?.Id ?? http?.TraceIdentifier;

    /// <summary>번호에서 조회 열쇠(가운데 32자리)를 뽑는다. 서버도 같은 일을 하지만(PortalErrorEndpoints.NormalizeTrace) 보내는 쪽이 미리 쪼개 두면 서버가 모양을 잘못 읽을 자리가 없다. 번호 모양이 둘이라 한쪽만 고치면 어긋난다.</summary>
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
