using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>메일 HTML 템플릿. 고치는 것은 서버가 역할을 한 번 더 본다(메뉴 권한과 같은 시스템관리자).</summary>
public sealed class MailTemplateClient(GatewayClient gateway)
{
    private const string Url = "projmng/mail-templates";

    public Task<IReadOnlyList<MailTemplateDto>> ListAsync(
        string? kind = MailTemplateDto.KindSchedule, bool usableOnly = false, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(kind)) query.Add($"kind={Uri.EscapeDataString(kind)}");
        if (usableOnly) query.Add("usableOnly=true");

        return gateway.GetListAsync<MailTemplateDto>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

    public Task<MailTemplateDto?> CreateAsync(MailTemplateDto item, CancellationToken ct = default)
        => gateway.PostAsync<MailTemplateDto>(Url, item, ct);

    public Task<MailTemplateDto?> UpdateAsync(MailTemplateDto item, CancellationToken ct = default)
        => gateway.PutAsync<MailTemplateDto>($"{Url}/{item.TplId}", item, ct);

    public Task DeleteAsync(long tplId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{tplId}", ct);
}

/// <summary>메일 템플릿 한 건 — <c>projmng.mail_template</c>.</summary>
public sealed class MailTemplateDto
{
    public const string KindSchedule = "SCHEDULE";

    public long TplId { get; set; }
    public string TplKind { get; set; } = KindSchedule;
    public string? TplNm { get; set; }
    public string? TplDesc { get; set; }

    /// <summary>제목 틀. 비우면 기본 제목을 쓴다.</summary>
    public string? Subject { get; set; }

    public string? BodyHtml { get; set; }
    public bool UseYn { get; set; } = true;
    public int SortNo { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
    public string? ModId { get; set; }
    public DateTime? ModDt { get; set; }

    public MailTemplateDto Copy() => (MailTemplateDto)MemberwiseClone();
}
