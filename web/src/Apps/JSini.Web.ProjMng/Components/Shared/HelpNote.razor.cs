using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class HelpNote
{
    /// <summary>접힌 줄에 적을 글자.</summary>
    [Parameter] public string Title { get; set; } = "조작법";

    /// <summary>펴면 나오는 본문.</summary>
    [Parameter] public string? Text { get; set; }
}
