using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

/// <summary>
/// 요청 한 줄에 적는 글자 — 상태 · 경과.
/// </summary>
/// <remarks>
/// <para>
/// 목록을 그리는 부품이 둘이다(<c>RequestGrid</c> 데스크톱 ·
/// <c>RequestCards</c> 휴대폰). 둘이 같은 자료를 다른 모양으로 세울 뿐이라
/// <b>적는 말은 하나여야 한다</b> — 한쪽에만 「종료」가 있고 다른 쪽은
/// 「UserCompleted」로 적히면 같은 건이 기기에 따라 다른 상태로 읽힌다.
/// </para>
/// <para>
/// 화면(<c>RequestManage</c>)도 접힌 조회줄의 상태 요약에 이것을 쓴다.
/// </para>
/// </remarks>
public static class RequestRowText
{
    /// <summary>
    /// 상태 글자. <b>서버가 적어 준 이름이 있으면 그것이 먼저다</b> —
    /// 상태는 DB 에서 늘어날 수 있고, 아래 표는 그때 비는 자리다.
    /// </summary>
    public static string Status(ImprovementRequest r) =>
        !string.IsNullOrWhiteSpace(r.StatusName) ? r.StatusName! : Status(r.Status);

    /// <summary>상태 코드를 우리말로. 모르는 코드는 코드 그대로 둔다.</summary>
    public static string Status(string? status) => status switch
    {
        "Pending" => "대기",
        "InProgress" => "진행",
        "Rejected" => "반려",
        "Completed" => "완료",
        "UserCompleted" => "종료",
        "Consultation" => "협의",
        "Negotiation" => "논의",
        _ => status ?? "-",
    };

    /// <summary>상태 딱지에 입히는 색. 공통 <c>jsini-badge--*</c> 에 기댄다.</summary>
    public static string StatusClass(string? status) => status switch
    {
        "Completed" or "UserCompleted" => "jsini-badge--on",
        "InProgress" or "Consultation" or "Negotiation" => "jsini-badge--warn",
        "Rejected" => "jsini-badge--off",
        _ => "",
    };

    /// <summary>
    /// 접수부터 완료(아직이면 지금)까지 얼마나 걸렸나.
    /// </summary>
    /// <remarks>
    /// <b>UTC 로 잰다.</b> 서버가 주는 시각은 UTC 인데 <c>DateTime.Now</c> 는
    /// 우리 시계라, 섞어 빼면 아직 안 끝난 건의 경과가 통째로 아홉 시간 부푼다.
    /// </remarks>
    public static string Elapsed(ImprovementRequest r)
    {
        if (r.CreatedAt is not { } from)
        {
            return "-";
        }

        var to = r.CompletededAt ?? r.CompletedAt ?? DateTime.UtcNow;
        var span = to - from;

        return span < TimeSpan.Zero ? "-"
            : span.TotalDays >= 1 ? $"{(int)span.TotalDays}일"
            : $"{(int)span.TotalHours}시간";
    }
}
