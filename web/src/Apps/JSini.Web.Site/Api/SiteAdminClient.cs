using JSini.Web.Http;

namespace JSini.Web.Site.Api;

/// <summary>
/// 회사 소개 사이트의 <b>관리</b> 조회. 게이트웨이의 <c>/site/admin/*</c> 로 나간다.
///
/// [공개 사이트의 클라이언트와 다른 것이다]
///
/// <c>JSini.PublicSite</c> 에도 <c>SiteApi</c> 가 있지만 그쪽은 인증 없는 공개
/// 조회만 한다(문구·글·자료·문의 접수). 여기는 <b>포털에 로그인한 사람</b>이
/// 접수된 문의를 들여다보는 자리라 인증이 붙고 경로도 <c>/admin</c> 아래다.
///
/// 두 클라이언트를 합치지 않는다. 합치면 공개 사이트가 인증 배관을 끌고
/// 들어오게 되고, 그 앱이 포털의 배포 일정에 묶인다.
/// </summary>
public sealed class SiteAdminClient(GatewayClient gateway)
{
    /// <summary>
    /// 접수된 문의. 상태로 좁힐 수 있다.
    ///
    /// 서버가 최근 500건까지만 준다 — 페이징이 없다. 문의는 하루 몇 건이라
    /// 지금은 충분하고, 넘치면 그때 서버에 페이징을 붙인다.
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
/// 접수된 문의 한 건.
///
/// <c>ClientIp</c> 와 <c>ConsentedAt</c> 은 <b>개인정보 처리 근거</b>다.
/// 동의 시각과 접수 출처를 남겨 두어야 나중에 "동의 없이 받았다" 는 다툼에
/// 답할 수 있다. 화면에도 보여 준다.
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
