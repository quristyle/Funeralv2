using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

/// <summary>
/// 편집 중인 배치의 칸들.
/// </summary>
/// <remarks>
/// <para>
/// <b>고른 배치(<c>ReportMailScheduleDto</c>)를 직접 고치지 않는다.</b> 그 줄은
/// 위의 목록 표가 그리고 있는 바로 그 객체라, 칸을 건드리는 순간 저장도 안 한
/// 값이 표에 적힌다. 「고치다 말고 다른 배치를 눌렀다」가 되돌릴 수 없게 된다.
/// </para>
/// <para>
/// 요일만 글자로 든다 — <c>DxComboBox</c> 의 값 칸이 글자라서다. 숫자로 두면
/// 고르개가 값을 못 맞춰 늘 비어 보인다.
/// </para>
/// </remarks>
internal sealed class ReportMailForm
{
    /// <summary>고치는 중인 배치. 비어 있으면 새로 만드는 중이다</summary>
    public string? Id { get; set; }

    public bool IsNew => string.IsNullOrEmpty(Id);

    public string? Name { get; set; }

    public string Frequency { get; set; } = ReportMailFrequency.Weekly;

    /// <summary>요일. 고르개가 글자로 다룬다("1" ~ "7")</summary>
    public string DayOfWeekText { get; set; } = "1";

    public int DayOfMonth { get; set; } = 1;

    public int Hour { get; set; } = 8;

    public int Minute { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Remark { get; set; }

    /// <summary>고른 배치를 폼으로. 주기에 안 쓰는 칸은 기본값으로 둔다.</summary>
    public static ReportMailForm From(ReportMailScheduleDto s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Frequency = s.Frequency,
        DayOfWeekText = (s.DayOfWeek ?? 1).ToString(),
        DayOfMonth = s.DayOfMonth ?? 1,
        Hour = s.SendHourKst,
        Minute = s.SendMinuteKst,
        IsActive = s.IsActive,
        Remark = s.Remark,
    };

    /// <summary>보낼 꼴로. 고른 보고서·역할은 화면이 따로 담아 준다.</summary>
    public SaveReportMailScheduleDto ToRequest(
        IEnumerable<string> reportKeys, IEnumerable<string> roleIds) => new()
        {
            Name = Name?.Trim(),
            Frequency = Frequency,
            DayOfWeek = Frequency == ReportMailFrequency.Weekly
                ? int.TryParse(DayOfWeekText, out var dow) ? dow : 1
                : null,
            DayOfMonth = Frequency == ReportMailFrequency.Monthly ? DayOfMonth : null,
            SendHourKst = Hour,
            SendMinuteKst = Minute,
            IsActive = IsActive,
            Remark = Remark,
            ReportKeys = [.. reportKeys],
            RoleIds = [.. roleIds],
        };

    /// <summary>
    /// 서버까지 가지 않고 막을 수 있는 것. <b>서버도 같은 것을 막는다</b> —
    /// 여기 것은 왕복을 아끼는 것이지 유일한 문이 아니다.
    /// </summary>
    public string? Problem(int reportCount, int roleCount)
    {
        if (string.IsNullOrWhiteSpace(Name)) return "배치 이름을 적으십시오.";
        if (reportCount == 0) return "왼쪽에서 보낼 보고서를 하나 이상 고르십시오.";
        if (roleCount == 0) return "받을 권한 역할을 하나 이상 고르십시오.";

        return null;
    }
}

public partial class ReportMailList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private IReadOnlyList<ReportMailScheduleDto> _schedules = [];
    private IReadOnlyList<ReportCatalogItemDto> _catalog = [];
    private IReadOnlyList<RoleDto> _roles = [];

    /// <summary>고른 배치. 표와 오른쪽 폼이 이 값을 나눠 본다</summary>
    private ReportMailScheduleDto? _selected;

    private ReportMailForm _form = new();

