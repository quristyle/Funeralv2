using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Layout;

public partial class SiteHeader
{
    [Parameter, EditorRequired] public string Locale { get; set; } = SiteMessages.DefaultLocale;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private Messages T => SiteMessages.For(Locale);

    private string Link(string path) => $"/{Locale}{(path == "/" ? string.Empty : path)}";

    private (string Href, string Label)[] Items =>
    [
        (Link("/about"), T.Nav.About),
        (Link("/work"), T.Nav.Work),
        (Link("/news"), T.Nav.News),
        (Link("/downloads"), T.Nav.Downloads),
        (Link("/contact"), T.Nav.Contact),
    ];

    /// <summary>같은 화면의 다른 언어 주소. 언어 조각만 갈아 끼운다.</summary>
    private string OtherLocalePath
    {
        get
        {
            var rest = Navigation.ToBaseRelativePath(Navigation.Uri)
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1);

            return $"/{SiteMessages.Other(Locale)}{string.Concat(rest.Select(s => "/" + s))}";
        }
    }
}
