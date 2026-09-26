using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class PayBadge
{
    [Parameter] public string? Code { get; set; }

    [Parameter] public bool Bucket { get; set; }
}
