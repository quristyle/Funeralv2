using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class RedirectToLogin
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        var returnUrl = Navigation.ToBaseRelativePath(Navigation.Uri);
        Navigation.NavigateTo(
            $"/login?returnUrl={Uri.EscapeDataString("/" + returnUrl)}",
            forceLoad: true);
    }
}
