using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class NotificationHistory : IAsyncDisposable
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>밀기 손짓을 대는 브라우저 쪽(<c>js/note-swipe.js</c>)을 부를 때 쓴다.</summary>
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>알림구분 목록. 정본은 공통코드(<c>NOTI_CATEGORY</c>)다.</summary>
    [Inject] private PushCategoryClient Categories { get; set; } = default!;

    /// <summary>여는 김에 하는 읽음 처리가 실패했을 때만 쓴다 — 화면에는 말하지 않는다.</summary>
    [Inject] private ILogger<NotificationHistory> Log { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        SchSummary.NameOf(_categoryOptions, o => o.Value, o => o.Text, _category),
        SchSummary.Or(_keyword.Trim()),
        SchSummary.On(_unreadOnly, "안 읽은 것만"));

    /// <summary>조회 기간. 기본은 최근 한 달이다 — 알림은 지우지 않아 자라기만 하고, 서버에 페이징이 없어 기간이 없으면 전부 받아 온다.</summary>
    private DateTime? _from = AppTime.TodayDate.AddMonths(-1);
    private DateTime? _to = AppTime.TodayDate;

    /// <summary>고른 알림구분. 비면 전체다. <see cref="PushCategoryClient.Unset"/> 이면 구분이 안 붙은 줄만 본다.</summary>
    private string? _category;

    /// <summary>제목 또는 내용에 들어 있는 글자를 찾는다.</summary>
    private string _keyword = string.Empty;

    /// <summary>고르개에 담는 항목들. 「전체」와 「구분 없음」이 함께 들어 있다.</summary>
    private IReadOnlyList<SchOption> _categoryOptions = [new SchOption(null, "전체")];

    /// <summary>코드값 → 이름. 표가 줄마다 부르므로 목록을 받을 때 한 번 만든다.</summary>
    private Func<string?, string> _categoryName = v => v ?? string.Empty;

    /// <summary>안 읽은 것만 보기. 기본이 켜짐이다. 조건은 서버와 화면 양쪽에 걸어, 읽음 처리한 줄이 다시 조회하지 않고도 빠지게 한다.</summary>
    private bool _unreadOnly = true;

    private IReadOnlyList<NotificationDto> _all = [];

    /// <summary>체크한 줄들. 줄 누르기는 「연다」이고 체크는 맨 앞 칸에서만 한다 — 둘을 겹치면 열려던 손짓이 일괄삭제 대상을 늘린다.</summary>
    private IReadOnlyList<NotificationDto> _selectedItems = [];

    /// <summary>일괄삭제를 묻는 창. 한 건짜리 길에는 안 쓴다.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>지금까지 낸 조회의 번호. 마지막으로 낸 것만 표에 앉힌다 — 늦게 온 옛 조건의 목록이 새 조건 아래 앉는 것을 막는다.</summary>
    private int _latest;

    private IReadOnlyList<NotificationDto> Shown =>
        _unreadOnly ? [.. _all.Where(n => !n.IsRead)] : _all;

    // ── 휴대폰에서는 표가 아니라 카드 목록이다 ────────────────────
    // 표의 가로 구름을 없애려고 한 건을 한 장에 세로로 편다.

    /// <summary>휴대폰(≤767px)인가. 첫 그림에는 거짓이다 — 회로가 붙어야 값이 와서 표가 한 번 섰다가 걷힌다.</summary>
    private bool _isPhone;

    private void OnPhoneChanged(bool active) => _isPhone = active;

    /// <summary>휴대폰에서 한 번에 깔 줄 수. 「더보기」가 이만큼씩 늘린다.</summary>
    private const int PhonePage = 20;

    /// <summary>지금까지 깔기로 한 줄 수. 카드 목록엔 쪽나누기가 없어 받은 것을 「더보기」로 나눠 깐다.</summary>
    private int _take = PhonePage;

    /// <summary>휴대폰 카드 목록에 실제로 깔리는 줄.</summary>
    private IReadOnlyList<NotificationDto> PhoneShown
    {
        get
        {
            var shown = Shown;
            return _take < shown.Count ? [.. shown.Take(_take)] : shown;
        }
    }

    /// <summary>아직 안 깐 줄 수. 「더보기」 단추에 적는다.</summary>
    private int PhoneRest => Math.Max(0, Shown.Count - _take);

    private void ShowMore() => _take += PhonePage;

    /// <summary>스크롤 끝 감지점. 휴대폰에서 목록 끝에 닿으면 더보기를 자동으로 실행한다.</summary>
    private ElementReference _moreSentinel;

    /// <summary>스크롤 감지 브라우저 모듈(<c>js/note-more.js</c>).</summary>
    private IJSObjectReference? _moreModule;

    /// <summary>스크롤 감지 모듈이 우리를 부를 손잡이.</summary>
    private DotNetObjectReference<NotificationHistory>? _moreRef;

    /// <summary>휴대폰에서 목록 끝에 닿아 더 보기를 시도했다.</summary>
    [JSInvokable]
    public async Task<bool> ShowMoreFromScrollAsync()
    {
        var hasMore = false;

        await InvokeAsync(() =>
        {
            if (PhoneRest <= 0)
            {
                return;
            }

            ShowMore();
            StateHasChanged();
            hasMore = PhoneRest > 0;
        });

        return hasMore;
    }

    // ── 체크 ──────────────────────────────────────────────────────
    // 표의 체크 칸이 하던 일을 카드에서는 우리가 직접 한다 — 같은 목록(_selectedItems)을 쓴다.

    private bool Picked(NotificationDto n) => _selectedItems.Contains(n);

    private void Pick(NotificationDto n, bool on)
    {
        if (on)
        {
            if (!_selectedItems.Contains(n))
            {
                _selectedItems = [.. _selectedItems, n];
            }
        }
        else
        {
            _selectedItems = [.. _selectedItems.Where(x => !ReferenceEquals(x, n))];
        }
    }

    /// <summary>깔린 것이 모두 체크됐는가. 「전체」 스위치가 이 값을 든다 — 안 깐 줄은 세지 않는다.</summary>
    private bool AllPicked
    {
        get
        {
            var rows = PhoneShown;
            return rows.Count > 0 && rows.All(Picked);
        }
    }

    /// <summary>깔린 것을 한꺼번에 고르거나 푼다. 안 깐 줄은 잡지 않는다 — 잡으면 눈으로 확인한 것보다 많이 지운다.</summary>
    private void PickAll(bool on) =>
        _selectedItems = on ? [.. PhoneShown] : [];

    /// <summary>카드를 누름쇠로 연다. 줄 전체가 누르는 자리라 역할도 단추로 알린다.</summary>
    private Task OnCardKeyAsync(KeyboardEventArgs e, NotificationDto n) =>
        e.Key is "Enter" or " " ? OpenAsync(n) : Task.CompletedTask;

    // ── 밀어서 읽음 · 밀어서 삭제 ────────────────────────────────
    // 오른쪽으로 밀면 읽음, 왼쪽은 삭제 — 서랍의 카드와 같은 짝이라 같은 모듈(js/note-swipe.js)을 쓴다.
    // 손짓을 브라우저가 직접 받는다 — Blazor Server 에서 pointermove 를 보내면 카드가 손가락보다 몇백 ms 늦는다.

    /// <summary>손짓을 걸 목록의 DOM 아이디. 마크업과 **글자가 같아야 한다**.</summary>
    private const string SwipeListId = "ad-notecards";

    /// <summary>브라우저 쪽 모듈. 한 번 받아 두고 계속 쓴다.</summary>
    private IJSObjectReference? _swipeModule;

    /// <summary>그쪽이 우리를 부를 손잡이. 반드시 버려야 한다(회로마다 하나씩 샌다).</summary>
    private DotNetObjectReference<NotificationHistory>? _swipeRef;

    /// <summary>지금 깔린 목록에 손짓을 걸어 두었나. 목록이 사라지면 거짓으로 되돌린다 — 다시 생긴 DOM 에는 걸린 것이 없다.</summary>
    private bool _swipeOn;

    /// <summary>목록이 지금 깔려 있나. 손짓을 걸 자리가 있다는 뜻이다.</summary>
    private bool SwipeListShown => _isPhone && PhoneShown.Count > 0;

    /// <summary>카드 목록에 손짓을 건다. 목록은 휴대폰에서만, 한 건 이상일 때만 서므로 목록이 설 때마다 다시 건다.
    /// 회로가 닫히는 중이거나 프리렌더면 조용히 넘어간다 — 밀기만 못 하고 체크 칸과 ✓ 단추가 같은 일을 한다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!SwipeListShown)
        {
            _swipeOn = false;
            return;
        }

        await AttachMoreObserverAsync();

        if (_swipeOn) return;

        try
        {
            _swipeModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/note-swipe.js");
            _swipeRef ??= DotNetObjectReference.Create(this);

            await _swipeModule.InvokeVoidAsync("attachNoteSwipe",
                $"#{SwipeListId}", _swipeRef, new { card = ".ad-notecards__item[data-note]" });

            _swipeOn = true;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
                                   or ObjectDisposedException or InvalidOperationException
                                   or TaskCanceledException)
        {
        }
    }

    private async Task AttachMoreObserverAsync()
    {
        try
        {
            _moreModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Admin/js/note-more.js");
            _moreRef ??= DotNetObjectReference.Create(this);

            await _moreModule.InvokeVoidAsync(
                "attachMoreObserver", _moreSentinel, _moreRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
                                   or ObjectDisposedException or InvalidOperationException
                                   or TaskCanceledException)
        {
        }
    }

    /// <summary>오른쪽으로 민 것을 읽음으로 찍는다. 돌려주는 값은 「카드가 목록에 남느냐」다 — 안 읽은 것만 보기가 켜져 있으면 빠지고, 꺼져 있으면 그 자리에 남는다.</summary>
    [JSInvokable]
    public async Task<bool> MarkReadSwipedAsync(string id)
    {
        var note = _all.FirstOrDefault(x => x.Id == id);
        if (note is null) return true;

        if (!note.IsRead)
        {
            await MarkReadAsync(note);
        }

        await InvokeAsync(StateHasChanged);

        // 찍히지 않았으면(실패) 그 줄은 어느 쪽이든 남는다.
        return !_unreadOnly || !note.IsRead;
    }

    /// <summary>왼쪽으로 민 것을 알림함에서 치운다. 묻지 않는다 — 한 줄이라 되짚을 일이 없다. 지운 것은 체크 목록에서도 뺀다. 못 치우면 참을 돌려 카드를 제자리로 되돌린다.</summary>
    [JSInvokable]
    public async Task<bool> DeleteSwipedAsync(string id)
    {
        var note = _all.FirstOrDefault(x => x.Id == id);
        if (note is null) return true;

        if (!await RunAsync(() => Api.DeleteNotificationAsync(note.Id),
                "알림을 삭제했습니다.", "삭제하지 못했습니다"))
        {
            await InvokeAsync(StateHasChanged);
            return true;
        }

        _all = [.. _all.Where(x => !ReferenceEquals(x, note))];
        _selectedItems = [.. _selectedItems.Where(x => !ReferenceEquals(x, note))];

        await InvokeAsync(StateHasChanged);
        return false;
    }

    /// <summary>손잡이와 모듈을 버린다. 회로가 이미 닫혔으면 조용히 넘어간다.</summary>
    public async ValueTask DisposeAsync()
    {
        _swipeRef?.Dispose();
        _swipeRef = null;

        if (_moreModule is not null)
        {
            try
            {
                await _moreModule.InvokeVoidAsync("detachMoreObserver", _moreSentinel);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                                       or ObjectDisposedException or InvalidOperationException
                                       or TaskCanceledException)
            {
            }
        }

        _moreRef?.Dispose();
        _moreRef = null;

        if (_moreModule is not null)
        {
            try
            {
                await _moreModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                                       or ObjectDisposedException or InvalidOperationException
                                       or TaskCanceledException)
            {
            }
            finally
            {
                _moreModule = null;
            }
        }

        if (_swipeModule is null) return;

        try
        {
            await _swipeModule.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
        finally
        {
            _swipeModule = null;
        }
    }

    // ── 조건이 바뀌면 그 자리에서 다시 읽는다 ──────────────────
    // 기간과 구분은 서버가 거르는 조건이라 다시 묻는다. 안 읽은 것만은 받아 둔 것에서도 걸러 먼저 바뀐다.

    private Task OnFromChangedAsync(DateTime? value)
    {
        _from = value;
        return ReloadAsync();
    }

    private Task OnToChangedAsync(DateTime? value)
    {
        _to = value;
        return ReloadAsync();
    }

    private Task OnCategoryChangedAsync(string? value)
    {
        _category = value;
        return ReloadAsync();
    }

    private Task OnUnreadChangedAsync(bool value)
    {
        _unreadOnly = value;
        return ReloadAsync();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadCategoriesAsync();
        await ReloadAsync();
    }

    /// <summary>갈래 목록을 받아 고르개와 옮기개를 세운다. 조회와 묶지 않는다 — 공통코드를 못 읽어도 알림함은 열린다.</summary>
    private async Task LoadCategoriesAsync()
    {
        _categoryOptions = await Categories.OptionsAsync();
        _categoryName = PushCategoryClient.Labeler(await Categories.GetAsync());
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var mine = ++_latest;
        var rows = await Api.GetMyNotificationsAsync(_from, _to, _category, _unreadOnly, _keyword);

        // 새 조회가 이미 나갔으면 받은 답은 버린다.
        if (mine != _latest) return 1;

        _all = rows;

        // 조건이 바뀌었으니 「더보기」로 늘린 것을 되감는다.
        _take = PhonePage;

        return _all.Count;
    }, "받은 알림이 없습니다.", "알림을 읽지 못했습니다");

    /// <summary>그 알림이 가리키는 화면을 연다. 옮기기만 한다 — 탭을 세우는 것은 가는 쪽 화면의 몫이다. 열면서 읽음으로 찍는다.</summary>
    private async Task OpenAsync(NotificationDto n)
    {
        var url = OpenUrl(n.Url);

        if (url is null)
        {
            // 주소 없이 보낸 알림은 아무 일도 없으면 고장으로 읽히므로 그 사실을 말한다.
            // 열리지 않았으면 읽음으로도 찍지 않는다.
            Say("이 알림에는 열어 볼 화면이 없습니다.", NoticeTone.Warning);
            return;
        }

        // 옮기기 전에 찍는다 — 옮기고 나면 이 화면이 사라져 뒤의 일감을 확인할 길이 없다.
        await MarkReadQuietlyAsync(n);

        Navigation.NavigateTo(url);
    }

    /// <summary>여는 김에 읽음으로 찍는다. 못 찍어도 조용하다 — 사람이 시킨 것은 열기지 읽음 처리가 아니다.</summary>
    private async Task MarkReadQuietlyAsync(NotificationDto n)
    {
        if (n.IsRead)
        {
            return;
        }

        try
        {
            await Api.MarkNotificationReadAsync(n.Id);

            // 돌아왔을 때 목록이 이미 그 사실을 알고 있게 한다.
            n.IsRead = true;
        }
        catch (ApiException ex)
        {
            Log.LogDebug(ex, "알림 {Id} 를 읽음으로 찍지 못했다.", n.Id);
        }
    }

    /// <summary>저장된 주소를 지금 열 수 있는 주소로 옮긴다. DB 값은 건드리지 않고 보여 줄 때만 옮긴다.</summary>
    private static string? OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var trimmed = url.Trim();

        // 옛 AI 작업 알림: 목록 주소 + 건 번호(`?task=` · `?key=`).
        var match = AiTaskListLink().Match(trimmed);

        return match.Success ? $"/projmng/ai/task/{match.Groups["key"].Value}" : trimmed;
    }

    [GeneratedRegex(@"^/projmng/ai/tasks\?(?:task|key)=(?<key>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AiTaskListLink();

    private async Task MarkReadAsync(NotificationDto n)
    {
        if (await RunAsync(() => Api.MarkNotificationReadAsync(n.Id),
                "읽음으로 표시했습니다.", "표시하지 못했습니다"))
        {
            n.IsRead = true;
        }
    }

    /// <summary>체크한 것을 한꺼번에 읽음으로 찍는다. 이미 읽은 줄은 빼고, 왕복은 하나로 보낸다.</summary>
    private async Task MarkSelectedReadAsync()
    {
        var targets = _selectedItems.Where(n => !n.IsRead).ToList();
        if (targets.Count == 0)
        {
            Say("고른 알림이 이미 모두 읽음입니다.", NoticeTone.Warning);
            return;
        }

        if (await RunAsync(() => Api.MarkNotificationsReadAsync(targets.Select(n => n.Id)),
                $"{targets.Count}건을 읽음으로 표시했습니다.", "표시하지 못했습니다"))
        {
            // 받아 둔 것에도 반영한다 — 다시 조회하지 않는다(백 건짜리 목록이 깜박인다).
            foreach (var n in targets)
            {
                n.IsRead = true;
            }

            _selectedItems = [];
        }
    }

    /// <summary>체크한 것을 한꺼번에 알림함에서 치운다. 묻고 나서 한다 — 체크해 둔 수십 건은 되짚을 길이 없다.</summary>
    private async Task DeleteSelectedAsync()
    {
        var targets = _selectedItems.ToList();
        if (targets.Count == 0) return;

        var ok = await _confirm!.AskAsync(
            $"고른 알림 {targets.Count}건을 알림함에서 치웁니다."
            + "\n되돌릴 수 없습니다 — 보낸 쪽 발송 기록에는 남지만 이 목록에서는 다시 볼 수 없습니다.");

        if (!ok) return;

        if (await RunAsync(() => Api.DeleteNotificationsAsync(targets.Select(n => n.Id)),
                $"{targets.Count}건을 삭제했습니다.", "삭제하지 못했습니다"))
        {
            _selectedItems = [];
            await ReloadAsync();
        }
    }
}
