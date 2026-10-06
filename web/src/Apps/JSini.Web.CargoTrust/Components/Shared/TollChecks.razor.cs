using JSini.Web.CargoTrust.Api;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class TollChecks
{
    [Parameter] public IReadOnlyList<EligibilityCheckInfo> Items { get; set; } = [];

    /// <summary>알 수 없음(<c>null</c>)은 아님과 다르다 — 가운데 색으로 둔다.</summary>
    private static string Tone(bool? ok) => ok switch
    {
        true => "ct-check ct-check--ok",
        false => "ct-check ct-check--no",
        null => "ct-check ct-check--unknown",
    };
}
