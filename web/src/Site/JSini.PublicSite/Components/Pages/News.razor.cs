using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class News
{
    [Inject] private SiteApi Api { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    private Messages T => SiteMessages.For(Locale);
    private string L => SiteMessages.Normalize(Locale);

    private List<PostListItem> _posts = [];

    protected override async Task OnInitializedAsync()
    {
        _posts = await Api.PostsAsync(L, 50);
        Api.RecordVisit($"/{L}/news", L);
    }
}
