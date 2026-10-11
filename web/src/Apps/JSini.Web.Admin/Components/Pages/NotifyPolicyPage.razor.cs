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

public partial class NotifyPolicyPage
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private IReadOnlyList<NotifyEventDto> _events = [];

    private IReadOnlyList<NotifyRoleDto> _roles = [];

    /// <summary>고른 이벤트. 왼쪽 표와 오른쪽 판이 이 값을 나눠 본다.</summary>
    private NotifyEventDto? _selected;

    /// <summary>오른쪽 표가 그리는 줄들. 역할 전부가 한 줄씩이다.</summary>
    private List<NotifyPolicyRow> _rows = [];

    /// <summary>고치고 있는 「이 이벤트를 쓴다」. 고른 줄을 직접 안 건드린다.</summary>
    private bool _isActive = true;

    private string? _keyword;

    private bool _activeOnly;

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

    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.On(_activeOnly, "쓰는 것만"));

    private string EventHint => $"{_events.Count}건";

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
        _events = await Api.GetNotifyEventsAsync(_keyword, _activeOnly);

        // 보고 있던 이벤트를 다시 집는다. 저장 뒤에 목록을 새로 읽으면 같은
        // 코드의 **다른 객체**가 오므로, 그대로 두면 오른쪽이 옛 줄을 가리킨다.
        var keep = _selected?.Code;
        _selected = keep is null ? null : _events.FirstOrDefault(e => e.Code == keep);

        if (_selected is not null) LoadForm(_selected);

        return _events.Count;
    }, "등록된 알림 이벤트가 없습니다.", "알림 이벤트를 읽지 못했습니다");

    private Task ResetAsync()
    {
        _keyword = null;
        _activeOnly = false;
        return ReloadAsync();
    }

    /// <summary>
    /// 이벤트를 골랐다. 고른 것이 풀리면(<c>null</c>) 폼을 비우지 <b>않는다</b> —
    /// 표가 다시 그려질 때 잠깐 풀리는 자리가 있고, 그때 비우면 고치던 체크가 사라진다.
    /// </summary>
    private Task SelectAsync(NotifyEventDto? row)
    {
        if (row is null) return Task.CompletedTask;

        _selected = row;
        LoadForm(row);
        return Task.CompletedTask;
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
        if (row is null) return;

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

    /// <summary>
    /// 저장. <b>알림을 멎게 하는 저장은 묻고 한다</b> — 이벤트를 끄거나
    /// 역할을 전부 떼는 것은 「누구에게도 안 간다」로 가는 길이고, 그 결과는
    /// 아무 화면에도 안 보여서 몇 주 뒤에야 드러난다.
    /// </summary>
    private async Task ConfirmSaveAsync()
    {
        if (_selected is null || _confirm is null) return;

        var target = _selected;

        if (!target.Governed)
        {
            Say("이 이벤트는 설정이 걸리지 않습니다. 저장해도 발송은 지금 그대로입니다.", NoticeTone.Warning);
            return;
        }

        if (!_isActive)
        {
            var asked = await _confirm.AskAsync(
                $"「{target.Name}」 알림을 끕니다. 역할과 상관없이 아무에게도 나가지 않습니다.",
                confirmText: "끄기",
                confirmStyle: DevExpress.Blazor.ButtonRenderStyle.Danger);

            if (!asked) return;
        }
        else if (NoRolePicked && !target.Unrestricted)
        {
            // 걸려 있던 역할을 전부 떼는 저장이다. **「아무도 안 받는다」가
            // 아니라 「제한 없음」으로 되돌아간다** — 그 둘을 헷갈리면 알림을
            // 끄려다 오히려 전원에게 열어 두게 된다.
            var asked = await _confirm.AskAsync(
                $"「{target.Name}」 에 걸린 역할을 모두 뗍니다. "
                + "막는 것이 아니라 제한 없는 상태로 돌아가, 보내는 쪽이 정한 대상에게 그대로 나갑니다.",
                confirmText: "떼기",
                confirmStyle: DevExpress.Blazor.ButtonRenderStyle.Secondary);

            if (!asked) return;
        }

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

        if (await RunAsync(() => Api.SaveNotifyPolicyAsync(target.Code, request),
                "알림 설정을 저장했습니다.", "알림 설정을 저장하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>
    /// 받는 사람을 띄운다. <b>창을 먼저 열고 읽는다</b> — 읽는 동안 아무 일도
    /// 일어나지 않으면 눌리지 않은 줄 알고 다시 누른다.
    /// </summary>
    private async Task OpenRecipientsAsync()
    {
        if (_selected is null) return;

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
}
