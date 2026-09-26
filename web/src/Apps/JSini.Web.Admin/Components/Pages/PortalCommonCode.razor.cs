using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class PortalCommonCode
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_groupKeyword);

    private IReadOnlyList<CommonCodeGroupDto> _groups = [];
    private IReadOnlyList<CommonCodeDto> _codes = [];

    private CommonCodeGroupDto? _group;
    private string? _groupKeyword;

    /// <summary>편집 창이 등록으로 열렸는가. 묶음 코드를 잠글지와 AI 추천을 정한다.</summary>
    private bool _newGroup;

    private bool _editingCode;
    private bool _newCode;
    private string _codeId = string.Empty;
    private CommonCodeDto _codeEdit = new();

    private ConfirmDialog? _confirm;

    private IReadOnlyList<CommonCodeGroupDto> ShownGroups
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_groupKeyword))
            {
                return _groups;
            }

            var k = _groupKeyword.Trim();
            return [.. _groups.Where(g =>
                g.GroupCode.Contains(k, StringComparison.OrdinalIgnoreCase)
                || g.GroupName.Contains(k, StringComparison.OrdinalIgnoreCase))];
        }
    }

    /// <summary>상위 코드 고르개. 자기 자신은 뺀다 — 자기 밑에 자기를 달 수 없다.</summary>
    private IReadOnlyList<object> ParentCodeChoices =>
    [
        .. FlattenCodes(_codes)
            .Where(c => c.Id != _codeId)
            .Select(c => (object)new { Value = c.Id, Text = $"{c.CodeName} ({c.CodeValue})" })
    ];

    protected override Task OnInitializedAsync() => ReloadGroupsAsync();

    private Task ReloadGroupsAsync() => LoadAsync(async () =>
    {
        _groups = await Api.GetCodeGroupsAsync();

        // 고른 묶음이 사라졌으면(지웠거나 검색을 바꿨거나) 첫 묶음으로 옮긴다.
        // 안 그러면 오른쪽이 없는 묶음의 코드를 계속 들고 있는다.
        if (_group is not null && _groups.All(g => g.Id != _group.Id))
        {
            _group = null;
            _codes = [];
        }

        _group ??= _groups.FirstOrDefault();

        if (_group is not null)
        {
            await LoadCodesAsync();
        }

        return _groups.Count;
    }, "등록된 코드 묶음이 없습니다.", "코드 묶음을 읽지 못했습니다");

    private async Task OnGroupChangedAsync(CommonCodeGroupDto? group)
    {
        _group = group;
        _codes = [];

        if (group is not null)
        {
            await ReloadCodesAsync();
        }
    }

    private Task ReloadCodesAsync() => LoadAsync(async () =>
    {
        await LoadCodesAsync();
        return _codes.Count;
    }, "이 묶음에는 코드가 없습니다.", "코드를 읽지 못했습니다");

    private async Task LoadCodesAsync()
    {
        if (_group is null)
        {
            _codes = [];
            return;
        }

        _codes = await Api.GetCodesAsync(_group.GroupCode, _group.IsHierarchical);
    }

    // ── 묶음 ────────────────────────────────────────────────
    // 등록·수정·삭제·다시 읽기는 CommGrd 가 몫을 나눠 맡는다. 이 화면이 하는
    // 것은 **무엇을 채우고 무엇을 보내는가**뿐이다.

    private void FillNewGroup(CommonCodeGroupDto g) => g.SortOrder = NextGroupSortOrder();

    /// <summary>다음 순서 번호. 마지막 뒤에 붙인다 — 0 으로 두면 맨 앞에 끼어든다.</summary>
    private int NextGroupSortOrder() =>
        _groups.Count == 0 ? 1 : _groups.Max(g => g.SortOrder) + 1;

    /// <summary>
    /// 편집 창이 열릴 때. <b>수정으로 열 때도 불린다</b> — 묶음 코드를 잠그는
    /// 것과 AI 추천을 감추는 것이 그 구분에 걸려 있어서, 등록 때만 불리는
    /// <c>OnNew</c> 로는 앞선 등록의 상태가 그대로 남는다.
    /// </summary>
    private void OnGroupEditOpen(CommonCodeGroupDto g, bool isNew) => _newGroup = isNew;

    private Task SaveGroupAsync((CommonCodeGroupDto Item, bool IsNew) e)
    {
        if (string.IsNullOrWhiteSpace(e.Item.GroupCode) || string.IsNullOrWhiteSpace(e.Item.GroupName))
        {
            // ApiException 으로 올린다. CommGrd 가 잡아 안내로 옮기고 팝업을
            // 열어 두므로 쓴 내용이 사라지지 않는다.
            throw new ApiException("묶음 코드와 이름은 반드시 넣어야 합니다.");
        }

        var body = new SaveCommonCodeGroupDto
        {
            GroupCode = e.Item.GroupCode,
            GroupName = e.Item.GroupName,
            IsHierarchical = e.Item.IsHierarchical,
            SortOrder = e.Item.SortOrder,
            Remark = e.Item.Remark,
        };

        return e.IsNew
            ? Api.CreateCodeGroupAsync(body)
            : Api.UpdateCodeGroupAsync(e.Item.Id, body);
    }

    /// <summary>
    /// 삭제 확인 문구. 묶음을 지우면 <b>그 안의 코드가 함께 사라진다</b> —
    /// 무엇이 몇 건 사라지는지 적어 준다. DevExpress 기본 확인창으로는
    /// 어느 줄을 눌렀는지도 알 수 없어 묻는 의미가 절반쯤 없어진다.
    ///
    /// <para>
    /// 건수는 <b>고른 묶음일 때만</b> 안다 — 오른쪽에 읽어 둔 것이 그 묶음의
    /// 코드다. 식별자로 견준다. 목록을 다시 읽으면 같은 묶음이라도 <b>다른
    /// 인스턴스</b>라, 참조로 견주면 건수 안내가 조용히 사라진다.
    /// </para>
    /// </summary>
    private string GroupDeleteMessage(CommonCodeGroupDto g)
    {
        var count = _group is not null && _group.Id == g.Id ? FlattenCodes(_codes).Count : 0;
        var scope = count > 0 ? $"\n안에 든 코드 {count}건도 함께 사라집니다." : string.Empty;

        return $"묶음 「{g.GroupName}」({g.GroupCode}) 을(를) 지웁니다.{scope}\n되돌릴 수 없습니다.";
    }

    private Task DeleteGroupAsync(CommonCodeGroupDto g) => Api.DeleteCodeGroupAsync(g.Id);

    // ── 코드 ────────────────────────────────────────────────

    private void FillNewCode(CommonCodeDto c)
    {
        // 묶음 식별자를 여기서 넣는다. 안 넣으면 저장돼도 어느 묶음인지 몰라
        // 목록에서 사라진다.
        c.GroupId = _group?.Id ?? string.Empty;
        c.Status = 1;
        c.SortOrder = NextSortOrder();
    }

    /// <summary>
    /// 편집 창이 열릴 때. <see cref="OnGroupEditOpen"/> 과 같은 이유로 등록·수정
    /// 둘 다에서 불려야 한다 — 코드 값을 잠그는 것이 그 구분에 걸려 있다.
    ///
    /// <para>
    /// 상위 코드 고르개가 자기 자신을 빼려면 지금 고치는 것이 누구인지도
    /// 알아야 한다(<see cref="ParentCodeChoices"/>).
    /// </para>
    /// </summary>
    private void OnCodeEditOpen(CommonCodeDto c, bool isNew)
    {
        _newCode = isNew;
        _codeId = c.Id;
    }

    /// <summary>다음 순서 번호. 마지막 뒤에 붙인다 — 0 으로 두면 맨 앞에 끼어든다.</summary>
    private int NextSortOrder()
    {
        var all = FlattenCodes(_codes);
        return all.Count == 0 ? 1 : all.Max(c => c.SortOrder) + 1;
    }

    private Task SaveCodeAsync((CommonCodeDto Item, bool IsNew) e)
    {
        if (string.IsNullOrWhiteSpace(e.Item.CodeValue) || string.IsNullOrWhiteSpace(e.Item.CodeName))
        {
            throw new ApiException("코드 값과 이름은 반드시 넣어야 합니다.");
        }

        var body = new SaveCommonCodeDto
        {
            GroupId = e.Item.GroupId,
            ParentId = e.Item.ParentId,
            CodeValue = e.Item.CodeValue,
            CodeName = e.Item.CodeName,
            I18nKey = e.Item.I18nKey,
            SortOrder = e.Item.SortOrder,
            Status = e.Item.Status,
            Remark = e.Item.Remark,
        };

        return e.IsNew
            ? Api.CreateCodeAsync(body)
            : Api.UpdateCodeAsync(e.Item.Id, body);
    }

    /// <summary>
    /// 삭제 확인 문구. 코드에는 딸린 것을 셀 필요가 없다 — 하위가 있으면
    /// <see cref="DeleteCodeAsync"/> 가 아예 막는다.
    /// </summary>
    private static string CodeDeleteMessage(CommonCodeDto c) =>
        $"코드 「{c.CodeName}」({c.CodeValue}) 을(를) 지웁니다.\n되돌릴 수 없습니다.";

    private Task DeleteCodeAsync(CommonCodeDto c)
    {
        // 하위를 남기고 지우면 그 가지가 트리에서 사라져 화면에서 손댈 길이 없어진다.
        if (c.Children is { Count: > 0 })
        {
            throw new ApiException("하위 코드가 있어 지울 수 없습니다.");
        }

        return Api.DeleteCodeAsync(c.Id);
    }

    // ── 나무일 때만 도는 길 ─────────────────────────────────
    // 평평한 묶음은 위의 것들을 CommGrd 가 부른다. DxTreeList 에는 그 흐름이
    // 없어서 등록·수정·삭제를 여기서 손으로 엮는다.

    private void StartNewCode()
    {
        if (_group is null)
        {
            return;
        }

        _newCode = true;
        _codeId = string.Empty;
        _codeEdit = new CommonCodeDto();
        FillNewCode(_codeEdit);
        _editingCode = true;
    }

    private void StartEditCode(CommonCodeDto c)
    {
        // 복사본을 띄운다. 원본을 묶으면 취소해도 나무에 바뀐 값이 남는다.
        _codeEdit = new CommonCodeDto
        {
            Id = c.Id,
            GroupId = c.GroupId,
            ParentId = c.ParentId,
            CodeValue = c.CodeValue,
            CodeName = c.CodeName,
            I18nKey = c.I18nKey,
            SortOrder = c.SortOrder,
            Status = c.Status,
            Remark = c.Remark,
        };

        OnCodeEditOpen(_codeEdit, isNew: false);
        _editingCode = true;
    }

    private async Task SaveEditingCodeAsync()
    {
        var saved = await RunAsync(
            () => SaveCodeAsync((_codeEdit, _newCode)),
            _newCode ? "등록했습니다." : "저장했습니다.",
            _newCode ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (!saved)
        {
            // 실패하면 팝업을 닫지 않는다. 닫으면 쓴 내용이 사라진다.
            return;
        }

        _editingCode = false;
        await ReloadCodesAsync();
    }

    private async Task ConfirmDeleteCodeAsync(CommonCodeDto c)
    {
        if (!await _confirm!.AskAsync(CodeDeleteMessage(c)))
        {
            return;
        }

        if (await RunAsync(() => DeleteCodeAsync(c), "지웠습니다.", "지우지 못했습니다"))
        {
            await ReloadCodesAsync();
        }
    }

    private static List<CommonCodeDto> FlattenCodes(IEnumerable<CommonCodeDto> nodes)
    {
        var flat = new List<CommonCodeDto>();

        void Walk(IEnumerable<CommonCodeDto> items)
        {
            foreach (var item in items)
            {
                flat.Add(item);

                if (item.Children is { Count: > 0 } children)
                {
                    Walk(children);
                }
            }
        }

        Walk(nodes);
        return flat;
    }
}
