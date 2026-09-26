using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Layout;

public partial class SiteHeader
{
    [Parameter, EditorRequired] public string Locale { get; set; } = SiteMessages.DefaultLocale;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private Messages T => SiteMessages.For(Locale);

    /// <summary>
    /// 업무 포털 주소. **다른 오리진이다** — 공개 사이트와 업무 시스템의 쿠키·토큰
    /// 표면을 섞지 않으려고 도메인을 나눴다(바닥글의 관리 포털 링크와 같은 까닭).
    /// 그래서 사이트 안 경로(<see cref="Link"/>)로 만들지 않는다.
    /// </summary>
    private const string PortalUrl = "https://portal.jsini.co.kr";

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
