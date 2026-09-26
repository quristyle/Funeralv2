using DevExpress.Blazor;

using JSini.Web.Abstractions;
using JSini.Web.Admin.Api;
using JSini.Web.Admin.Components.Company;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

/// <summary>
/// [회사 관리] — 목록과 고른 회사의 탭을 잇는다.
/// </summary>
/// <remarks>
/// <para>
/// 고치는 값(<see cref="_edit"/>)은 탭이 아니라 여기에 둔다. 탭은 고른 것만
/// 그리므로 부서 탭으로 옮겨 가면 수정 탭이 사라지는데, 값까지 거기 두면
/// 고치던 것이 함께 없어진다.
/// </para>
/// <para>
/// 저장하지 않은 채 다른 회사를 누르면 묻고, 「취소」면 고르던 줄로 되돌린다.
/// </para>
/// </remarks>
public partial class CompanyList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private CommGrd<CompanyDto>? _grid;
    private ConfirmDialog? _confirm;

    private IReadOnlyList<CompanyDto> _all = [];
    private IReadOnlyList<CommonCodeDto> _usages = [];

    private string? _keyword;
    private string? _usageFilter;

    /// <summary>표에서 고른 줄. 새 회사를 적는 동안은 비어 있다.</summary>
    private CompanyDto? _selected;

    /// <summary>수정 탭이 고치는 복사본. 표의 줄을 물리면 치는 대로 목록 글자가 바뀐다.</summary>
    private CompanyDto? _edit;

    /// <summary>판을 열 때 서버에 있던 값.</summary>
    private CompanyDto? _original;

    private bool _isNew;
    private bool _saving;
    private int _tab;

    private bool _canUpdate;
    private bool _canCreate;

    private bool CanSave => _isNew ? _canCreate : _canUpdate;

    private bool IsDirty =>
        _edit is not null && _original is not null && !CompanyForm.Same(_edit, _original);

    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.NameOf(_usages, c => c.CodeValue, c => c.CodeName, _usageFilter));

    private IReadOnlyList<CompanyDto> Shown
    {
        get
        {
            var rows = _all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_keyword))
            {
                rows = rows.Where(c =>
                    c.Name.Contains(_keyword, StringComparison.OrdinalIgnoreCase)
                    || (c.ShortName?.Contains(_keyword, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (!string.IsNullOrEmpty(_usageFilter))
            {
                rows = rows.Where(c => c.UsageLocations.Contains(_usageFilter, StringComparer.Ordinal));
            }

            return [.. rows];
        }
    }

    protected override Task OnInitializedAsync()
    {
        _canUpdate = Can(MenuAction.Update);
        _canCreate = Can(MenuAction.Create);

        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var companies = Api.GetCompaniesAsync();
        var usages = Api.GetCodesAsync("COMPANY_USAGE_LOCATION");

        await Task.WhenAll(companies, usages);

        _all = companies.Result;
        _usages = usages.Result;

        Repick();

        return _all.Count;
    }, "등록된 회사가 없습니다.", "회사 목록을 읽지 못했습니다");

    /// <summary>
    /// 다시 읽은 목록에서 보던 회사를 다시 잡는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 표의 자동 선택(<c>AutoSelectFirstRow</c>)은 끄고 여기서 첫 회사를 연다.
    /// <see cref="Shown"/> 이 렌더마다 새 목록이라 표가 매번 「자료가 바뀌었다」로
    /// 읽고, 새 회사를 적는 동안 첫 줄을 잡아 판을 덮어쓴다.
    /// </para>
    /// <para>
    /// 고치던 것이 있으면 기준(<see cref="_original"/>)만 새 값으로 갈고 친 값은
    /// 둔다. 순서 칸은 손대지 않았으면 끌어 옮긴 새 숫자를 따라간다 — 옛 숫자가
    /// 남으면 저장할 때 끌어 놓은 자리를 덮는다.
    /// </para>
    /// </remarks>
    private void Repick()
    {
        if (_isNew)
        {
            return;
        }

        if (_edit is null)
        {
            if (Shown.FirstOrDefault() is { } first)
            {
                _selected = first;
                Open(first);
            }

            return;
        }

        var fresh = _all.FirstOrDefault(c => c.Id == _edit.Id);

        if (fresh is null)
        {
            ClosePanel();
            return;
        }

        _selected = fresh;

        if (!IsDirty)
        {
            Open(fresh, keepTab: true);
            return;
        }

        if (_original is not null && _edit.SortOrder == _original.SortOrder)
        {
            _edit.SortOrder = fresh.SortOrder;
        }

        _original = CompanyForm.Copy(fresh);
    }

    private string UsageName(string code) =>
        _usages.FirstOrDefault(u => string.Equals(u.CodeValue, code, StringComparison.Ordinal))?.CodeName ?? code;

    // ── 고르기 ──────────────────────────────────────────────

    private async Task SelectAsync(CompanyDto? row)
    {
        if (row is not null && !_isNew && _edit?.Id == row.Id)
        {
            _selected = row;
            return;
        }

        if (!await ConfirmLeaveAsync())
        {
            return;
        }

        _selected = row;

        if (row is null)
        {
            ClosePanel();
            return;
        }

        Open(row, keepTab: true);
    }

    /// <summary>＋ 를 눌렀다. 표가 부르는 자리가 동기라 묻는 일은 뒤로 넘긴다.</summary>
    private void OnEditOpen(CompanyDto company, bool isNew)
    {
        if (!isNew)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            if (!await ConfirmLeaveAsync())
            {
                return;
            }

            _selected = null;
            _isNew = true;
            _tab = 0;
            _edit = company;
            _original = CompanyForm.Copy(company);

            StateHasChanged();
        });
    }

    private void Open(CompanyDto row, bool keepTab = false)
    {
        _isNew = false;
        _edit = CompanyForm.Copy(row);
        _original = CompanyForm.Copy(row);

        if (!keepTab)
        {
            _tab = 0;
        }
    }

    private void ClosePanel()
    {
        _isNew = false;
        _edit = null;
        _original = null;
        _tab = 0;
    }

    private async Task<bool> ConfirmLeaveAsync()
    {
        if (!IsDirty || _confirm is null)
        {
            return true;
        }

        var who = _isNew ? "새 회사" : $"「{_original?.Name}」";

        var ok = await _confirm.AskAsync(
            $"{who} 에 저장하지 않은 변경이 있습니다.\n옮기면 고친 것이 사라집니다.",
            "저장하지 않은 변경",
            "버리고 옮기기",
            ButtonRenderStyle.Warning);

        if (!ok)
        {
            await RestoreHighlightAsync();
        }

        return ok;
    }

    /// <summary>
    /// 표가 칠해 둔 새 줄을 고르던 줄로 되돌린다. 같은 값을 다시 넘기는 것으로는
    /// DxGrid 가 바뀐 것으로 보지 않아 강조가 누른 줄에 남는다.
    /// </summary>
    private async Task RestoreHighlightAsync()
    {
        if (_grid?.Grid is not { } grid)
        {
            return;
        }

        if (_selected is null)
        {
            grid.ClearSelection();
            return;
        }

        grid.SelectDataItem(_selected);
        await grid.SetFocusedDataItemAsync(_selected);
    }

    private void Revert()
    {
        if (_isNew)
        {
            ClosePanel();
            Repick();
            return;
        }

        if (_original is not null)
        {
            _edit = CompanyForm.Copy(_original);
        }
    }

    // ── 저장 · 삭제 · 순서 ───────────────────────────────────

    private void FillNew(CompanyDto company)
    {
        company.Status = 1;

        // 목록 끝에 붙인다. 0 이면 있는 회사들 사이에 끼어든다.
        company.SortOrder = _all.Count == 0 ? 0 : _all.Max(c => c.SortOrder) + 1;
        company.UsageLocations = string.IsNullOrEmpty(_usageFilter) ? [] : [_usageFilter];
    }

    /// <summary>수정 탭의 「저장」. 표의 저장 길(OnSave → 문구 → 다시 읽기)을 탄다.</summary>
    private async Task SaveEditAsync()
    {
        if (_edit is null || _grid is null || _saving)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_edit.Name))
        {
            Say("회사명을 적어 주십시오.", NoticeTone.Warning);
            return;
        }

        _saving = true;

        try
        {
            // 등록은 서버가 아이디를 돌려주지 않는다. 저장 전 아이디와 견주어 새 줄을 찾는다.
            var before = _isNew ? _all.Select(c => c.Id).ToHashSet(StringComparer.Ordinal) : null;

            var saved = await _grid.SaveAsync(_edit, _isNew);

            if (!saved || before is null)
            {
                return;
            }

            var created = _all.FirstOrDefault(c => !before.Contains(c.Id));
            _selected = created;

            if (created is null)
            {
                ClosePanel();
                return;
            }

            Open(created);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task PersistAsync((CompanyDto Item, bool IsNew) e)
    {
        var payload = CompanyForm.Payload(e.Item);

        if (e.IsNew)
        {
            await Api.CreateCompanyAsync(payload);
            return;
        }

        await Api.UpdateCompanyAsync(e.Item.Id, payload);
    }

    private Task DeleteAsync(CompanyDto company) => Api.DeleteCompanyAsync(company.Id);

    private static string DeleteMessage(CompanyDto c)
    {
        var scope = c.UserCount > 0 || c.DeptCount > 0
            ? $"\n이 회사에 사람 {c.UserCount}명 · 부서 {c.DeptCount}개가 딸려 있습니다."
            : string.Empty;

        return $"회사 「{c.Name}」 을(를) 지웁니다.{scope}\n되돌릴 수 없습니다.";
    }

    /// <summary>
    /// 줄을 끌어다 놓으면 곧바로 저장한다. 자리는 거른 목록이 아니라
    /// <see cref="_all"/> 에서 옮긴다 — 보이는 것만 보내면 가려진 회사들의 차례가 날아간다.
    /// </summary>
    private async Task OnRowsDroppedAsync(GridItemsDroppedEventArgs e)
    {
        if (e.DroppedItems.FirstOrDefault() is not CompanyDto moved
            || e.TargetItem is not CompanyDto target
            || ReferenceEquals(moved, target))
        {
            return;
        }

        var order = _all.ToList();

        if (!order.Remove(moved))
        {
            return;
        }

        // 뽑아낸 뒤에 자리를 찾는다. 먼저 찾으면 아래로 옮길 때 한 칸 어긋난다.
        var at = order.IndexOf(target);

        if (at < 0)
        {
            return;
        }

        order.Insert(e.DropPosition == GridItemDropPosition.Before ? at : at + 1, moved);

        await RunAsync(
            () => Api.ReorderCompaniesAsync([.. order.Select(c => c.Id)]),
            "차례를 바꿨습니다.", "차례를 바꾸지 못했습니다");

        // 성공이든 실패든 되읽는다 — 표는 놓는 순간 이미 줄을 옮겨 그렸다.
        await ReloadAsync();
    }
}