    /// <summary>체크한 보고서들. <b>목록과 같은 객체여야</b> 체크가 남는다</summary>
    private IReadOnlyList<ReportCatalogItemDto> _pickedReports = [];

    /// <summary>체크한 역할들</summary>
    private IReadOnlyList<RoleDto> _pickedRoles = [];

    private string? _keyword;
    private bool _activeOnly;

    private bool _showRecipients;
    private ReportMailRecipientsDto? _recipients;

    private ConfirmDialog? _confirm;

    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.On(_activeOnly, "쓰는 것만"));

    /// <summary>왼쪽 판 머리의 곁줄 — 몇 개 중 몇 개를 골랐나.</summary>
    private string ReportHint => $"{_pickedReports.Count} / {_catalog.Count} 선택";

    /// <summary>받는 사람 창에서 주소가 없는 사람을 짚는 줄.</summary>
    private string MissingEmailText => _recipients is null
        ? string.Empty
        : $"{_recipients.Recipients.Count}명 중 {_recipients.DeliverableCount}명에게만 갑니다 — "
          + "나머지는 계정에 이메일이 없습니다. 「계정 관리」에서 넣으십시오.";

    /// <summary>
    /// 시각 한 줄. <b>받은 값은 UTC 라</b> 보여 주기 직전에 한 번만 옮긴다.
    /// </summary>
    private static string Stamp(DateTime? utc) => utc.Kst("MM-dd HH:mm");

    protected override async Task OnInitializedAsync()
    {
        await LoadCatalogAsync();
        await LoadRolesAsync();
        await ReloadAsync();
    }

    private Task LoadCatalogAsync() => LoadAsync(async () =>
    {
        _catalog = await Api.GetReportCatalogAsync();

        // 목록이 새로 들어왔으므로 체크도 그 객체들로 다시 맞춘다 — 안 맞추면
        // 고른 줄이 「같은 열쇠의 다른 객체」가 되어 체크가 조용히 풀린다.
        SyncPickedReports(_selected?.ReportKeys ?? []);
        return _catalog.Count;
    }, "고를 수 있는 보고서가 없습니다.", "보고서 목록을 읽지 못했습니다");

    private Task LoadRolesAsync() => LoadAsync(async () =>
    {
        _roles = await Api.GetRolesAsync();
        SyncPickedRoles(_selected?.RoleIds ?? []);
        return _roles.Count;
    }, "등록된 역할이 없습니다.", "역할 목록을 읽지 못했습니다");

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _schedules = await Api.GetReportMailSchedulesAsync(_keyword, _activeOnly);

        // 고르고 있던 배치를 다시 집는다. 저장 뒤에 목록을 새로 읽으면 같은
        // 식별자의 **다른 객체**가 오므로, 그대로 두면 오른쪽이 옛 줄을 가리킨다.
        var keep = _selected?.Id;
        _selected = keep is null ? null : _schedules.FirstOrDefault(s => s.Id == keep);

        if (_selected is not null)
        {
            LoadForm(_selected);
        }

        return _schedules.Count;
    }, "등록된 배치가 없습니다.", "배치 목록을 읽지 못했습니다");

    private Task ResetAsync()
    {
        _keyword = null;
        _activeOnly = false;
        return ReloadAsync();
    }

    /// <summary>
    /// 배치를 골랐다. 고른 것이 풀리면(<c>null</c>) 폼을 비우지 <b>않는다</b> —
    /// 새로 만드는 중일 수 있고, 그때 비우면 적던 값이 사라진다.
    /// </summary>
    private Task SelectAsync(ReportMailScheduleDto? schedule)
    {
        if (schedule is null) return Task.CompletedTask;

        _selected = schedule;
        LoadForm(schedule);
        return Task.CompletedTask;
    }

    private void LoadForm(ReportMailScheduleDto schedule)
    {
        _form = ReportMailForm.From(schedule);
        SyncPickedReports(schedule.ReportKeys);
        SyncPickedRoles(schedule.RoleIds);
    }

    /// <summary>「배치 등록」 — 빈 폼으로 돌아간다.</summary>
    private void StartNew()
    {
        _selected = null;
        _form = new ReportMailForm();
        _pickedReports = [];
        _pickedRoles = [];
    }

    /// <summary>
    /// 열쇠 목록을 <b>지금 화면에 실려 있는 객체</b>로 바꾼다.
    /// </summary>
    /// <remarks>
    /// DevExpress 의 체크는 객체 동일성으로 맞춘다. 같은 열쇠라도 다른 인스턴스를
    /// 주면 체크가 하나도 안 걸린다 — 저장하고 목록을 다시 읽을 때마다 고른
    /// 보고서가 통째로 풀려 보이는 종류의 증상이다.
    /// </remarks>
    private void SyncPickedReports(IReadOnlyCollection<string> keys) =>
        _pickedReports = [.. _catalog.Where(c => keys.Contains(c.RouteKey))];

    private void SyncPickedRoles(IReadOnlyCollection<string> ids) =>
        _pickedRoles = [.. _roles.Where(r => ids.Contains(r.Id))];

    private Task OnReportsPickedAsync(IReadOnlyList<ReportCatalogItemDto> picked)
    {
        _pickedReports = picked;
        return Task.CompletedTask;
    }

    private Task OnRolesPickedAsync(IReadOnlyList<RoleDto> picked)
    {
        _pickedRoles = picked;
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (_form.Problem(_pickedReports.Count, _pickedRoles.Count) is { } problem)
        {
            Say(problem, NoticeTone.Warning);
            return;
        }

        var request = _form.ToRequest(
            _pickedReports.Select(r => r.RouteKey),
            _pickedRoles.Select(r => r.Id));

        ReportMailScheduleDto? saved = null;

        var ok = _form.Id is { } id
            ? await RunAsync(async () => saved = await Api.UpdateReportMailScheduleAsync(id, request),
                "배치를 고쳤습니다.", "배치를 고치지 못했습니다")
            : await RunAsync(async () => saved = await Api.CreateReportMailScheduleAsync(request),
                "배치를 등록했습니다.", "배치를 등록하지 못했습니다");

        if (!ok) return;

        // 방금 저장한 것을 고른 채로 둔다 — 등록 직후에 「받는 사람」을
        // 눌러 보는 것이 가장 흔한 다음 동작이다.
        _selected = saved;
        await ReloadAsync();
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_selected is null || _confirm is null) return;

        var target = _selected;

        if (!await _confirm.AskAsync($"「{target.Name}」 배치를 지웁니다. 이 주기 발송이 멈춥니다."))
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteReportMailScheduleAsync(target.Id),
                "배치를 지웠습니다.", "배치를 지우지 못했습니다"))
        {
            StartNew();
            await ReloadAsync();
        }
    }

    /// <summary>
    /// 지금 한 번 보낸다. <b>묻고 보낸다</b> — 되돌릴 수 없고 받는 쪽의
    /// 메일함이 울린다.
    /// </summary>
    private async Task ConfirmSendAsync()
    {
        if (_selected is null || _confirm is null) return;

        var target = _selected;

        var asked = await _confirm.AskAsync(
            $"「{target.Name}」 을 지금 보냅니다. 받는 역할: {string.Join(" · ", target.RoleNames)}.",
            confirmText: "보내기",
            confirmStyle: DevExpress.Blazor.ButtonRenderStyle.Primary);

        if (!asked) return;

        if (await RunAsync(() => Api.SendReportMailNowAsync(target.Id),
                "보냈습니다.", "보내지 못했습니다"))
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

        _recipients = null;
        _showRecipients = true;

        try
        {
            _recipients = await Api.GetReportMailRecipientsAsync(_selected.Id);
        }
        catch (ApiException ex)
        {
            _showRecipients = false;
            Say($"받는 사람을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }
}
