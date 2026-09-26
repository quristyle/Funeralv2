using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class TransactionFields
{
    [Parameter, EditorRequired] public TransactionDraft Draft { get; set; } = default!;

    /// <summary>못 찾은 거래처를 그 자리에서 등록하게 할지. 팝업 안에서는 끈다.</summary>
    [Parameter] public bool AllowCreateCompany { get; set; } = true;

    private void OnCompanyChanged(CompanyPick? pick) => Draft.Company = pick;
}
