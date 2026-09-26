using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class CompanyList
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.NameOf(CargoAdminCodes.CompanyStatus.Filter, o => o.Value, o => o.Text, _status));

    private IReadOnlyList<AdminCompany> _rows = [];

    private string? _keyword;
    private string? _status;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetCompaniesAsync(_keyword, _status);
        return _rows.Count;
    }, "조건에 맞는 거래처가 없습니다.", "거래처를 읽지 못했습니다");

    private void FillNew(AdminCompany company) => company.Status = "ACTIVE";

    private async Task SaveAsync((AdminCompany Item, bool IsNew) e)
    {
        var c = e.Item;

        var body = new AdminCompanySave
        {
            BusinessNumber = c.BusinessNumber,
            CompanyName = c.CompanyName,
            CeoName = c.CeoName,
            Address = c.Address,
            Region = c.Region,
            Phone = c.Phone,
            BusinessType = c.BusinessType,
            Status = string.IsNullOrWhiteSpace(c.Status) ? "ACTIVE" : c.Status,
            AdminMemo = c.AdminMemo,
        };

        if (e.IsNew)
        {
            await Api.CreateCompanyAsync(body);
        }
        else
        {
            await Api.UpdateCompanyAsync(c.CompanyId, body);
        }
    }
}
