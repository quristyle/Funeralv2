using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class UserList
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.NameOf(CargoAdminCodes.UserType.Filter, o => o.Value, o => o.Text, _type));

    private IReadOnlyList<AdminUser> _rows = [];
    private IReadOnlyList<AdminCompany> _companies = [];
    private bool _companiesLoaded;

    private string? _keyword;
    private string? _type;

    private bool _editing;
    private AdminUser? _target;
    private AdminUserUpdate _form = new();

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetUsersAsync(_keyword, _type);
        return _rows.Count;
    }, "조건에 맞는 사용자가 없습니다.", "사용자를 읽지 못했습니다");

    private async Task OpenAsync(AdminUser row)
    {
        _target = row;
        _form = new AdminUserUpdate
        {
            UserType = row.UserType,
            CompanyId = row.CompanyId,
            Status = row.Status,
            AdminMemo = row.AdminMemo,
        };
        _editing = true;

        if (!_companiesLoaded)
        {
            try
            {
                _companies = await Api.GetCompaniesAsync();
                _companiesLoaded = true;
            }
            catch (ApiException ex)
            {
                // 창은 그대로 둔다 — 회사를 못 읽어도 유형 · 차단 · 메모는 고칠 수 있다.
                // 다음에 창을 열 때 다시 읽는다.
                Say($"거래처 목록을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
            }
        }
    }

    private async Task SaveAsync()
    {
        if (_target is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_form.UserType) || string.IsNullOrWhiteSpace(_form.Status))
        {
            Say("유형과 상태를 고르십시오.", NoticeTone.Warning);
            return;
        }

        var id = _target.UserId;
        var body = _form;

        if (await RunAsync(() => Api.UpdateUserAsync(id, body), "저장했습니다.", "저장하지 못했습니다"))
        {
            _editing = false;
            await ReloadAsync();
        }
    }
}
