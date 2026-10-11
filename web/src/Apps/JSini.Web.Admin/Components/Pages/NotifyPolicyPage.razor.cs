using JSini.Web.Abstractions;
using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

/// <summary>
/// 오른쪽 표의 한 줄 — 역할 하나와 그 역할에 켠 길들.
/// </summary>
/// <remarks>
/// <para>
/// <b>서버가 준 줄(<see cref="NotifyPolicyDto"/>)을 직접 고치지 않는다.</b>
/// 그 줄은 왼쪽 목록이 들고 있는 바로 그 객체라, 체크를 건드리는 순간 저장도
/// 안 한 값이 목록의 「받는 설정」 칸에 적힌다 — 「고치다 말고 다른 이벤트를
/// 눌렀다」가 되돌릴 수 없게 된다. 보고서 메일 화면이 같은 자리에서 내린
/// 판단이다.
/// </para>
/// <para>
/// <b>역할 전부가 한 줄씩 있다.</b> 걸지 않은 역할도 체크가 꺼진 채로 있고,
/// 저장할 때 길을 하나도 안 켠 줄은 빠진다(서버도 같은 것을 버린다).
/// </para>
/// </remarks>
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

public partial class NotifyPolicyPage : IDisposable
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private List<NotifyEventDto> _events = [];

    private IReadOnlyList<NotifyRoleDto> _roles = [];

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

    /// <summary>
    /// 고칠 수 있는 사람인가. <b>체크 칸을 잠그는 데 쓴다</b> — 누르는 즉시
    /// 값이 바뀌는 칸이라 저장 단추만 가려서는 막지 못한다.
    /// </summary>
    /// <remarks>
    /// 서버도 같은 표(<c>scom.role_menus</c>)를 본다. 여기서 잠그는 것은
    /// 왕복을 아끼는 것이지 유일한 문이 아니다.
    /// </remarks>
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

    /// <summary>체크를 만질 수 있나. 정책이 안 걸리는 이벤트에서는 잠근다.</summary>
    private bool CanPick => _canEdit && _selected is { Governed: true };

    /// <summary>역할을 하나도 안 걸었나. 「제한 없음」 띠의 조건이다.</summary>
    private bool NoRolePicked => !_rows.Any(r => r.Picked);

    /// <summary>
    /// 역할을 하나도 안 걸었을 때 띄우는 말. <b>푸시와 메일을 갈라 적는다.</b>
    /// </summary>
    /// <remarks>
    /// 두 길의 기본값이 반대다 — 푸시는 안 걸면 「지금까지와 똑같이」 나가고,
    /// 푸시 경로가 함께 내는 메일은 <b>한 통도 안 나간다</b>. 한쪽만 적으면
    /// 「제한 없음」을 「메일도 모두에게 간다」로 읽는다.
    /// </remarks>
    private string NoRoleText =>
        "역할을 하나도 걸지 않았습니다 — 제한 없이 지금까지와 똑같이 나갑니다. "
        + "하나라도 걸면 그때부터 이 목록이 기준이 됩니다."
        + (_selected is { SupportsEmail: true, EmailFromPush: true }
            ? " 다만 이메일은 지금 한 통도 나가지 않습니다 — 켠 역할이 있어야 보냅니다."
            : string.Empty);

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

    /// <summary>
    /// 「이메일」 체크에 붙는 한 줄. <b>메일이 나가는 길이 두 갈래</b>라 갈라 적는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 켠 뒤에 받는 것이 서로 다르다 — 푸시 경로가 함께 내는 이벤트는 <b>앱
    /// 알림과 같은 글</b>이 오고, 보내는 쪽이 따로 내는 이벤트는 그쪽이 만든
    /// 제 틀의 메일이 온다(가입 신청 · AI 작업 결과). 뭉뚱그려 「이메일로
    /// 보냅니다」라고만 적으면 켜 본 사람이 받은 메일을 보고 <b>다른 설정이
    /// 걸린 줄 안다.</b>
    /// </para>
    /// <para>
    /// <b>푸시와 기본값이 반대라는 것도 여기서 말한다.</b> 역할을 안 걸면
    /// 푸시는 「지금 그대로」 나가지만 메일은 <b>한 통도 안 나간다</b> — 이
    /// 화면에서 가장 틀리기 쉬운 자리다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 왼쪽 카드의 색갈래. <b>왼쪽 굵은 선 하나로 지금 상태를 말한다</b> —
    /// 「정책 밖」·「꺼짐」은 딱지로도 적지만 「제한 없음」은 적을 자리가 없어
    /// 받는 설정 글자에 묻힌다.
    /// </summary>
    /// <remarks>
    /// 색만으로 가르지 않는다 — 카드 아래 줄에 <see cref="NotifyEventDto.ChannelSummary"/>
    /// 가 늘 글자로 서 있다(색각 이상이 있어도 읽힌다).
    /// </remarks>
    private static string CardTone(NotifyEventDto e) => e switch
    {
        { Governed: false } => "off",
        { IsActive: false } => "warn",
        { Unrestricted: true } => "free",
        _ => "on",
    };

    /// <summary>닿나 못 닿나. 글자 하나가 체크 표시보다 읽기 쉽다.</summary>
    private static string Mark(bool reaches) => reaches ? "○" : "—";

    /// <summary>시각 한 줄. <b>받은 값은 UTC 라</b> 보여 주기 직전에 옮긴다.</summary>
    private static string Stamp(DateTime? utc) => utc.Kst("yyyy-MM-dd HH:mm");

    protected override async Task OnInitializedAsync()
    {
        // 서버도 같은 표를 보지만, 체크 칸은 **누르는 즉시** 값이 바뀌므로
        // 화면에서도 가려야 한다(`_canEdit` 머리말). 경로를 꺼내는 규칙은
        // `BasePage.Can` 한 곳에 있다 — 두 벌이 되면 감춘 단추와 화면의
        // 판정이 갈린다.
        _canEdit = Can(MenuAction.Update);

        await LoadRolesAsync();
        await ReloadAsync();
    }

    private Task LoadRolesAsync() => LoadAsync(async () =>
    {
        _roles = await Api.GetNotifyRolesAsync();

        // 역할 목록이 새로 들어왔으니 오른쪽 표를 다시 세운다 — 안 세우면
        // 고른 이벤트의 체크가 옛 역할 줄에 걸린 채로 남는다.
        if (_selected is not null) LoadForm(_selected);

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

        // 보고 있던 이벤트를 다시 집는다. 저장 뒤에 목록을 새로 읽으면 같은
        // 코드의 **다른 객체**가 오므로, 그대로 두면 오른쪽이 옛 줄을 가리킨다.
        var keep = _selected?.Code;
        var visible = VisibleEvents;
        _selected = keep is null ? null : visible.FirstOrDefault(e => e.Code == keep);

        // **첫 줄을 우리가 고른다.** 왼쪽이 표였을 때는 `CommGrd` 가 해 주던
        // 일이고(`AutoSelectFirstRow`), 카드로 바꾸면서 그 일을 할 사람이
        // 없어졌다 — 안 하면 화면을 열자마자 오른쪽이 늘 비어 있다.
        _selected ??= visible.FirstOrDefault();

        LoadForm(_selected);

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
    /// 현재 선택된 이벤트가 숨겨졌으면 표시 중인 첫 번째 이벤트로 바꾼다.
    /// </summary>
    private void EnsureSelectedVisible()
    {
        var visible = VisibleEvents;
        if (_selected is null || !visible.Any(e => e.Code == _selected.Code))
        {
            _selected = visible.FirstOrDefault();
            LoadForm(_selected);
        }
    }

    /// <summary>
    /// 이벤트를 골랐다. 고른 것이 풀리면(<c>null</c>) 폼을 비우지 <b>않는다</b> —
    /// 표가 다시 그려질 때 잠깐 풀리는 자리가 있고, 그때 비우면 고치던 체크가 사라진다.
    /// </summary>
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

    /// <summary>
    /// 고른 이벤트를 오른쪽 표로 편다. 역할 전부가 한 줄씩이고, 걸려 있는
    /// 역할만 체크가 켜진다.
    /// </summary>
    /// <remarks>
    /// <b>꺼 둔 정책 줄(<c>IsActive = false</c>)은 안 켠 것으로 본다.</b>
    /// 화면에 체크 칸이 둘뿐이라 「켜져 있지만 쉬는 줄」을 그릴 자리가 없고,
    /// 서버도 켜진 줄만 세어 「제한 없음」을 판정한다 — 그릴 수 없는 상태를
    /// 들고 있으면 저장할 때 조용히 사라져 화면과 표가 어긋난다.
    /// </remarks>
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

                if (!asked) return;
            }
        }

        _isActive = value;
        await SavePolicyAsync();
    }

    /// <summary>
    /// 체크박스 변경 시 곧바로 정책을 저장한다.
    /// </summary>
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
