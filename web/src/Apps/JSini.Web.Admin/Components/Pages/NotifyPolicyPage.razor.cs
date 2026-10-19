using JSini.Web.Abstractions;
using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

/// <summary>오른쪽 표의 한 줄 — 역할 하나와 그 역할에 켠 길들. 서버가 준 줄을 직접 고치지 않는다 — 체크를 건드리면 저장 안 한 값이 목록의 「받는 설정」 칸에 적힌다. 역할은 전부 한 줄씩 있고, 저장할 때 길을 하나도 안 켠 줄은 빠진다.</summary>
internal sealed class NotifyPolicyRow
{
    public required string RoleId { get; init; }

    public required string RoleName { get; init; }

    /// <summary>그 역할에 걸린 사람 수. 고르기 전에 보여 준다.</summary>
    public int AccountCount { get; init; }

    public bool Push { get; set; }

    public bool Email { get; set; }

    /// <summary>켠 길이 하나라도 있나. 저장할 줄을 고르는 기준이다.</summary>
    public bool Picked => Push || Email;
}

/// <summary>역할 중심 탭의 한 줄 — 알림 이벤트 하나와 이 역할의 앱 푸시 · 이메일 설정.</summary>
internal sealed class RoleEventPolicyRow
{
    public required NotifyEventDto Event { get; init; }

    public bool Push { get; set; }

    public bool Email { get; set; }

    public bool Picked => Push || Email;
}

