using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Layout;

public partial class SiteFooter
{
    [Parameter, EditorRequired] public string Locale { get; set; } = SiteMessages.DefaultLocale;

    private Messages T => SiteMessages.For(Locale);
}
