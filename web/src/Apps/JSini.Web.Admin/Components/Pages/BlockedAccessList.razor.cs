using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Models;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class BlockedAccessList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    // ── 보기 셋 ─────────────────────────────────────────────
    //
    // **값은 화면 안에서만 쓴다.** 서버로 나가지 않으므로 글자를 바꿔도
    // 조회가 깨지지 않는다 — 기간·사용자·까닭·검색어와 다른 종류의 조건이다.

    private const string ViewByScreen = "screen";
    private const string ViewByUser = "user";
    private const string ViewLog = "log";

    private static readonly SchOption[] ViewOptions =
    [
        new(ViewByScreen, "화면별"),
        new(ViewByUser, "사용자별"),
        new(ViewLog, "기록"),
    ];

    /// <summary>
    /// 까닭 고르개. <b>서버로 나가는 값이라 글자가 아니라 코드다</b> —
    /// <c>BlockedAccessReason</c> 의 상수와 같아야 한다.
    /// </summary>
    private static readonly SchOption[] ReasonOptions =
    [
        new(BlockedAccessReason.Unmanaged, "관리 밖"),
        new(BlockedAccessReason.NoPermission, "권한 없음"),
    ];

    /// <summary>
    /// 기록 목록이 한 번에 받는 최대 줄 수. <b>서버의 값과 같아야 한다</b>
    /// (<c>BlockedAccessEndpoints.MaxTake</c>) — 작으면 「잘렸다」 안내가 안 뜨고,
    /// 크면 안 잘린 목록에 그 안내가 뜬다.
    /// </summary>
    private const int LogCap = 500;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        SchSummary.NameOf(ViewOptions, o => o.Value, o => o.Text, _view),
        SchSummary.NameOf(ReasonOptions, o => o.Value, o => o.Text, _reason),
        SchSummary.NameOf(_users, u => u.UserId, u => u.Who, _userId),
        SchSummary.Or(_keyword));

    private IReadOnlyList<BlockedAccessByScreenDto> _screens = [];
    private IReadOnlyList<BlockedAccessByUserDto> _users = [];
    private IReadOnlyList<BlockedAccessDto> _logs = [];

    private DateTime? _from = AppTime.TodayDate.AddDays(-7);
    private DateTime? _to = AppTime.TodayDate;
    private string _view = ViewByScreen;
    private string? _reason;
    private string? _userId;
    private string? _keyword;

    /// <summary>화면별 표에서 고른 줄.</summary>
    private BlockedAccessByScreenDto? _pickedScreen;

    /// <summary>사용자별 표에서 고른 줄.</summary>
    private BlockedAccessByUserDto? _pickedUser;

    /// <summary>기록 표에서 고른 줄.</summary>
    private BlockedAccessDto? _pickedLog;

    /// <summary>상세 창에서 보여주는 화면 정보.</summary>
    private BlockedAccessByScreenDto? _detailScreen;

    /// <summary>상세 창에서 보여주는 사용자 정보.</summary>
    private BlockedAccessByUserDto? _detailUser;

    /// <summary>상세 창에서 보여주는 단일 기록 정보.</summary>
    private BlockedAccessDto? _detailLog;

    /// <summary>상세 창에 띄울 차단 접근 기록 목록.</summary>
    private IReadOnlyList<BlockedAccessDto> _detailLogs = [];

    /// <summary>상세 창 표시 여부.</summary>
    private bool _detailVisible;

    /// <summary>상세 기록 조회 중 여부.</summary>
    private bool _detailLoading;

    private string DetailTitle =>
        _detailScreen is not null ? $"{_detailScreen.Screen} 차단 상세"
        : _detailUser is not null ? $"{_detailUser.Who} 차단 상세"
        : _detailLog is not null ? "차단 접근 상세"
        : "차단 접근 상세";

    /// <summary>
    /// 고르개에 세울 사람. <b>아이디를 들고 목록에서 되찾는다</b> — 줄 자체를
    /// 들고 있으면 다시 조회했을 때 새로 만들어진 줄과 같지 않아 고르개가 빈다.
    /// </summary>
    private BlockedAccessByUserDto? PickedUser =>
        _userId is null ? null : _users.FirstOrDefault(u => u.UserId == _userId);

    // ── 타일 넷 ─────────────────────────────────────────────
    //
    // **화면별 집계에서 읽는다.** 그쪽이 고른 사람·까닭까지 반영한 값이라,
    // 보기를 무엇으로 두든 지금 보고 있는 것과 숫자가 맞는다.

    private int TotalBlocks => _screens.Sum(s => s.Blocks);

    /// <summary>
    /// 메뉴가 아예 없어 막힌 화면의 수. <b>사람이 손봐야 하는 수다</b> —
    /// 나머지 타일은 상태를 말하고 이 하나는 할 일을 말한다.
    /// </summary>
    private int UnmanagedScreens =>
        _screens.Count(s => s.Reason == BlockedAccessReason.Unmanaged);

    /// <summary>
    /// 사람 수. 한 사람을 고르면 1 이다 — 사람별 집계는 고른 사람과 무관하게
    /// (고르개를 채워야 해서) 전체를 들고 있으므로 그 수를 그대로 쓰면 안 된다.
    /// </summary>
    private int PeopleCount => _userId is null ? _users.Count : 1;

    private string LastBlockText => _screens.Count == 0
        ? "-"
        : _screens.Max(s => s.LastBlockedAt).Kst("MM-dd HH:mm");

    /// <summary>타일 아래에 적을 기간. 사람이 고른 날짜 그대로다.</summary>
    private string PeriodText => SchSummary.Period(_from, _to) ?? "전체 기간";

    /// <summary>
    /// 서버가 잘랐으면 그 한도, 아니면 <c>null</c>. 집계 보기(화면별·사람별)는
    /// 자르지 않으므로 언제나 <c>null</c> 이다.
    /// </summary>
    private int? Truncated => _view == ViewLog && _logs.Count >= LogCap ? LogCap : null;

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>
    /// 다시 읽는다.
    /// </summary>
    /// <remarks>
    /// <b>집계 둘은 보기와 무관하게 늘 읽는다</b> — 타일 넷과 사용자 고르개가
    /// 그것으로 서기 때문이다. 줄이 많은 쪽(기록)만 보고 있을 때 읽는다.
    /// </remarks>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var from = DateOf(_from);
        var to = DateOf(_to);

        _screens = await Api.GetBlockedAccessByScreenAsync(from, to, _userId, _reason, _keyword);
        _users = await Api.GetBlockedAccessByUserAsync(from, to, _reason, _keyword);

        _logs = _view == ViewLog
            ? await Api.GetBlockedAccessLogsAsync(from, to, _userId, _reason, _keyword, LogCap)
            : [];

        // 고른 사람이 이 기간에는 기록이 없을 수 있다. 그때 고르개만 값을
        // 들고 있으면 「전체」로 보이면서 조건은 걸려 있는 상태가 된다.
        if (_userId is not null && _users.All(u => u.UserId != _userId))
        {
            _userId = null;
        }

        return _view switch
        {
            ViewByScreen => _screens.Count,
            ViewByUser => _users.Count,
            _ => _logs.Count,
        };
    }, "이 기간에 막힌 기록이 없습니다.", "차단 접근기록을 읽지 못했습니다");

    private Task Reset()
    {
        _from = AppTime.TodayDate.AddDays(-7);
        _to = AppTime.TodayDate;
        _view = ViewByScreen;
        _reason = null;
        _userId = null;
        _keyword = null;
        _pickedScreen = null;
        _pickedUser = null;
        _pickedLog = null;
        _detailVisible = false;
        _detailScreen = null;
        _detailUser = null;
        _detailLog = null;
        _detailLogs = [];
        return ReloadAsync();
    }

    /// <summary>
    /// 보기를 바꾼다. <b>바로 다시 읽는다</b> — 보기마다 읽어 오는 것이 달라서
    /// (기록은 그때만 받는다) 조회를 다시 누르게 하면 빈 판이 뜬다.
    /// </summary>
    private Task PickViewAsync(string? view)
    {
        _view = string.IsNullOrWhiteSpace(view) ? ViewByScreen : view;
        _detailVisible = false;
        return ReloadAsync();
    }

    private Task PickReasonAsync(string? reason)
    {
        _reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        return ReloadAsync();
    }

    private Task PickUserAsync(BlockedAccessByUserDto? user)
    {
        _userId = user?.UserId;
        return ReloadAsync();
    }

    /// <summary>
    /// 화면별 표에서 항목 카드를 골랐을 때. 상세 창을 띄워 차단된 사용자와 시각 정보를 보여준다.
    /// </summary>
    private async Task OnScreenPickedAsync(BlockedAccessByScreenDto? row)
    {
        _pickedScreen = row;
        if (row is not null)
        {
            await OpenScreenDetailAsync(row);
        }
    }

    /// <summary>
    /// 화면별 차단 항목 카드를 선택했을 때 상세 창을 연다.
    /// 해당 화면에서 차단된 사용자와 시각, 주소 등의 정보를 조회해 표시한다.
    /// </summary>
    private async Task OpenScreenDetailAsync(BlockedAccessByScreenDto row)
    {
        _pickedScreen = row;
        _detailScreen = row;
        _detailUser = null;
        _detailLog = null;
        _detailLogs = [];
        _detailLoading = true;
        _detailVisible = true;
        StateHasChanged();

        try
        {
            var from = DateOf(_from);
            var to = DateOf(_to);
            _detailLogs = await Api.GetBlockedAccessLogsAsync(
                from, to, _userId, _reason, keyword: row.ScreenKey, take: 100);
        }
        catch (Exception)
        {
            _detailLogs = [];
        }
        finally
        {
            _detailLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 사용자별 표에서 항목 카드를 골랐을 때.
    /// </summary>
    private async Task OnUserPickedAsync(BlockedAccessByUserDto? row)
    {
        _pickedUser = row;
        if (row is not null)
        {
            await OpenUserDetailAsync(row);
        }
    }

    /// <summary>
    /// 사용자별 차단 항목 카드를 선택했을 때 상세 창을 연다.
    /// </summary>
    private async Task OpenUserDetailAsync(BlockedAccessByUserDto row)
    {
        _pickedUser = row;
        _detailUser = row;
        _detailScreen = null;
        _detailLog = null;
        _detailLogs = [];
        _detailLoading = true;
        _detailVisible = true;
        StateHasChanged();

        try
        {
            var from = DateOf(_from);
            var to = DateOf(_to);
            _detailLogs = await Api.GetBlockedAccessLogsAsync(
                from, to, userId: row.UserId, reason: _reason, keyword: _keyword, take: 100);
        }
        catch (Exception)
        {
            _detailLogs = [];
        }
        finally
        {
            _detailLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 기록 표에서 항목 카드를 골랐을 때.
    /// </summary>
    private Task OnLogPickedAsync(BlockedAccessDto? row)
    {
        _pickedLog = row;
        if (row is not null)
        {
            OpenLogDetail(row);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 개별 차단 기록 카드를 선택했을 때 상세 정보를 띄운다.
    /// </summary>
    private void OpenLogDetail(BlockedAccessDto row)
    {
        _pickedLog = row;
        _detailLog = row;
        _detailScreen = null;
        _detailUser = null;
        _detailLogs = [];
        _detailLoading = false;
        _detailVisible = true;
    }

    private void CloseDetail()
    {
        _detailVisible = false;
    }

    /// <summary>
    /// 상세 창에서 해당 화면의 전체 기록 보기로 전환한다.
    /// </summary>
    private Task OpenScreenLogFromDetailAsync()
    {
        if (_detailScreen is { } screen)
        {
            _detailVisible = false;
            return OpenScreenLogAsync(screen);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 사용자별 집계에서 그 사람의 기록으로 좁혀 연다.
    /// </summary>
    private Task OpenUserLogAsync(BlockedAccessByUserDto row)
    {
        _pickedUser = row;
        _userId = row.UserId;
        _view = ViewLog;
        return ReloadAsync();
    }

    /// <summary>
    /// 그 화면으로 좁혀 기록을 연다.
    /// </summary>
    /// <remarks>
    /// 화면별 집계는 <b>열쇠로 묶으므로 실제 주소가 사라진다</b>. 「누구의 것을
    /// 보려 했나」에 답하려면 기록 보기로 내려가야 하고, 그 길을 줄에서 바로
    /// 열어 준다. 검색어로 넘기는 값이 열쇠인 것은 서버의 검색이 열쇠·주소·
    /// 경로·화면 클래스를 함께 훑기 때문이다.
    /// </remarks>
    private Task OpenScreenLogAsync(BlockedAccessByScreenDto row)
    {
        _pickedScreen = row;
        _keyword = row.ScreenKey;
        _view = ViewLog;
        return ReloadAsync();
    }

    /// <summary>
    /// 까닭에 입히는 모양. <b>「관리 밖」만 눈에 띄게 둔다</b> — 그쪽이 사람이
    /// 손봐야 하는 줄이고, 둘 다 붉게 칠하면 갈라 둔 뜻이 없어진다.
    /// </summary>
    private static string ReasonCss(string? reason) =>
        reason == BlockedAccessReason.Unmanaged ? "jsini-text-danger" : "jsini-text-muted";

    /// <summary>
    /// 고르개가 주는 <c>DateTime?</c> 을 날짜로 바꾼다.
    /// </summary>
    /// <remarks>
    /// <b>한국 달력 날짜다.</b> 사람이 고른 것이 그것이고, 서버는 이 날짜를
    /// 받아 하루의 경계를 잡는다(docs/utc-time.md).
    /// </remarks>
    private static DateOnly? DateOf(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);
}
