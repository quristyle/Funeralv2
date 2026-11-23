using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class CompanyUsageManagerBoard
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private const string UsageGroup = "COMPANY_USAGE_LOCATION";

    /// <summary>가운데 단의 한 줄 — 사용처 코드와 그 이름, 담당자 수.</summary>
    private sealed record UsageRow(string CodeValue, string Name, int Count);

    private string? _keyword;

    private IReadOnlyList<CompanyDto> _companies = [];
    private IReadOnlyList<CommonCodeDto> _codes = [];
    private IReadOnlyList<AccountDto> _accounts = [];

    private CompanyDto? _company;
    private string? _usage;

    /// <summary>고른 회사의 담당자 전부(사용처 모두). 가운데 단의 수도 이것으로 센다 — 사용처마다 묻지 않는다.</summary>
    private IReadOnlyList<CompanyUsageManagerDto> _managers = [];

    private IEnumerable<string> _pickedAccounts = [];

    private string ConditionSummary => SchSummary.Of(_keyword, _company?.Name);

    private IReadOnlyList<CompanyDto> Companies =>
        string.IsNullOrWhiteSpace(_keyword)
            ? _companies
            : [.. _companies.Where(c =>
                c.Name.Contains(_keyword.Trim(), StringComparison.OrdinalIgnoreCase)
                || (c.ShortName ?? string.Empty).Contains(_keyword.Trim(), StringComparison.OrdinalIgnoreCase))];

    /// <summary>회사에 걸린 사용처. 공통코드 차례를 따르고, 코드에 없는 값도 버리지 않는다(이름 대신 코드가 선다).</summary>
    private IReadOnlyList<UsageRow> Usages
    {
        get
        {
            if (_company is null) return [];

            var linked = _company.UsageLocations.ToHashSet(StringComparer.Ordinal);
            var rows = _codes
                .Where(c => linked.Contains(c.CodeValue))
                .OrderBy(c => c.SortOrder)
                .Select(c => c.CodeValue)
                .Concat(_company.UsageLocations.Where(v => _codes.All(c => c.CodeValue != v)));

            return [.. rows.Select(code => new UsageRow(
                code,
                _codes.FirstOrDefault(c => c.CodeValue == code)?.CodeName ?? code,
                _managers.Count(m => m.CodeValue == code)))];
        }
    }

    private IReadOnlyList<CompanyUsageManagerDto> Managers =>
        _usage is null ? [] : [.. _managers.Where(m => m.CodeValue == _usage)];

    /// <summary>넣을 수 있는 사람 — 이미 담당인 사람은 뺀다.</summary>
    private IReadOnlyList<AccountDto> Candidates
    {
        get
        {
            var taken = Managers.Select(m => m.AccountId).ToHashSet(StringComparer.Ordinal);
            return [.. _accounts.Where(a => !taken.Contains(a.Id)).OrderBy(a => a.UserName)];
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var companies = Api.GetCompaniesAsync();
        var codes = Api.GetCodesAsync(UsageGroup);
        var accounts = Api.GetAccountsAsync();
        await Task.WhenAll(companies, codes, accounts);

        _companies = companies.Result;
        _codes = codes.Result;
        _accounts = [.. accounts.Result.Where(a => a.Status == "ACTIVE")];

        // 고른 회사를 새 목록의 것으로 갈아 끼운다(사용처가 바뀌었을 수 있다).
        _company = _company is null ? null : _companies.FirstOrDefault(c => c.Id == _company.Id);
        if (_company is null)
        {
            _usage = null;
            _managers = [];
        }
        else
        {
            if (_usage is not null && !_company.UsageLocations.Contains(_usage)) _usage = null;
            _managers = await Api.GetUsageManagersAsync(_company.Id);
        }

        return _companies.Count;
    }, "회사가 없습니다.", "회사 목록을 읽지 못했습니다");

    private Task PickCompanyAsync(CompanyDto company)
    {
        _company = company;
        _usage = null;
        _pickedAccounts = [];
        _managers = [];

        return LoadAsync(async () =>
        {
            _managers = await Api.GetUsageManagersAsync(company.Id);

            // 사용처가 하나뿐이면 바로 연다 — 고를 것이 없다.
            if (company.UsageLocations.Count == 1) _usage = company.UsageLocations[0];

            return company.UsageLocations.Count;
        }, "걸린 사용처가 없습니다.", "담당자를 읽지 못했습니다");
    }

    private void PickUsage(string codeValue)
    {
        _usage = codeValue;
        _pickedAccounts = [];
    }

    private async Task AddAsync()
    {
        if (_company is null || _usage is null) return;

        var ids = _pickedAccounts.ToList();
        if (ids.Count == 0) return;

        if (await RunAsync(
                () => Api.AddUsageManagersAsync(_company.Id, _usage, ids),
                $"담당자 {ids.Count}명을 넣었습니다.", "넣지 못했습니다"))
        {
            _pickedAccounts = [];
            await RefreshManagersAsync();
        }
    }

    private async Task RemoveAsync(CompanyUsageManagerDto manager)
    {
        if (await RunAsync(
                () => Api.RemoveUsageManagersAsync([manager.Id]),
                $"{manager.UserName} 님을 뺐습니다.", "빼지 못했습니다"))
        {
            await RefreshManagersAsync();
        }
    }

    private async Task RefreshManagersAsync()
    {
        if (_company is null) return;

        try
        {
            _managers = await Api.GetUsageManagersAsync(_company.Id);
        }
        catch (Exception ex)
        {
            Say($"담당자를 다시 읽지 못했습니다: {ex.Message}", NoticeTone.Warning);
        }
    }

    private static string ManagerMeta(CompanyUsageManagerDto m) =>
        string.Join(" · ", new[] { m.CompanyName, m.DeptName, m.Email }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
}
