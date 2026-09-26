using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class CompanyCard
{
    [Parameter, EditorRequired] public CompanySummary Company { get; set; } = default!;

    private CompanyStats S => Company.Stats ?? new CompanyStats();
}
