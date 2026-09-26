using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class CompanyUserList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        SchSummary.NameOf(_companies, c => c.Id, c => c.Name, _companyId, "회사를 고르세요");

    private IReadOnlyList<CompanyDto> _companies = [];
    private string? _companyId;
    private IReadOnlyList<AccountDto> _users = [];

    private IReadOnlyList<AccountDto> Shown => _users;

    private bool _adding;
    private IReadOnlyList<AccountDto> _candidates = [];
    private IReadOnlyList<object> _picked = [];

    private ConfirmDialog? _confirm;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _companies = _companies.Count == 0 ? await Api.GetCompaniesAsync() : _companies;

        if (string.IsNullOrWhiteSpace(_companyId))
        {
            _users = [];
            Say("회사를 고르고 조회하십시오.", NoticeTone.Info);
            return -1;
        }

        _users = await Api.GetCompanyUsersAsync(_companyId);
        return _users.Count;
    }, "그 회사에 속한 사용자가 없습니다.", "사용자 목록을 읽지 못했습니다");

    /// <summary>
    /// 넣을 후보를 읽는다.
    ///
    /// 후보는 <b>소속이 없는 사람</b>이다 — 서버가 회사를 가리지 않는다.
    /// 이미 다른 회사에 속한 사람은 여기 없다.
    /// </summary>
    private Task OpenAddAsync() => LoadAsync(async () =>
    {
        _picked = [];
        _candidates = await Api.GetCompanyEligibleUsersAsync();
        _adding = true;

        return -1;
    }, string.Empty, "넣을 수 있는 사람을 읽지 못했습니다");

    private async Task AddAsync()
    {
        var ids = _picked.OfType<AccountDto>().Select(a => a.Id).ToList();

        if (ids.Count == 0 || string.IsNullOrWhiteSpace(_companyId))
        {
            return;
        }

        // 한 번에 보낸다. 한 사람씩 보내면 중간에 실패했을 때 절반만 반영된다.
        if (await RunAsync(() => Api.AssignCompanyUsersAsync(_companyId, ids),
                $"{ids.Count} 명을 넣었습니다.", "넣지 못했습니다"))
        {
            _adding = false;
            await ReloadAsync();
        }
    }

    private async Task RemoveAsync(AccountDto target)
    {
        // 무엇이 함께 끊기는지 적어 준다. 「소속만 푼다」를 모르면 계정을
        // 지우는 것으로 읽고, 알고도 부서·역할까지 끊기는 것은 모른다.
        var ok = await _confirm!.AskAsync(
            $"「{target.UserName}」 의 회사 소속을 풉니다."
            + "\n계정은 그대로 남고 소속만 없어집니다 — 부서와 역할 범위가"
            + " 회사에 걸려 있으면 그것들도 함께 끊깁니다.",
            confirmText: "해제");

        if (!ok)
        {
            return;
        }

        if (await RunAsync(() => Api.RemoveCompanyUsersAsync([target.Id]),
                $"{target.UserName} 의 소속을 풀었습니다.", "소속을 풀지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
