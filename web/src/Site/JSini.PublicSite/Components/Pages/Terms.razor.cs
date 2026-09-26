using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class Terms
{
    [Inject] private SiteApi Api { get; set; } = default!;
    [Inject] private IOptions<LegalInfo> LegalOptions { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    private string L => SiteMessages.Normalize(Locale);
    private bool Ko => L == "ko";

    private LegalInfo Legal => LegalOptions.Value;

    protected override void OnInitialized() => Api.RecordVisit($"/{L}/terms", L);
}
