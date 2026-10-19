using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

/// <summary>요청 한 줄에 적는 글자 — 상태·경과. 목록 부품 둘(RequestGrid · RequestCards)이 같은 자료를 다른 모양으로 세울 뿐이라 적는 말은 하나여야 한다.
/// 한쪽만 「종료」로 적고 다른 쪽은 UserCompleted 로 적으면 같은 건이 기기에 따라 다르게 읽힌다. 화면 RequestManage 의 접힌 조회줄도 이것을 쓴다.</summary>
public static class RequestRowText
{
    /// <summary>상태 글자. 서버가 적어 준 이름이 있으면 그것이 먼저다 — 상태는 DB 에서 늘어날 수 있고 아래 표는 그때 비는 자리다.</summary>
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

    /// <summary>접수부터 완료(아직이면 지금)까지 걸린 시간. UTC 로 잰다 — 서버 시각은 UTC 인데 DateTime.Now 와 섞으면 경과가 아홉 시간 부푼다.</summary>
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
