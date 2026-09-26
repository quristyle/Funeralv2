using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class Privacy
{
    [Inject] private SiteApi Api { get; set; } = default!;
    [Inject] private IOptions<LegalInfo> LegalOptions { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    private string L => SiteMessages.Normalize(Locale);
    private bool Ko => L == "ko";

    private LegalInfo Legal => LegalOptions.Value;

    /// <summary>보호책임자에게 닿는 이메일. 따로 없으면 대표 이메일이다.</summary>
    private string OfficerEmail =>
        string.IsNullOrWhiteSpace(Legal.Officer.Email) ? Legal.ContactEmail : Legal.Officer.Email;

    protected override void OnInitialized() => Api.RecordVisit($"/{L}/privacy", L);
}
