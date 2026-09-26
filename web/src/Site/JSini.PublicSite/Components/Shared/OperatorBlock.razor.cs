using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Shared;

public partial class OperatorBlock
{
    [Parameter, EditorRequired] public LegalInfo Legal { get; set; } = default!;

    [Parameter] public bool Ko { get; set; } = true;
}
