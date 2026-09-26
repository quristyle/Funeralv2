using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class RedirectTo
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>보낼 주소.</summary>
    [Parameter, EditorRequired] public string Url { get; set; } = "/";

    /// <summary>서버를 다시 밟을지. 쿠키를 굽거나 지우는 경로에서만 켠다.</summary>
    [Parameter] public bool ForceLoad { get; set; }

    protected override void OnInitialized() => Navigation.NavigateTo(Url, ForceLoad);
}
