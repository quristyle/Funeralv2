using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

/// <summary>일정표가 메일 화면에 넘기는 조회 조건. <c>Stamp</c> 가 다르면 일정표에서 다시 누른 것이라 본문을 새로 만든다.</summary>
public sealed record ScheduleMailScope(
    int? ProjectRid, string? ProjectName, string? CompleteState, IReadOnlyList<string> ScheduleTypes, Guid Stamp)
{
    public bool AllProjects => ProjectRid is null;

    /// <summary>새로고침으로 넘겨받은 것이 없을 때 — 내가 참여한 프로젝트 전부, 조건 없음.</summary>
    public static readonly ScheduleMailScope Default = new(null, null, null, [], Guid.Empty);
}

public partial class ScheduleMailPage : IDisposable
{
    [Inject] private WbsClient Api { get; set; } = default!;
    [Inject] private CommonCodes Codes { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private ScheduleMailClient Mails { get; set; } = default!;
    [Inject] private MailTemplateClient MailTemplates { get; set; } = default!;
    [Inject] private UserCard Cards { get; set; } = default!;
    [Inject] private PortalTabs Tabs { get; set; } = default!;
    [Inject] private ScreenState State { get; set; } = default!;

    public const string Href = "/projmng/proj/scheduler/mail";
    private const string SchedulerHref = "/projmng/proj/scheduler";
    private const string TabTitle = "일정 메일 발송";

    /// <summary>일정표가 조건을 맡기는 열쇠. 질의 문자열로 넘기지 않는 것은 탭이 주소에서 질의를 떼고 되돌아오기 때문이다.</summary>
    public const string ScopeKey = Href + ":scope";
    private const string DraftKey = Href + ":draft";

    /// <summary>템플릿 고르개의 「기본 틀」. DB 번호는 1부터라 0 과 안 겹친다.</summary>
    private const long DefaultTemplateId = 0;

    /// <summary>한 번에 실을 수 있는 기간. 넘으면 타임라인이 메일 한 통으로 감당 못 할 만큼 길어진다.</summary>
    private const int MaxMailDays = 366;

    /// <summary>탭을 옮겼다 와도 쓰던 그대로 — 보내거나 닫으면 버린다.</summary>
    private sealed class Draft
    {
        public required ScheduleMailScope Scope { get; init; }
        public string To = string.Empty;
        public string Cc = string.Empty;
        public bool MyMailLooked;
        public string Subject = string.Empty;
        public string SubjectAuto = string.Empty;
        public string? Message;
        public string Body = string.Empty;
        public int Count;
        public bool Edited;
        public DateOnly From;
        public DateOnly Until;
        public (DateOnly From, DateOnly To) Loaded;
        public IReadOnlyList<ScheduleMailItem> Items = [];
        public IReadOnlyList<MailTemplateDto> Templates = [];
        public long TplId = DefaultTemplateId;
        public bool Ready;
    }

    private Draft _d = new() { Scope = ScheduleMailScope.Default };

    /// <summary>보냈거나 닫았다 — 떠날 때 맡기지 않는다.</summary>
    private bool _done;

    private bool _sending;
    private bool _loading;
    private MailHtmlEditor? _editor;
    private ConfirmDialog? _confirm;

    private Dictionary<int, string> _projects = new();

    private MailTemplateDto? MailTemplate => _d.Templates.FirstOrDefault(t => t.TplId == _d.TplId);

    private string? CurrentUserName =>
        string.IsNullOrWhiteSpace(Me?.DisplayName)
            ? (string.IsNullOrWhiteSpace(Me?.Username) ? null : Me.Username.Trim())
            : Me.DisplayName.Trim();

    private string ScopeText => _d.Scope.AllProjects
        ? "내가 참여한 프로젝트 전체"
        : _d.Scope.ProjectName ?? $"프로젝트 {_d.Scope.ProjectRid}";

    protected override void OnInitialized()
    {
        Tabs.Open(Href, TabTitle, standalone: true);

        var scope = State.Get<ScheduleMailScope>(ScopeKey) ?? ScheduleMailScope.Default;
        var kept = State.Get<Draft>(DraftKey);

        if (kept is not null && kept.Ready && kept.Scope.Stamp == scope.Stamp)
        {
            _d = kept;
            return;
        }

        // 일정표에서 다시 눌렀다 — 조건이 바뀌었으니 본문은 새로 만들되 적어 둔 받는 사람·인사말은 살린다.
        _d = new Draft { Scope = scope };

        if (kept is not null)
        {
            _d.To = kept.To;
            _d.Cc = kept.Cc;
            _d.MyMailLooked = kept.MyMailLooked;
            _d.Message = kept.Message;
            _d.From = kept.From;
            _d.Until = kept.Until;
            _d.TplId = kept.TplId;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        if (_d.Ready)
        {
            return;
        }

        if (_d.From == default)
        {
            (_d.From, _d.Until) = ScheduleMailBody.Window(AppTime.Today);
        }

        if (_d.Scope.AllProjects)
        {
            await LoadProjectsAsync();
        }

        await LoadTemplatesAsync();

        if (!_d.MyMailLooked && !string.IsNullOrWhiteSpace(Me?.Username))
        {
            _d.MyMailLooked = true;
            var card = await Cards.LoadAsync(Me.Username.Trim());

            if (!string.IsNullOrWhiteSpace(card?.Email) && string.IsNullOrWhiteSpace(_d.Cc))
            {
                _d.Cc = card.Email.Trim();
            }
        }

        if (await LoadItemsAsync())
        {
            _d.SubjectAuto = string.Empty;
            RebuildBody();
        }

        _d.Ready = true;
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            var list = await Codes.GetAsync("projlist");
            var map = new Dictionary<int, string>();

            foreach (var item in list)
            {
                if (int.TryParse(item.Code, out var rid))
                {
                    map.TryAdd(rid, item.Name);
                }
            }

            _projects = map;
        }
        catch
        {
            // 이름을 못 읽어도 번호로 적는다.
        }
    }

    private string Title(WbsItemDto w)
    {
        var title = string.IsNullOrWhiteSpace(w.ProcNm) ? "(제목 없음)" : w.ProcNm;

        if (!_d.Scope.AllProjects || w.PrjRid is not { } rid)
        {
            return title;
        }

        var prj = _projects.TryGetValue(rid, out var name) ? name : $"프로젝트 {rid}";
        return $"[{prj}] {title}";
    }

    /// <summary>템플릿 목록. 못 읽어도 화면은 연다 — 기본 틀로는 보낼 수 있다.</summary>
    private async Task LoadTemplatesAsync()
    {
        var basic = new MailTemplateDto { TplId = DefaultTemplateId, TplNm = "기본 틀" };

        try
        {
            var rows = await MailTemplates.ListAsync(MailTemplateDto.KindSchedule, usableOnly: true);
            _d.Templates = [basic, .. rows];
        }
        catch (Exception ex)
        {
            _d.Templates = [basic];
            Say($"메일 템플릿을 읽지 못해 기본 틀을 씁니다 — {ex.Message}", NoticeTone.Warning);
        }

        if (MailTemplate is null)
        {
            _d.TplId = DefaultTemplateId;
        }
    }

    /// <summary>고른 기간의 일정을 읽는다. 기간이 틀렸으면 말하고 거짓을 준다.</summary>
    private async Task<bool> LoadItemsAsync()
    {
        if (_d.Until < _d.From)
        {
            Say("기간의 끝이 시작보다 앞입니다.", NoticeTone.Warning);
            return false;
        }

        if (_d.Until.DayNumber - _d.From.DayNumber + 1 > MaxMailDays)
        {
            Say($"기간은 {MaxMailDays}일까지 고를 수 있습니다.", NoticeTone.Warning);
            return false;
        }

        _loading = true;

        try
        {
            var s = _d.Scope;
            var rows = await Api.ListAsync(s.ProjectRid, s.CompleteState, s.ScheduleTypes, _d.From, _d.Until, mine: s.AllProjects);

            _d.Items = [.. rows
                .Where(w => w.PlanSdt is not null || w.PlanEdt is not null)
                .Select(w =>
                {
                    var start = w.PlanSdt ?? w.PlanEdt!.Value;
                    var end = w.PlanEdt ?? start;

                    return new ScheduleMailItem(start, end < start ? start : end, Title(w));
                })];

            _d.Loaded = (_d.From, _d.Until);
            return true;
        }
        catch (Exception ex)
        {
            Say($"메일에 넣을 일정을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return false;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>「다시 가져오기」. 직접 고친 본문이 있으면 덮기 전에 묻는다.</summary>
    private async Task ReloadItemsAsync()
    {
        if (!await ConfirmOverwriteAsync())
        {
            return;
        }

        if (await LoadItemsAsync())
        {
            RebuildBody();
            Say($"{_d.From:yyyy.MM.dd} ~ {_d.Until:yyyy.MM.dd} 일정 {_d.Count}건을 다시 가져왔습니다.");
        }
    }

    private async Task PickTemplateAsync(long id)
    {
        if (id == _d.TplId || !await ConfirmOverwriteAsync())
        {
            return;
        }

        _d.TplId = id;
        RebuildBody();
    }

    private async Task RebuildBodyAsync()
    {
        if (await ConfirmOverwriteAsync())
        {
            RebuildBody();
        }
    }

    /// <summary>직접 고친 본문을 새로 만든 것으로 덮어도 되는지.</summary>
    private async Task<bool> ConfirmOverwriteAsync()
    {
        if (!_d.Edited || _confirm is null)
        {
            return true;
        }

        return await _confirm.AskAsync(
            "직접 고친 본문이 새로 만든 본문으로 바뀝니다.\n고친 내용은 사라집니다.",
            "본문 다시 만들기", "다시 만들기", ButtonRenderStyle.Primary);
    }

    private void OnMessageChanged(string? text)
    {
        _d.Message = text;

        // 고친 본문은 인사말 한 글자에 덮지 않는다 — 「본문 다시 만들기」로 반영한다.
        if (!_d.Edited)
        {
            RebuildBody();
        }
    }

    private void OnBodyEdited(string? html)
    {
        _d.Body = html ?? string.Empty;
        _d.Edited = true;
    }

    private ScheduleMailInput MailInput() => new(
        _d.Loaded.From, _d.Loaded.To, AppTime.Today, _d.Items,
        _d.Scope.AllProjects ? null : _d.Scope.ProjectName, CurrentUserName, _d.Message);

    /// <summary>템플릿과 지금 자료로 제목·본문을 새로 만든다.</summary>
    private void RebuildBody()
    {
        var input = MailInput();
        var tpl = MailTemplate;

        _d.Count = ScheduleMailBody.InRange(_d.Items, input.From, input.To).Count();
        _d.Body = ScheduleMailBody.Build(input, tpl?.BodyHtml);
        _d.Edited = false;

        var subject = ScheduleMailBody.Subject(input, tpl?.Subject);

        if (string.IsNullOrWhiteSpace(_d.Subject) || _d.Subject == _d.SubjectAuto)
        {
            _d.Subject = subject;
        }

        _d.SubjectAuto = subject;
    }

    /// <summary>받는 사람 칸의 주소들. 쉼표·세미콜론·줄바꿈 어느 것으로 이어도 된다.</summary>
    private static List<string> SplitMail(string? text) =>
        [.. (text ?? string.Empty)
            .Split([',', ';', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static bool IsMailAddress(string address) =>
        System.Net.Mail.MailAddress.TryCreate(address, out var parsed)
        && string.Equals(parsed.Address, address, StringComparison.OrdinalIgnoreCase);

    private async Task SendAsync()
    {
        if (_sending) return;

        var to = SplitMail(_d.To);
        var cc = SplitMail(_d.Cc);
        var bad = to.Concat(cc).Where(a => !IsMailAddress(a)).ToList();

        if (to.Count == 0)
        {
            Say("받는 사람 메일 주소를 적으십시오.", NoticeTone.Warning);
            return;
        }

        if (bad.Count > 0)
        {
            Say($"주소 꼴이 아닙니다 — {string.Join(" · ", bad.Take(5))}", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_d.Subject))
        {
            Say("메일 제목을 적으십시오.", NoticeTone.Warning);
            return;
        }

        if ((_d.From, _d.Until) != _d.Loaded && _confirm is not null
            && !await _confirm.AskAsync(
                $"기간을 고쳤지만 다시 가져오지 않았습니다.\n본문은 {_d.Loaded.From:yyyy.MM.dd} ~ {_d.Loaded.To:yyyy.MM.dd} 의 일정입니다. 그대로 보낼까요?",
                "기간 확인", "그대로 발송", ButtonRenderStyle.Primary))
        {
            return;
        }

        _sending = true;

        try
        {
            // 편집 칸은 친 것을 모아서 올리므로, 방금 친 몇 자가 아직 안 왔을 수 있다.
            if (_editor is not null)
            {
                _d.Body = await _editor.FlushAsync() ?? _d.Body;
            }

            if (string.IsNullOrWhiteSpace(_d.Body))
            {
                Say("본문이 비어 있습니다.", NoticeTone.Warning);
                return;
            }

            var ok = await RunAsync(
                () => Mails.SendAsync(new ScheduleMailRequest
                {
                    To = string.Join(",", to),
                    Cc = cc.Count == 0 ? null : string.Join(",", cc),
                    Subject = _d.Subject.Trim(),
                    Body = _d.Body,
                }),
                cc.Count == 0 ? "메일을 보냈습니다." : $"메일을 보냈습니다. 참조: {string.Join(", ", cc)}",
                "메일을 보내지 못했습니다");

            if (ok)
            {
                ClosePage();
            }
        }
        finally
        {
            _sending = false;
        }
    }

    /// <summary>이 탭을 닫고 일정표로 돌아간다. 쓰던 것은 버린다.</summary>
    private void ClosePage()
    {
        _done = true;
        State.Clear(DraftKey);
        Tabs.Close(Href);
        Navigation.NavigateTo(SchedulerHref);
    }

    public void Dispose()
    {
        if (!_done && _d.Ready)
        {
            State.Set(DraftKey, _d);
        }
    }

    // ── 사람 찾기 ──────────────────────────────────────────────

    /// <summary>고른 주소를 어느 칸에 더할지. 창 하나를 받는 사람과 참조가 나눠 쓴다.</summary>
    private enum MailPickTarget { To, Cc }

    private bool _picking;
    private MailPickTarget _pickTarget = MailPickTarget.To;
    private string _pickFilter = string.Empty;

    private IReadOnlyList<ScheduleMailPerson> _people = [];
    private bool _peopleLoaded;

    private readonly HashSet<string> _pickedIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>고를 수 있는 사람 — 메일 주소가 있는 재직자. 보낼 수 없는 줄은 고를 까닭이 없다.</summary>
    private IReadOnlyList<ScheduleMailPerson> PickRows =>
        [.. _people
            .Where(p => !string.IsNullOrWhiteSpace(p.Email))
            .Where(p => string.Equals(p.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrWhiteSpace(_pickFilter)
                        || $"{p.UserName} {p.LoginId} {p.Email} {p.DeptName} {p.CompanyName}"
                             .Contains(_pickFilter.Trim(), StringComparison.OrdinalIgnoreCase))];

    /// <summary>찾기 칸에 친 것이 메일 주소 꼴이면 그 주소. 계정이 없는 상대(거래처)를 그 자리에서 넣는 길이다.</summary>
    private string? TypedAddress
    {
        get
        {
            var text = _pickFilter.Trim();
            return text.Length > 0 && IsMailAddress(text) ? text : null;
        }
    }

    private async Task OpenPickerAsync(MailPickTarget target)
    {
        _pickTarget = target;
        _pickFilter = string.Empty;
        _pickedIds.Clear();
        _picking = true;

        if (_peopleLoaded) return;

        try
        {
            _people = await Mails.GetPeopleAsync();
            _peopleLoaded = true;
        }
        catch (Exception ex)
        {
            Say($"사람 목록을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    private void TogglePicked(string loginId, bool on)
    {
        if (on) _pickedIds.Add(loginId);
        else _pickedIds.Remove(loginId);
    }

    /// <summary>고른 사람들의 주소를 칸에 더하고 창을 닫는다.</summary>
    private void ApplyPicked()
    {
        foreach (var person in _people.Where(p => _pickedIds.Contains(p.LoginId)))
        {
            AddPickedAddress(person.Email);
        }

        _picking = false;
    }

    private void AddTypedAddress()
    {
        AddPickedAddress(TypedAddress);
        _pickFilter = string.Empty;
    }

    private void AddPickedAddress(string? address)
    {
        if (_pickTarget == MailPickTarget.To) _d.To = AppendMail(_d.To, address);
        else _d.Cc = AppendMail(_d.Cc, address);
    }

    /// <summary>칸의 주소들 뒤에 하나를 더한다. 이미 있으면 그대로 둔다.</summary>
    private static string AppendMail(string? text, string? address)
    {
        var list = SplitMail(text);
        var add = address?.Trim();

        if (!string.IsNullOrEmpty(add) && !list.Contains(add, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(add);
        }

        return string.Join(", ", list);
    }
}
