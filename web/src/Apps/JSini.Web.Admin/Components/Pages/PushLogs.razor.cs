using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class PushLogs : IAsyncDisposable
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>알림구분 목록. 정본은 공통코드(<c>NOTI_CATEGORY</c>)다.</summary>
    [Inject] private PushCategoryClient Categories { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        SchSummary.NameOf(_categoryOptions, o => o.Value, o => o.Text, _category),
        _reason);

    /// <summary>
    /// 한 번에 받아 둘 최대 건수.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>화면이 상한을 정한다.</b> 전에는 클라이언트의 <c>pageSize</c> 기본값
    /// (50)이 조용히 상한이었고, 화면은 그것을 전부라고 그렸다.
    /// </para>
    ///
    /// <para>
    /// 표가 30건씩 나누므로 200 이면 일곱 쪽이다. 더 늘리는 대신 기간으로
    /// 좁히게 한다 — 표 안에서 굴리는 것보다 조건을 좁히는 편이 빠르고,
    /// 넘으면 위에서 그렇게 말해 준다.
    /// </para>
    /// </remarks>
    private const int Cap = 200;

    /// <summary>
    /// 조회 기간. <b>기본이 최근 이레다.</b>
    /// </summary>
    /// <remarks>
    /// 이력은 지우지 않으므로 자라기만 한다. 조건 없이 열면 언젠가 상한에
    /// 부딪히고, 그때 사용자가 할 수 있는 일이 없다. 기본값을 두면 화면이
    /// 열리는 순간부터 조회가 유계다.
    /// </remarks>
    private DateTime? _from = AppTime.TodayDate.AddDays(-7);
    private DateTime? _to = AppTime.TodayDate;

    private string? _reason;

    /// <summary>
    /// 고른 알림구분. 비면 전체다.
    /// <see cref="PushCategoryClient.Unset"/> 이면 <b>구분이 안 붙은 줄</b>만 본다.
    /// </summary>
    private string? _category;

    private IReadOnlyList<SchOption> _categoryOptions = [new SchOption(null, "전체")];

    /// <summary>코드값 → 이름. 표가 줄마다 부른다.</summary>
    private Func<string?, string> _categoryName = v => v ?? string.Empty;

    private IReadOnlyList<PushLogDto> _logs = [];
    private int _total;

    /// <summary>휴대폰(≤767px)인가. <c>DxLayoutBreakpoint</c> 가 채운다.</summary>
    private bool _isPhone;

    private void OnPhoneChanged(bool active) => _isPhone = active;

    /// <summary>휴대폰에서 한 번에 깔 줄 수. 「더보기」가 이만큼씩 늘린다.</summary>
    private const int PhonePage = 20;

    /// <summary>지금까지 깔기로 한 줄 수.</summary>
    private int _take = PhonePage;

    /// <summary>휴대폰 카드 목록에 실제로 깔리는 줄.</summary>
    private IReadOnlyList<PushLogDto> PhoneShown
    {
        get
        {
            var logs = _logs;
            return _take < logs.Count ? [.. logs.Take(_take)] : logs;
        }
    }

    /// <summary>아직 안 깐 줄 수. 「더보기」 단추에 적는다.</summary>
    private int PhoneRest => Math.Max(0, _logs.Count - _take);

    private void ShowMore() => _take += PhonePage;

    /// <summary>스크롤 끝 감지점. 휴대폰에서 목록 끝에 닿으면 더보기를 자동으로 실행한다.</summary>
    private ElementReference _moreSentinel;

    /// <summary>스크롤 감지 브라우저 모듈(<c>js/note-more.js</c>).</summary>
    private IJSObjectReference? _moreModule;

    /// <summary>스크롤 감지 모듈이 우리를 부를 손잡이.</summary>
    private DotNetObjectReference<PushLogs>? _moreRef;

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isPhone && PhoneShown.Count > 0)
        {
            await AttachMoreObserverAsync();
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

    public async ValueTask DisposeAsync()
    {
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
    }

    protected override async Task OnInitializedAsync()
    {
        // **조회와 묶지 않는다.** 공통코드를 못 읽어도 이력은 열려야 한다 —
        // 그때는 고르개에 「전체」만 남는다.
        _categoryOptions = await Categories.OptionsAsync();
        _categoryName = PushCategoryClient.Labeler(await Categories.GetAsync());

        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        (_logs, _total) = await Api.GetPushLogsAsync(Cap, _reason, _from, _to, _category);

        // 조건이 바뀌었으니 「더보기」로 늘려 둔 것을 되감는다.
        _take = PhonePage;

        // **잘렸으면 반드시 말한다.** 「전부다」로 읽고 넘어가면 없는 것을
        // 찾게 된다. 조회의 **결과**라 안내 줄이 아니라 토스트로 나간다.
        // 문구는 Q&A 목록과 같게 둔다 — 같은 상황에 다른 말을 하면 다른 일로 읽는다.
        if (_total > _logs.Count)
        {
            Say($"전체 {_total}건 중 {_logs.Count}건입니다. 기간·구분·사유로 좁히십시오.",
                NoticeTone.Warning);
        }

        return _logs.Count;
    }, "조건에 맞는 발송 내역이 없습니다.", "발송 이력을 읽지 못했습니다");
}
