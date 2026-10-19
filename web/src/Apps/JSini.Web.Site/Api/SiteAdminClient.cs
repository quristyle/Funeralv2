using JSini.Web.Http;

namespace JSini.Web.Site.Api;

/// <summary>
/// 회사 소개 사이트 관리 조회. 게이트웨이의 /site/admin/* 로 나간다. 공개 사이트의 SiteApi 와 합치지 않는다 — 합치면 공개 사이트가 인증 배관을 끌고 들어와 포털 배포에 묶인다.
/// </summary>
public sealed class SiteAdminClient(GatewayClient gateway)
{
    /// <summary>
    /// 접수된 문의. 상태로 좁힌다. 서버가 최근 500건까지만 주고 페이징이 없다(하루 몇 건이라 충분하다).
    /// </summary>
    public Task<IReadOnlyList<InquiryDto>> GetInquiriesAsync(
        string? status = null, CancellationToken ct = default)
        => gateway.GetListAsync<InquiryDto>(
            "site/admin/inquiries" + (string.IsNullOrWhiteSpace(status)
                ? string.Empty
                : $"?status={Uri.EscapeDataString(status)}"), ct);

    /// <summary>문의 처리 상태를 바꾼다.</summary>
    public Task SetStatusAsync(Guid id, string status, CancellationToken ct = default)
        => gateway.PutAsync($"site/admin/inquiries/{id}/status", new { status }, ct);
}

/// <summary>
/// 접수된 문의 한 건. ClientIp 와 ConsentedAt 은 개인정보 처리 근거라 화면에도 보여 준다 — 동의 없이 받았다는 다툼에 답하려고.
/// </summary>
public sealed class InquiryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Locale { get; set; } = "ko";

    /// <summary>new · reading · replied · closed.</summary>
    public string Status { get; set; } = "new";

    public string? InternalNote { get; set; }
    public string? ClientIp { get; set; }
    public DateTime ConsentedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
