namespace AuthServer.DTOs;

/// <summary>포털이 보내 오는 오류 한 건. 받는 쪽에서 길이를 자른다 — 그대로 넣으면 열을 넘겨 저장이 실패하고 오류 기록 자체가 사라진다.</summary>
public class PortalErrorReportDto
{
    /// <summary>화면에 보여 준 번호 그대로 (<c>00-…-00</c>).</summary>
    public string? Traceparent { get; set; }

    /// <summary>번호의 가운데 32자리. 비어 있으면 받는 쪽이 <see cref="Traceparent"/> 에서 뽑는다.</summary>
    public string? TraceId { get; set; }

    /// <summary>오류가 난 때(UTC). 보고가 늦을 수 있어 보내는 쪽이 적는다.</summary>
    public DateTime? OccurredAt { get; set; }

    /// <summary>어느 프론트인가 (<c>portal</c> · <c>site</c>).</summary>
    public string? Source { get; set; }

    public string? Path { get; set; }
    public string? QueryString { get; set; }
    public string? Method { get; set; }

    public string? UserId { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }

    public string? ExceptionType { get; set; }
    public string? Message { get; set; }

    /// <summary>스택 추적. 안쪽 예외까지 펼친 글이다.</summary>
    public string? Detail { get; set; }
}

/// <summary>조회 화면이 받는 오류 한 건. 저장한 것을 거의 그대로 돌려준다 — 엔티티를 그대로 내보내지 않는다.</summary>
public class PortalErrorDto
{
    public string Id { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string? Traceparent { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Source { get; set; }
    public string? Path { get; set; }
    public string? QueryString { get; set; }
    public string? Method { get; set; }
    public string? UserId { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? ExceptionType { get; set; }
    public string? Message { get; set; }
    public string? Detail { get; set; }
}
