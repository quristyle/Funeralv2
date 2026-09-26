using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class Home
{
    [Inject] private SiteApi Api { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    private Messages T => SiteMessages.For(Locale);
    private string L => SiteMessages.Normalize(Locale);
    private string Link(string path) => $"/{L}{path}";

    private List<Section> _sections = [];
    private List<PostListItem> _posts = [];

    protected override async Task OnInitializedAsync()
    {
        // 둘을 나란히 부른다. 순서에 의미가 없고, 소개 사이트에서는 첫 바이트까지의
        // 시간이 곧 이탈률이다.
        var sections = Api.SectionsAsync(L, "home.");
        var posts = Api.PostsAsync(L, 3);

        await Task.WhenAll(sections, posts);

        _sections = sections.Result;
        _posts = posts.Result;

        Api.RecordVisit($"/{L}", L);
    }
}
