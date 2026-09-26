using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class About
{
    [Inject] private SiteApi Api { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    private Messages T => SiteMessages.For(Locale);
    private string L => SiteMessages.Normalize(Locale);

    private List<Section> _sections = [];

    protected override async Task OnInitializedAsync()
    {
        _sections = await Api.SectionsAsync(L, "about.");
        Api.RecordVisit($"/{L}/about", L);
    }
}
