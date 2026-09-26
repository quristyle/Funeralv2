using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class NewsDetail
{
    [Inject] private SiteApi Api { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    /// <summary>자료의 주소 조각. DB 의 <c>slug</c> 다.</summary>
    [Parameter] public string Slug { get; set; } = string.Empty;

    private Messages T => SiteMessages.For(Locale);
    private string L => SiteMessages.Normalize(Locale);

    private PostDetail? _post;

    protected override async Task OnInitializedAsync()
    {
        _post = await Api.PostAsync(L, Slug);
        Api.RecordVisit($"/{L}/news/{Slug}", L);
    }
}
