using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class ConfidenceNote
{
    [Parameter] public CompanyStats? Stats { get; set; }
}
