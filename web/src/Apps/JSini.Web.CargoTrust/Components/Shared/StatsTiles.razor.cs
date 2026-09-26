using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class StatsTiles
{
    [Parameter, EditorRequired] public CompanyStats Stats { get; set; } = default!;

    private string Basis => $"{Stats.PeriodLabel ?? "전체"} · 운송일 기준 · 통계 제외·삭제 거래 뺌";
}
