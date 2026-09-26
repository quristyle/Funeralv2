using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Layout;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class BirthdayList
{
    [Inject] private BirthdayClient Api { get; set; } = default!;
    [Inject] private OrgOptions Org { get; set; } = default!;
    [Inject] private UserFaceClient Faces { get; set; } = default!;

    private static readonly int[] Months = [.. Enumerable.Range(1, 12)];

    private int? _month;
    private string? _companyId;
    private string? _departmentId;

    private IReadOnlyList<OrgCompany> _companies = [];
    private IReadOnlyList<OrgDepartment> _allDepartments = [];

    private IReadOnlyList<BirthdayMonthStat> _stats = [];
    private IReadOnlyList<BirthdayToday> _today = [];
    private IReadOnlyList<BirthdayMessage> _messages = [];
    private IReadOnlyList<BirthdayPerson> _people = [];

    /// <summary>알림 창이 열려 있는가.</summary>
    private bool _notifying;

    /// <summary>알림 창이 보낼 사람들. 창을 열 때 채운다.</summary>
    private IReadOnlyList<NotifyRecipient> _notifyTargets = [];

    /// <summary>지금 보고 있는 달. 안 고르면 오늘이 속한 달이다.</summary>
    private int CurrentMonth => _month ?? DateTime.Today.Month;

    /// <summary>
    /// 접힌 조회부의 머리줄에 적을 글 — <b>지금 무엇으로 걸러 본 목록인가</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 고르지 않은 칸을 빼 버리지 않고 「전체」로 적는다. 빼면 「개발팀 · 9 월」
    /// 이 되는데, 그것만 보고는 <b>회사를 안 고른 것인지 그 칸이 없는 것인지</b>
    /// 알 수가 없다.
    /// </para>
    /// <para>
    /// 아이디가 아니라 이름으로 적는다. 목록을 아직 못 읽었으면(소속 조회가
    /// 실패했을 때) 「전체」로 떨어진다 — 아이디를 그대로 적어 두면 사람이
    /// 읽을 수 없는 글자가 머리줄에 남는다.
    /// </para>
    /// </remarks>
    private string ConditionSummary
    {
        get
        {
            var company = _companies.FirstOrDefault(c =>
                string.Equals(c.Id, _companyId, StringComparison.Ordinal))?.Name ?? "전체";

            var department = _allDepartments.FirstOrDefault(d =>
                string.Equals(d.Id, _departmentId, StringComparison.Ordinal))?.Name ?? "전체";

            return $"{company} · {department} · {CurrentMonth} 월";
        }
    }

    /// <summary>알림 창의 첫 제목. 사람이 고쳐 쓸 수 있다.</summary>
    private string NotifySubject => _notifyTargets.Count == 1
        ? $"{_notifyTargets[0].Name} 님, 생일 축하합니다"
        : "생일 축하합니다";

    /// <summary>알림 창의 첫 본문.</summary>
    private static string NotifyBody => "오늘 생일을 맞으신 것을 축하드립니다. 좋은 하루 보내세요!";

    /// <summary>고른 회사의 부서만. 회사를 안 골랐으면 전부.</summary>
    private IReadOnlyList<OrgDepartment> Departments =>
        string.IsNullOrEmpty(_companyId)
            ? _allDepartments
            : [.. _allDepartments.Where(d => string.Equals(d.CompanyId, _companyId, StringComparison.Ordinal))];

    /// <summary>
    /// 보내기 전 문구. 사람마다 따로 들고 있어야 한다 — 하나로 두면 한 칸에 쓴
    /// 글이 다른 사람 칸에도 나타난다.
    /// </summary>
    private readonly Dictionary<string, string> _drafts = new(StringComparer.Ordinal);

    protected override async Task OnInitializedAsync()
    {
        await LoadOrgAsync();
        await ReloadAsync();
    }

    /// <summary>
    /// 소속 목록. 실패해도 화면을 죽이지 않는다 — 소속 드롭다운이 안 차는 것과
    /// 생일 목록을 못 보는 것은 다른 일이다.
    /// </summary>
    private async Task LoadOrgAsync()
    {
        try
        {
            var companies = Org.GetCompaniesAsync();
            var departments = Org.GetDepartmentsAsync();

            await Task.WhenAll(companies, departments);

            _companies = [.. companies.Result.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.Ordinal)];
            _allDepartments = departments.Result;
        }
        catch (ApiException)
        {
            _companies = [];
            _allDepartments = [];
        }
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 넷을 나란히. 서로 기다릴 이유가 없다 — 차례로 부르면 왕복이 넷 쌓인다.
        var stats = Api.GetStatsAsync(_companyId, _departmentId);
        var today = Api.GetTodayAsync(_companyId, _departmentId);
        var messages = Api.GetTodayMessagesAsync();
        var people = Api.GetMonthAsync(_month, _companyId, _departmentId);

        await Task.WhenAll(stats, today, messages, people);

        // 통계가 비어 오면 열두 칸을 0 으로 채운다. 타일이 아예 없으면
        // 「집계가 없다」와 「생일자가 없다」를 구분할 수 없다.
        _stats = stats.Result.Count > 0
            ? [.. stats.Result.OrderBy(s => s.Month)]
            : [.. Months.Select(m => new BirthdayMonthStat { Month = m })];

        _today = today.Result;
        _messages = messages.Result;
        _people = people.Result;

        // 얼굴은 목록을 받은 **뒤**에 묻는다. 위 넷과 나란히 걸 수 없다 —
        // 누구의 얼굴이 필요한지가 그 응답 안에 있다.
        //
        // 이것 때문에 조회가 실패하지는 않는다(`EnsureAsync` 가 삼킨다).
        // 두 목록의 아이디가 겹치지만 그쪽이 걸러 준다.
        await Faces.EnsureAsync(
            _people.Select(p => p.SubjectId).Concat(_today.Select(t => t.SubjectId)));

        return _people.Count;
    }, "그 달에 생일자가 없습니다.", "생일 목록을 읽지 못했습니다");

    /// <summary>
    /// 알림 창을 연다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>축하 대상이 아닌 사람은 뺀다.</b> 퇴사자·비대상이 그것인데, 표에서
    /// 「보내기」가 잠겨 있는 사람에게 알림만 나가면 말이 어긋난다.
    /// </para>
    /// <para>
    /// 얼굴도 함께 실어 보낸다. 창이 스스로 물으면 방금 물어본 아이디를
    /// 한 번 더 묻게 된다.
    /// </para>
    /// </remarks>
    private void OpenNotify(IEnumerable<BirthdayPerson> people)
    {
        _notifyTargets =
        [
            .. people
                .Where(p => p.IsCelebrated && !string.IsNullOrWhiteSpace(p.SubjectId))
                .Select(p => new NotifyRecipient(p.SubjectId, p.Name, Faces.PhotoOf(p.SubjectId)))
        ];

        if (_notifyTargets.Count == 0)
        {
            Say("알림을 보낼 대상이 없습니다.", NoticeTone.Warning);
            return;
        }

        _notifying = true;
    }

    private Task OnCompanyChangedAsync(string? companyId)
    {
        _companyId = companyId;

        // 회사가 바뀌면 부서 선택은 무효다. 남겨 두면 다른 회사의 부서로
        // 걸러 아무도 나오지 않는다.
        _departmentId = null;

        return ReloadAsync();
    }

    private Task PickMonthAsync(int month)
    {
        _month = month;
        return ReloadAsync();
    }

    private string GetDraft(string id) => _drafts.GetValueOrDefault(id, string.Empty);

    private void SetDraft(string id, string value) => _drafts[id] = value;

    private async Task SendAsync(BirthdayPerson person)
    {
        var text = GetDraft(person.SubjectId).Trim();

        if (text.Length == 0)
        {
            Say("보낼 문구를 적으십시오.", NoticeTone.Warning);
            return;
        }

        if (await RunAsync(() => Api.SendAsync(person.SubjectId, text),
                $"{person.Name} 님에게 축하를 보냈습니다.", "축하를 보내지 못했습니다"))
        {
            // 보낸 문구는 지운다. 남겨 두면 같은 글을 두 번 보내기 쉽다.
            _drafts.Remove(person.SubjectId);

            // 오늘의 축하와 축하 수가 달라졌다. 다시 읽지 않으면 방금 보낸 것이
            // 화면에 없어 사용자가 한 번 더 보낸다.
            await ReloadAsync();
        }
    }
}
