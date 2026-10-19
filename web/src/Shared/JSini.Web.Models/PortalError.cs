namespace JSini.Web.Models;

/// <summary>오류 한 건. AuthServer 의 PortalErrorDto 와 짝이다. 보내는 쪽과 보는 쪽이 다른 프로젝트라 공용에 둔다. 보내기와 보기가 같은 타입인 것은 일부러다(나눠 두면 한쪽에만 칸이 늘어난다). 목록 조회는 Detail 이 비어서 온다.</summary>
public sealed class PortalErrorDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>추적 번호의 가운데 32자리. 조회 열쇠다.</summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>오류 화면이 사람에게 보여 준 번호 그대로 (<c>00-…-00</c>).</summary>
    public string? Traceparent { get; set; }

    /// <summary>난 때(UTC). 화면이 <c>ToLocalTime()</c> 으로 그린다.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>어느 프론트인가 (<c>portal</c> · <c>site</c>).</summary>
    public string? Source { get; set; }

    public string? Path { get; set; }
    public string? QueryString { get; set; }
    public string? Method { get; set; }

    /// <summary>로그인한 사람의 아이디. 로그인 전에 난 오류는 비어 있다.</summary>
    public string? UserId { get; set; }

    public string? Ip { get; set; }

    /// <summary>브라우저·기기. 「모바일에서만 난다」를 확인하는 칸이다.</summary>
    public string? UserAgent { get; set; }

    public string? ExceptionType { get; set; }
    public string? Message { get; set; }

    /// <summary>스택 추적. <b>목록 조회에서는 비어 있다.</b></summary>
    public string? Detail { get; set; }

    /// <summary>예외 타입에서 네임스페이스를 뗀 짧은 이름. 표의 칸이 좁다.</summary>
    public string ShortExceptionType =>
        string.IsNullOrEmpty(ExceptionType)
            ? string.Empty
            : ExceptionType[(ExceptionType.LastIndexOf('.') + 1)..];
}
