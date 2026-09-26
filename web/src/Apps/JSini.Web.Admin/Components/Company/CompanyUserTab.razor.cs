using JSini.Web.Admin.Api;
using JSini.Web.Components.Layout;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Company;

/// <summary>고른 회사의 사용자 관리 탭.</summary>
public partial class CompanyUserTab
{
    [Inject] private AdminClient Api { get; set; } = default!;

    [Parameter, EditorRequired] public string CompanyId { get; set; } = string.Empty;

    [Parameter] public string? CompanyName { get; set; }

    /// <summary>사람이 늘거나 줄었다. 화면이 회사 목록의 사람 수를 다시 읽는다.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    private IReadOnlyList<AccountDto> _users = [];
    private string? _loadedFor;

    private bool _adding;
    private IReadOnlyList<AccountDto> _candidates = [];
    private IReadOnlyList<AccountDto> _picked = [];

    private ConfirmDialog? _confirm;

    private RenderFragment<AccountDto> Unlink => user => builder =>
    {
        builder.OpenComponent<UserRowActions>(0);
        builder.AddAttribute(1, nameof(UserRowActions.User), user);
        builder.AddAttribute(2, nameof(UserRowActions.OnRemove), EventCallback.Factory.Create<AccountDto>(this, RemoveAsync));
        builder.CloseComponent();
    };

    protected override Task OnParametersSetAsync()
    {
        if (_loadedFor == CompanyId)
        {
            return Task.CompletedTask;
        }

        _loadedFor = CompanyId;
        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _users = await Api.GetCompanyUsersAsync(CompanyId);
        return _users.Count;
    }, "이 회사에 속한 사용자가 없습니다.", "사용자 목록을 읽지 못했습니다");

    private Task OpenAddAsync() => LoadAsync(async () =>
    {
        _picked = [];
        _candidates = await Api.GetCompanyEligibleUsersAsync();
        _adding = true;

        return -1;
    }, string.Empty, "넣을 수 있는 사람을 읽지 못했습니다");

    private void OnPicked(IReadOnlyList<AccountDto> picked) => _picked = picked;

    /// <summary>한 번에 보낸다. 한 사람씩 보내면 중간에 실패했을 때 절반만 반영된다.</summary>
    private async Task AddAsync()
    {
        var ids = _picked.Select(a => a.Id).ToList();

        if (ids.Count == 0)
        {
            return;
        }

        if (await RunAsync(() => Api.AssignCompanyUsersAsync(CompanyId, ids),
                $"{ids.Count} 명을 넣었습니다.", "넣지 못했습니다"))
        {
            _adding = false;
            await ReloadAsync();
            await OnChanged.InvokeAsync();
        }
    }

    private async Task RemoveAsync(AccountDto target)
    {
        var ok = await _confirm!.AskAsync(
            $"「{target.UserName}」 의 회사 소속을 풉니다."
            + "\n계정은 그대로 남습니다. 부서와 역할 범위가 회사에 걸려 있으면 함께 끊깁니다.",
            confirmText: "해제");

        if (!ok)
        {
            return;
        }

        if (await RunAsync(() => Api.RemoveCompanyUsersAsync([target.Id]),
                $"{target.UserName} 의 소속을 풀었습니다.", "소속을 풀지 못했습니다"))
        {
            await ReloadAsync();
            await OnChanged.InvokeAsync();
        }
    }
}