public partial class NotifyPolicyPage : IDisposable
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>활성 탭 번호 (0: 이벤트 중심, 1: 역할 중심).</summary>
    private int _activeTab;

    private int ActiveTab
    {
        get => _activeTab;
        set
        {
            if (_activeTab != value)
            {
                _activeTab = value;
                OnTabChanged();
            }
        }
    }

    private List<NotifyEventDto> _events = [];

    private IReadOnlyList<NotifyRoleDto> _roles = [];

    /// <summary>역할 중심 탭에서 선택된 역할.</summary>
    private NotifyRoleDto? _selectedRole;

    /// <summary>역할 중심 탭에서 그리는 이벤트별 정책 줄들.</summary>
    private List<RoleEventPolicyRow> _roleEventRows = [];

    /// <summary>고른 이벤트. 왼쪽 표와 오른쪽 판이 이 값을 나눠 본다.</summary>
    private NotifyEventDto? _selected;

    /// <summary>오른쪽 표가 그리는 줄들. 역할 전부가 한 줄씩이다.</summary>
    private List<NotifyPolicyRow> _rows = [];

    /// <summary>고치고 있는 「이 이벤트를 쓴다」. 고른 줄을 직접 안 건드린다.</summary>
    private bool _isActive = true;

    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private int _pendingSaves;

    private int _targetVersion;

    private int _savedVersion;

    private string? _keyword;

    private bool _activeOnly;

    /// <summary>
    /// 정책밖 이벤트를 목록에서 뺄 것인가. 기본값은 참이다.
    /// </summary>
    private bool _excludeUngoverned = true;

    private bool _showRecipients;

    private NotifyPolicyPreviewDto? _preview;

    private ConfirmDialog? _confirm;

    /// <summary>고칠 수 있는 사람인가. 체크 칸을 잠그는 데 쓴다 — 누르는 즉시 값이 바뀌는 칸이라 저장 단추만 가려서는 막지 못한다. 서버도 같은 표를 보므로 여기서 잠그는 것은 왕복을 아끼는 것이다.</summary>
    private bool _canEdit;

    /// <summary>
    /// 화면에 표시할 알림 이벤트 목록. 정책밖 제외 체크에 따라 거른다.
    /// </summary>
    private IReadOnlyList<NotifyEventDto> VisibleEvents =>
        _excludeUngoverned
            ? _events.Where(e => e.Governed).ToList()
            : _events;

    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.On(_activeOnly, "쓰는 것만"));

    private string EventHint => $"{VisibleEvents.Count}건";

    /// <summary>화면에 표시할 역할 목록. 검색어가 있으면 이름이나 식별자로 거른다.</summary>
    private IReadOnlyList<NotifyRoleDto> VisibleRoles =>
        string.IsNullOrWhiteSpace(_keyword)
            ? _roles
            : _roles.Where(r => r.Name.Contains(_keyword, StringComparison.OrdinalIgnoreCase)
                                || r.Id.Contains(_keyword, StringComparison.OrdinalIgnoreCase)).ToList();

    private string RoleHint => $"{VisibleRoles.Count}건";

    /// <summary>체크를 만질 수 있나. 정책이 안 걸리는 이벤트에서는 잠근다.</summary>
    private bool CanPick => _canEdit && _selected is { Governed: true };

    /// <summary>역할을 하나도 안 걸었나. 「제한 없음」 띠의 조건이다.</summary>
    private bool NoRolePicked => !_rows.Any(r => r.Picked);

    /// <summary>역할을 하나도 안 걸었을 때 띄우는 말. 푸시와 메일을 갈라 적는다 — 푸시는 안 걸면 지금처럼 나가고 메일은 한 통도 안 나가서, 한쪽만 적으면 「제한 없음」을 메일도 모두에게 간다로 읽는다.</summary>
    private string NoRoleText =>
        _selected is { PolicyRequired: true }

            // 이 이벤트는 설정 파일에 기본 역할이 없어 역할이 비면 아무에게도 안 간다 — 「지금까지처럼」과 같은 말을 적으면 끈 줄을 모르고 들어온 문의를 못 본다.
            ? "받는 역할이 하나도 없습니다 — 이 알림은 지금 아무에게도 가지 않습니다. "
              + "이 목록이 받는 사람의 정본이라, 보내는 쪽에 적어 둔 기본 역할이 없습니다."

            : "역할을 하나도 걸지 않았습니다 — 제한 없이 지금까지와 똑같이 나갑니다. "
              + "하나라도 걸면 그때부터 이 목록이 기준이 됩니다."
              + (_selected is { SupportsEmail: true, EmailFromPush: true }
                  ? " 다만 이메일은 지금 한 통도 나가지 않습니다 — 켠 역할이 있어야 보냅니다."
                  : string.Empty);

    /// <summary>
    /// 역할이 빈 상태의 띠 색. <b>경고인지 안내인지가 이벤트마다 갈린다</b> —
    /// 「지금 그대로 나간다」는 알림이고 「아무에게도 안 간다」는 경고다.
    /// </summary>
    private NoticeTone NoRoleTone =>
        _selected is { PolicyRequired: true } ? NoticeTone.Warning : NoticeTone.Info;

    /// <summary>정책이 이 이벤트에서 무슨 일을 하는지 한 줄.</summary>
    private string EffectText => NotifyTargetKinds.Effect(_selected?.TargetKind);

    /// <summary>정책이 안 걸리는 이벤트에 띄우는 까닭.</summary>
    private string UngovernedText =>
        "이 알림은 아직 설정이 걸리지 않습니다 — 보내는 자리가 알림 서버를 거치지 않거나, "
        + "막으면 안 되는 알림입니다(시험 발송 · 비밀번호 재설정). 목록에 두는 것은 "
        + "「이런 알림이 나간다」를 숨기지 않기 위해서입니다.";

    private string PushHint => _selected is { SupportsPush: false }
        ? "이 알림은 앱 푸시로 나가지 않습니다."
        : "이 역할에 앱 푸시로 보냅니다.";

    /// <summary>「이메일」 체크에 붙는 한 줄. 메일이 나가는 길은 두 갈래다 — 푸시 경로가 함께 내는 이벤트는 앱 알림과 같은 글이, 보내는 쪽이 따로 내는 이벤트는 그쪽이 만든 틀의 메일이 온다. 푸시와 기본값이 반대라는 것도 여기서 말한다.</summary>
    private string MailHint => _selected switch
    {
        { SupportsEmail: false } => "이 알림은 이메일로 나가지 않습니다.",
        { EmailFromPush: true } => "이 역할에 앱 알림과 같은 내용을 이메일로도 보냅니다. "
                                   + "켠 역할이 하나도 없으면 메일은 나가지 않습니다.",
        _ => "이 역할에 이메일로 보냅니다 — 보내는 쪽이 만든 메일입니다.",
    };

    /// <summary>「받는 사람」 창 아래의 셈 한 줄.</summary>
    private string ReachText => _preview is null
        ? string.Empty
        : $"{_preview.Recipients.Count}명 중 앱 푸시 {_preview.PushReachable}명 · "
          + $"이메일 {_preview.EmailReachable}명에게 실제로 닿습니다.";

    /// <summary>나가는 길을 한 줄로. 이벤트마다 쓸 수 있는 길이 다르다.</summary>
    private static string ChannelText(NotifyEventDto e)
    {
        var parts = new List<string>();
        if (e.SupportsPush) parts.Add("앱 푸시");
        if (e.SupportsEmail) parts.Add("이메일");

        return parts.Count == 0 ? "—" : string.Join(" · ", parts);
    }

    /// <summary>왼쪽 카드의 색갈래. 왼쪽 굵은 선 하나로 지금 상태를 말한다 — 색만으로 가르지 않고 카드 아래 줄에 글자가 늘 서 있다.</summary>
    private string CardTone(NotifyEventDto e) => e switch
    {
        { Governed: false } => "off",
        _ when !IsEventActive(e) => "warn",

        // 같은 체크 0 개라도 이 표가 받는 사람의 정본인 이벤트에서는 알림이 멎은 것이라 경고 색을 쓴다.
        { Unrestricted: true, PolicyRequired: true } => "warn",
        { Unrestricted: true } => "free",
        _ => "on",
    };

    /// <summary>닿나 못 닿나. 글자 하나가 체크 표시보다 읽기 쉽다.</summary>
    private static string Mark(bool reaches) => reaches ? "○" : "—";

    /// <summary>시각 한 줄. <b>받은 값은 UTC 라</b> 보여 주기 직전에 옮긴다.</summary>
    private static string Stamp(DateTime? utc) => utc.Kst("yyyy-MM-dd HH:mm");

    protected override async Task OnInitializedAsync()
    {
        // 체크 칸은 누르는 즉시 값이 바뀌므로 화면에서도 가린다. 경로 판정은 BasePage.Can 한 곳에 있다 — 두 벌이 되면 감춘 단추와 판정이 갈린다.
        _canEdit = Can(MenuAction.Update);

        await LoadRolesAsync();
        await ReloadAsync();
    }

    private Task LoadRolesAsync() => LoadAsync(async () =>
    {
        _roles = await Api.GetNotifyRolesAsync();

        if (_selectedRole is null || !_roles.Any(r => r.Id == _selectedRole.Id))
        {
            _selectedRole = _roles.FirstOrDefault();
        }

        // 역할 목록이 새로 들어왔으니 양쪽 폼을 다시 세운다.
        if (_selected is not null) LoadForm(_selected);
        if (_selectedRole is not null) LoadRoleForm(_selectedRole);

        return _roles.Count;
    }, "등록된 역할이 없습니다.", "역할 목록을 읽지 못했습니다");

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        if (_pendingSaves > 0)
        {
            await _saveGate.WaitAsync();
            _saveGate.Release();
        }

        _events = [.. await Api.GetNotifyEventsAsync(_keyword, _activeOnly)];

        // 저장 뒤 목록을 새로 읽으면 같은 코드의 다른 객체가 오므로, 보던 이벤트를 다시 집는다.
        var keep = _selected?.Code;
        var visible = VisibleEvents;
        _selected = keep is null ? null : visible.FirstOrDefault(e => e.Code == keep);

        // 첫 줄을 우리가 고른다 — 왼쪽이 표였을 때는 CommGrd 가 했지만 카드로 바꾸면서 그 일을 할 사람이 없어졌다.
        _selected ??= visible.FirstOrDefault();

        LoadForm(_selected);

        // 역할 중심 탭의 선택 역할도 유지한다.
        var keepRole = _selectedRole?.Id;
        var visibleRoles = VisibleRoles;
        _selectedRole = keepRole is null ? null : visibleRoles.FirstOrDefault(r => r.Id == keepRole);
        _selectedRole ??= visibleRoles.FirstOrDefault() ?? _roles.FirstOrDefault();

        LoadRoleForm(_selectedRole);

        return visible.Count;
    }, "등록된 알림 이벤트가 없습니다.", "알림 이벤트를 읽지 못했습니다");

    private Task ResetAsync()
    {
        _keyword = null;
        _activeOnly = false;
        _excludeUngoverned = true;
        return ReloadAsync();
    }

    /// <summary>
    /// 정책밖 제외 체크박스 변경 시 표시 목록과 선택 항목을 갱신한다.
    /// </summary>
    private void OnExcludeUngovernedChanged(bool value)
    {
        _excludeUngoverned = value;
        EnsureSelectedVisible();
    }

    /// <summary>
    /// 현재 선택된 이벤트나 역할이 숨겨졌으면 표시 중인 첫 번째 항목으로 바꾼다.
    /// </summary>
    private void EnsureSelectedVisible()
    {
        var visible = VisibleEvents;
        if (_selected is null || !visible.Any(e => e.Code == _selected.Code))
        {
            _selected = visible.FirstOrDefault();
            LoadForm(_selected);
        }

        if (_selectedRole is not null)
        {
            LoadRoleForm(_selectedRole);
        }
    }

    /// <summary>탭 전환 시 호출된다.</summary>
    private void OnTabChanged()
    {
        if (_activeTab == 1)
        {
            if (_selectedRole is null && _roles.Count > 0)
            {
                _selectedRole = _roles.FirstOrDefault();
            }
            LoadRoleForm(_selectedRole);
        }
        else
        {
            EnsureSelectedVisible();
        }
    }

    /// <summary>역할 중심 탭에서 역할을 선택했을 때 호출된다.</summary>
    private async Task SelectRoleAsync(NotifyRoleDto? role)
    {
        if (role is null || role.Id == _selectedRole?.Id) return;

        if (_pendingSaves > 0)
        {
            await _saveGate.WaitAsync();
            _saveGate.Release();
        }

        _selectedRole = role;
        LoadRoleForm(role);
    }

    /// <summary>선택한 역할을 기준으로 알림 이벤트별 정책 줄들을 구성한다.</summary>
    private void LoadRoleForm(NotifyRoleDto? role)
    {
        if (role is null)
        {
            _roleEventRows = [];
            return;
        }

        _roleEventRows =
        [
            .. VisibleEvents.Select(ev =>
            {
                var hit = ev.Policies.FirstOrDefault(
                    p => string.Equals(p.RoleId, role.Id, StringComparison.OrdinalIgnoreCase));

                return new RoleEventPolicyRow
                {
                    Event = ev,
                    Push = hit is { IsActive: true, PushEnabled: true },
                    Email = hit is { IsActive: true, EmailEnabled: true },
                };
            })
        ];
    }

    /// <summary>이벤트를 골랐다. 고른 것이 풀리면(null) 폼을 비우지 않는다 — 표가 다시 그려질 때 잠깐 풀리는 자리가 있어 그때 비우면 고치던 체크가 사라진다.</summary>
    private async Task SelectAsync(NotifyEventDto? row)
    {
        if (row is null || row.Code == _selected?.Code) return;

        if (_pendingSaves > 0)
        {
            await _saveGate.WaitAsync();
            _saveGate.Release();
        }

        _selected = row;
        LoadForm(row);
    }

    /// <summary>고른 이벤트를 오른쪽 표로 편다. 역할 전부가 한 줄씩이고 걸린 역할만 체크가 켜진다. 꺼 둔 정책 줄은 안 켠 것으로 본다 — 화면에 체크 칸이 둘뿐이라 쉬는 줄을 그릴 자리가 없다.</summary>
    private void LoadForm(NotifyEventDto? row)
    {
        if (row is null)
        {
            _rows = [];
            return;
        }

        _isActive = row.IsActive;

        _rows =
        [
            .. _roles.Select(role =>
            {
                var hit = row.Policies.FirstOrDefault(
                    p => string.Equals(p.RoleId, role.Id, StringComparison.OrdinalIgnoreCase));

                return new NotifyPolicyRow
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    AccountCount = role.AccountCount,
                    Push = hit is { IsActive: true, PushEnabled: true },
                    Email = hit is { IsActive: true, EmailEnabled: true },
                };
            })
        ];
    }

    /// <summary>앱 푸시 체크박스 변경 시 곧바로 저장한다.</summary>
    private Task SetPushAsync(NotifyPolicyRow row, bool value)
    {
        if (!CanPick || !_selected!.SupportsPush || row.Push == value)
        {
            return Task.CompletedTask;
        }

        row.Push = value;
        return SavePolicyAsync();
    }

    /// <summary>이메일 체크박스 변경 시 곧바로 저장한다.</summary>
    private Task SetEmailAsync(NotifyPolicyRow row, bool value)
    {
        if (!CanPick || !_selected!.SupportsEmail || row.Email == value)
        {
            return Task.CompletedTask;
        }

        row.Email = value;
        return SavePolicyAsync();
    }

    /// <summary>모든 역할에 앱 푸시가 켜져 있는지 확인한다.</summary>
    private bool AllPush => _rows.Count > 0 && _rows.All(r => r.Push);

    /// <summary>모든 역할에 이메일이 켜져 있는지 확인한다.</summary>
    private bool AllEmail => _rows.Count > 0 && _rows.All(r => r.Email);

    /// <summary>모든 역할의 앱 푸시 체크를 한 번에 켜거나 끈다.</summary>
    private Task OnAllPushChangedAsync(bool value)
    {
        if (!CanPick || !_selected!.SupportsPush)
        {
            return Task.CompletedTask;
        }

        var changed = false;
        foreach (var row in _rows)
        {
            if (row.Push != value)
            {
                row.Push = value;
                changed = true;
            }
        }

        return changed ? SavePolicyAsync() : Task.CompletedTask;
    }

    /// <summary>모든 역할의 이메일 체크를 한 번에 켜거나 끈다.</summary>
    private Task OnAllEmailChangedAsync(bool value)
    {
        if (!CanPick || !_selected!.SupportsEmail)
        {
            return Task.CompletedTask;
        }

        var changed = false;
        foreach (var row in _rows)
        {
            if (row.Email != value)
            {
                row.Email = value;
                changed = true;
            }
        }

        return changed ? SavePolicyAsync() : Task.CompletedTask;
    }

    /// <summary>
    /// 이벤트의 사용 여부를 구한다. 지금 고치고 있는 선택 이벤트는 화면의 _isActive 를 따른다.
    /// </summary>
    private bool IsEventActive(NotifyEventDto e) =>
        _selected?.Code == e.Code ? _isActive : e.IsActive;

    /// <summary>키보드로 카드를 선택한다.</summary>
    private Task OnCardKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, NotifyEventDto ev)
    {
        if (e.Key is "Enter" or " ")
        {
            return SelectAsync(ev);
        }
        return Task.CompletedTask;
    }

    /// <summary>카드 목록의 스위치 변경 시 해당 이벤트의 사용 여부를 곧바로 바꾸고 저장한다.</summary>
    private async Task OnCardActiveChangedAsync(NotifyEventDto ev, bool value)
    {
        if (!_canEdit || !ev.Governed || IsEventActive(ev) == value)
        {
            return;
        }

        if (_selected?.Code != ev.Code)
        {
            await SelectAsync(ev);
        }

        await OnActiveChangedAsync(value);
    }

    /// <summary>이벤트 사용 여부 스위치 변경 시 저장한다.</summary>
    private async Task OnActiveChangedAsync(bool value)
    {
        if (_selected is null || !_selected.Governed || !_canEdit || _isActive == value)
        {
            return;
        }

        if (!value)
        {
            if (_confirm is not null)
            {
                var asked = await _confirm.AskAsync(
                    $"「{_selected.Name}」 알림을 끕니다. 역할과 상관없이 아무에게도 나가지 않습니다.",
                    confirmText: "끄기",
                    confirmStyle: DevExpress.Blazor.ButtonRenderStyle.Danger);

                if (!asked)
                {
                    StateHasChanged();
                    return;
                }
            }
        }

        _isActive = value;
        await SavePolicyAsync();
    }

    /// <summary>체크박스 변경 시 곧바로 정책을 저장한다.</summary>
    private async Task SavePolicyAsync()
    {
        if (_selected is null || !_canEdit || !_selected.Governed) return;

        var target = _selected;
        var targetCode = target.Code;

        _targetVersion++;
        var myVersion = _targetVersion;

        _pendingSaves++;
        StateHasChanged();

        await _saveGate.WaitAsync();
        try
        {
            // 대상 이벤트가 바뀌었거나 더 최신 상태가 이미 저장되었으면 중복 저장을 건너뛴다.
            if (_selected?.Code != targetCode || _savedVersion >= myVersion)
            {
                return;
            }

            var versionToSave = _targetVersion;

            var request = new SaveNotifyPolicyDto
            {
                IsActive = _isActive,
                Policies =
                [
                    .. _rows.Where(r => r.Picked).Select(r => new SaveNotifyPolicyRowDto
                    {
                        RoleId = r.RoleId,
                        PushEnabled = r.Push,
                        EmailEnabled = r.Email,
                        IsActive = true,
                    })
                ],
            };

            var saved = await Api.SaveNotifyPolicyAsync(targetCode, request);
            _savedVersion = versionToSave;

            if (saved is not null)
            {
                var idx = _events.FindIndex(e => e.Code == saved.Code);
                if (idx >= 0)
                {
                    _events[idx] = saved;
                }

                if (_selected?.Code == saved.Code)
                {
                    _selected.Policies = saved.Policies;
                    _selected.Unrestricted = saved.Unrestricted;
                    _selected.UpdatedAt = saved.UpdatedAt;
                    _selected.IsActive = saved.IsActive;
                }
            }
        }
        catch (ApiException ex)
        {
            Say($"알림 설정을 저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            if (_selected?.Code == targetCode)
            {
                LoadForm(_selected);
            }
        }
        catch (Exception ex)
        {
            Say($"알림 설정을 저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            if (_selected?.Code == targetCode)
            {
                LoadForm(_selected);
            }
        }
        finally
        {
            _saveGate.Release();
            _pendingSaves--;
            StateHasChanged();
        }
    }

    /// <summary>이 역할에 부여된 총 이벤트(푸시 또는 이메일 켠 것) 수를 센다.</summary>
    private int RoleTotalEventsCount(NotifyRoleDto r) =>
        _events.Count(e => e.Policies.Any(p => string.Equals(p.RoleId, r.Id, StringComparison.OrdinalIgnoreCase) && p.IsActive && (p.PushEnabled || p.EmailEnabled)));

    /// <summary>이 역할에 부여된 앱 푸시 수신 이벤트 수를 센다.</summary>
    private int RolePushCount(NotifyRoleDto r) =>
        _events.Count(e => e.Policies.Any(p => string.Equals(p.RoleId, r.Id, StringComparison.OrdinalIgnoreCase) && p.IsActive && p.PushEnabled));

    /// <summary>이 역할에 부여된 이메일 수신 이벤트 수를 센다.</summary>
    private int RoleEmailCount(NotifyRoleDto r) =>
        _events.Count(e => e.Policies.Any(p => string.Equals(p.RoleId, r.Id, StringComparison.OrdinalIgnoreCase) && p.IsActive && p.EmailEnabled));

    /// <summary>역할 카드 하단에 표시할 권한 요약 문구.</summary>
    private string RoleChannelSummary(NotifyRoleDto r)
    {
        var push = RolePushCount(r);
        var mail = RoleEmailCount(r);
        var total = RoleTotalEventsCount(r);

        if (total == 0) return "지정된 알림 없음";

        var parts = new List<string>();
        if (push > 0) parts.Add($"앱푸시 {push}");
        if (mail > 0) parts.Add($"이메일 {mail}");

        return $"이벤트 {total}건 · {string.Join(" · ", parts)}";
    }

    /// <summary>역할 카드의 왼쪽 톤 선 색상.</summary>
    private string RoleCardTone(NotifyRoleDto r) =>
        RoleTotalEventsCount(r) > 0 ? "on" : "off";

    /// <summary>역할 중심 탭에서 체크박스를 수정할 수 있는지 판정한다.</summary>
    private bool CanPickRoleEvent(NotifyEventDto ev) =>
        _canEdit && ev.Governed;

    private static string RolePushHint(NotifyEventDto ev) =>
        ev.SupportsPush ? "이 역할에 앱 푸시로 보냅니다." : "이 알림은 앱 푸시로 나가지 않습니다.";

    private static string RoleMailHint(NotifyEventDto ev) =>
        ev.SupportsEmail ? "이 역할에 이메일로 보냅니다." : "이 알림은 이메일로 나가지 않습니다.";

    private string RoleEffectText => _selectedRole is null
        ? string.Empty
        : $"「{_selectedRole.Name}」 역할에 부여된 알림 이벤트 수신 권한을 확인하고 지정합니다. 설정 변경 시 즉시 저장됩니다.";

    /// <summary>선택된 역할에 대해 모든 지원 이벤트의 앱 푸시가 켜져 있는지 확인한다.</summary>
    private bool AllRolePush =>
        _roleEventRows.Count > 0
        && _roleEventRows.Where(r => CanPickRoleEvent(r.Event) && r.Event.SupportsPush).All(r => r.Push);

    /// <summary>선택된 역할에 대해 모든 지원 이벤트의 이메일이 켜져 있는지 확인한다.</summary>
    private bool AllRoleEmail =>
        _roleEventRows.Count > 0
        && _roleEventRows.Where(r => CanPickRoleEvent(r.Event) && r.Event.SupportsEmail).All(r => r.Email);

    /// <summary>선택된 역할의 모든 지원 이벤트에 대해 앱 푸시를 일괄 변경한다.</summary>
    private Task OnAllRolePushChangedAsync(bool value)
    {
        if (_selectedRole is null || !_canEdit) return Task.CompletedTask;

        var changed = new List<RoleEventPolicyRow>();
        foreach (var r in _roleEventRows)
        {
            if (CanPickRoleEvent(r.Event) && r.Event.SupportsPush && r.Push != value)
            {
                r.Push = value;
                changed.Add(r);
            }
        }

        return changed.Count > 0 ? SaveRoleEventsBatchAsync(changed) : Task.CompletedTask;
    }

    /// <summary>선택된 역할의 모든 지원 이벤트에 대해 이메일을 일괄 변경한다.</summary>
    private Task OnAllRoleEmailChangedAsync(bool value)
    {
        if (_selectedRole is null || !_canEdit) return Task.CompletedTask;

        var changed = new List<RoleEventPolicyRow>();
        foreach (var r in _roleEventRows)
        {
            if (CanPickRoleEvent(r.Event) && r.Event.SupportsEmail && r.Email != value)
            {
                r.Email = value;
                changed.Add(r);
            }
        }

        return changed.Count > 0 ? SaveRoleEventsBatchAsync(changed) : Task.CompletedTask;
    }

    /// <summary>역할 중심 탭에서 특정 이벤트의 앱 푸시 체크박스 변경 시 즉시 저장한다.</summary>
    private Task SetRoleEventPushAsync(RoleEventPolicyRow row, bool value)
    {
        if (_selectedRole is null || !CanPickRoleEvent(row.Event) || !row.Event.SupportsPush || row.Push == value)
        {
            return Task.CompletedTask;
        }

        row.Push = value;
        return SaveRoleEventPolicyAsync(row);
    }

    /// <summary>역할 중심 탭에서 특정 이벤트의 이메일 체크박스 변경 시 즉시 저장한다.</summary>
    private Task SetRoleEventEmailAsync(RoleEventPolicyRow row, bool value)
    {
        if (_selectedRole is null || !CanPickRoleEvent(row.Event) || !row.Event.SupportsEmail || row.Email == value)
        {
            return Task.CompletedTask;
        }

        row.Email = value;
        return SaveRoleEventPolicyAsync(row);
    }

    /// <summary>역할 중심 탭에서 한 이벤트의 정책 변경사항을 저장한다.</summary>
    private async Task SaveRoleEventPolicyAsync(RoleEventPolicyRow row)
    {
        if (_selectedRole is null || !_canEdit || !row.Event.Governed) return;

        var roleId = _selectedRole.Id;
        var ev = row.Event;

        var hit = ev.Policies.FirstOrDefault(
            p => string.Equals(p.RoleId, roleId, StringComparison.OrdinalIgnoreCase));

        if (hit is null)
        {
            if (row.Push || row.Email)
            {
                ev.Policies.Add(new NotifyPolicyDto
                {
                    RoleId = roleId,
                    RoleName = _selectedRole.Name,
                    PushEnabled = row.Push,
                    EmailEnabled = row.Email,
                    IsActive = true,
                    AccountCount = _selectedRole.AccountCount,
                });
            }
        }
        else
        {
            hit.PushEnabled = row.Push;
            hit.EmailEnabled = row.Email;
            hit.IsActive = true;
        }

        var request = new SaveNotifyPolicyDto
        {
            IsActive = ev.IsActive,
            Policies =
            [
                .. ev.Policies
                    .Where(p => p.IsActive && (p.PushEnabled || p.EmailEnabled))
                    .Select(p => new SaveNotifyPolicyRowDto
                    {
                        RoleId = p.RoleId,
                        PushEnabled = p.PushEnabled,
                        EmailEnabled = p.EmailEnabled,
                        IsActive = true,
                    })
            ],
        };

        _pendingSaves++;
        StateHasChanged();

        await _saveGate.WaitAsync();
        try
        {
            var saved = await Api.SaveNotifyPolicyAsync(ev.Code, request);
            if (saved is not null)
            {
                var idx = _events.FindIndex(e => e.Code == saved.Code);
                if (idx >= 0) _events[idx] = saved;

                ev.Policies = saved.Policies;
                ev.Unrestricted = saved.Unrestricted;
                ev.UpdatedAt = saved.UpdatedAt;
                ev.IsActive = saved.IsActive;

                if (_selected?.Code == saved.Code)
                {
                    _selected.Policies = saved.Policies;
                    _selected.Unrestricted = saved.Unrestricted;
                    _selected.UpdatedAt = saved.UpdatedAt;
                    _selected.IsActive = saved.IsActive;
                    LoadForm(_selected);
                }
            }
        }
        catch (Exception ex)
        {
            Say($"알림 설정을 저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            LoadRoleForm(_selectedRole);
        }
        finally
        {
            _saveGate.Release();
            _pendingSaves--;
            StateHasChanged();
        }
    }

    /// <summary>역할 중심 탭에서 여러 이벤트의 정책 변경사항을 일괄 저장한다.</summary>
    private async Task SaveRoleEventsBatchAsync(List<RoleEventPolicyRow> rows)
    {
        if (_selectedRole is null || !_canEdit || rows.Count == 0) return;

        var roleId = _selectedRole.Id;
        _pendingSaves++;
        StateHasChanged();

        await _saveGate.WaitAsync();
        try
        {
            foreach (var row in rows)
            {
                var ev = row.Event;
                var hit = ev.Policies.FirstOrDefault(
                    p => string.Equals(p.RoleId, roleId, StringComparison.OrdinalIgnoreCase));

                if (hit is null)
                {
                    if (row.Push || row.Email)
                    {
                        ev.Policies.Add(new NotifyPolicyDto
                        {
                            RoleId = roleId,
                            RoleName = _selectedRole.Name,
                            PushEnabled = row.Push,
                            EmailEnabled = row.Email,
                            IsActive = true,
                            AccountCount = _selectedRole.AccountCount,
                        });
                    }
                }
                else
                {
                    hit.PushEnabled = row.Push;
                    hit.EmailEnabled = row.Email;
                    hit.IsActive = true;
                }

                var request = new SaveNotifyPolicyDto
                {
                    IsActive = ev.IsActive,
                    Policies =
                    [
                        .. ev.Policies
                            .Where(p => p.IsActive && (p.PushEnabled || p.EmailEnabled))
                            .Select(p => new SaveNotifyPolicyRowDto
                            {
                                RoleId = p.RoleId,
                                PushEnabled = p.PushEnabled,
                                EmailEnabled = p.EmailEnabled,
                                IsActive = true,
                            })
                    ],
                };

                var saved = await Api.SaveNotifyPolicyAsync(ev.Code, request);
                if (saved is not null)
                {
                    var idx = _events.FindIndex(e => e.Code == saved.Code);
                    if (idx >= 0) _events[idx] = saved;

                    ev.Policies = saved.Policies;
                    ev.Unrestricted = saved.Unrestricted;
                    ev.UpdatedAt = saved.UpdatedAt;
                    ev.IsActive = saved.IsActive;

                    if (_selected?.Code == saved.Code)
                    {
                        _selected.Policies = saved.Policies;
                        _selected.Unrestricted = saved.Unrestricted;
                        _selected.UpdatedAt = saved.UpdatedAt;
                        _selected.IsActive = saved.IsActive;
                    }
                }
            }

            if (_selected is not null) LoadForm(_selected);
        }
        catch (Exception ex)
        {
            Say($"일부 설정을 저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            LoadRoleForm(_selectedRole);
        }
        finally
        {
            _saveGate.Release();
            _pendingSaves--;
            StateHasChanged();
        }
    }

    /// <summary>지정된 이벤트의 수신자 미리보기 창을 연다.</summary>
    private async Task OpenRecipientsForEventAsync(NotifyEventDto ev)
    {
        if (ev is null) return;

        if (_pendingSaves > 0)
        {
            await _saveGate.WaitAsync();
            _saveGate.Release();
        }

        _preview = null;
        _showRecipients = true;

        try
        {
            _preview = await Api.GetNotifyRecipientsAsync(ev.Code);
        }
        catch (ApiException ex)
        {
            _showRecipients = false;
            Say($"받는 사람을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    /// <summary>
    /// 받는 사람을 띄운다. <b>창을 먼저 열고 읽는다</b> — 읽는 동안 아무 일도
    /// 일어나지 않으면 눌리지 않은 줄 알고 다시 누른다.
    /// </summary>
    private async Task OpenRecipientsAsync()
    {
        if (_selected is null) return;

        if (_pendingSaves > 0)
        {
            await _saveGate.WaitAsync();
            _saveGate.Release();
        }

        _preview = null;
        _showRecipients = true;

        try
        {
            _preview = await Api.GetNotifyRecipientsAsync(_selected.Code);
        }
        catch (ApiException ex)
        {
            _showRecipients = false;
            Say($"받는 사람을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    public void Dispose()
    {
        _saveGate.Dispose();
    }
}
