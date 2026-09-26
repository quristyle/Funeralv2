using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Models;

namespace JSini.Web.Components.Layout;

public partial class NoticeAutoPopup
{
    [Inject] private NoticeClient Notices { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<NoticeAutoPopup> Log { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;

    /// <summary>
    /// 브라우저에 「오늘 하루 보지 않기」를 적어 두는 열쇠. Vue 때와 같다.
    ///
    /// <para>
    /// <b>정본은 <see cref="PortalBoot.NoticeDismissedKey"/> 다.</b> 읽는 일은
    /// 그쪽이 다른 값들과 함께 한 왕복으로 하고, 여기서는 쓰는 데만 쓴다.
    /// </para>
    /// </summary>
    private const string StorageKey = PortalBoot.NoticeDismissedKey;

    /// <summary>이 탭에서 이미 닫았다는 표시. 머리말의 표를 보라.</summary>
    private const string ClosedKey = PortalBoot.NoticeClosedUserKey;

    private IReadOnlyList<NoticeDto> _notices = [];
    private bool _open;

    /// <summary>오늘 안 보기로 해 둔 공지들. 저장할 때 다시 쓴다.</summary>
    private HashSet<string> _dismissed = new(StringComparer.Ordinal);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // 저장소를 직접 읽지 않는다. 닫힘 표시와 「오늘 하루 보지 않기」를
        // 따로 읽어 왕복이 둘이던 자리다 — 이제 잠금 표시·고정 탭·테마까지
        // 함께 **한 왕복**이다(`PortalBoot` 머리말).
        var browser = await Boot.ReadAsync();

        // 이 탭에서 이미 닫았으면 조회조차 하지 않는다.
        if (browser.NoticeClosed)
        {
            return;
        }

        _dismissed = ParseDismissed(browser.NoticeDismissedJson);

        IReadOnlyList<NoticeDto> list;

        try
        {
            list = await Notices.GetPopupAsync();
        }
        catch (Exception ex)
        {
            // **공지를 못 읽었다고 화면 진입을 막지 않는다.** 옛 화면도 같았다 —
            // 안내를 못 본 것과 로그인이 안 되는 것은 무게가 다르다.
            Log.LogWarning(ex, "팝업 공지를 읽지 못했습니다.");
            return;
        }

        var fresh = list.Where(n => !_dismissed.Contains(n.Id)).ToList();

        if (fresh.Count == 0)
        {
            return;
        }

        _notices = fresh;
        _open = true;

        StateHasChanged();
    }

    /// <summary>
    /// 창이 닫히면 이 탭에 표시를 남긴다. 그래야 화면을 옮겨 다녀도 다시 뜨지 않는다.
    /// </summary>
    private async Task OpenChangedAsync(bool open)
    {
        _open = open;

        if (open)
        {
            return;
        }

        try
        {
            await Js.InvokeVoidAsync("sessionStorage.setItem", ClosedKey, "1");
        }
        catch (JSException ex)
        {
            // 못 적어도 이번 탭에서 한 번 더 뜨는 것이 전부다.
            Log.LogDebug(ex, "공지 팝업을 닫았다는 표시를 남기지 못했습니다.");
        }
    }

    /// <summary>
    /// 「오늘 하루 보지 않기」로 닫은 공지를 브라우저에 적어 둔다.
    /// </summary>
    private async Task DismissAsync(string noticeId)
    {
        _dismissed.Add(noticeId);

        var payload = System.Text.Json.JsonSerializer.Serialize(
            new DismissRecord { Until = Today(), Ids = [.. _dismissed] });

        try
        {
            await Js.InvokeVoidAsync("localStorage.setItem", StorageKey, payload);
        }
        catch (JSException ex)
        {
            // 사생활 보호 모드에서는 setItem 이 던진다. 이번 창에서만 안 보이고
            // 끝날 뿐이라 사용자에게 말할 일이 아니다.
            Log.LogDebug(ex, "「오늘 하루 보지 않기」를 저장하지 못했습니다.");
        }
    }

    /// <summary>
    /// 오늘 안 보기로 해 둔 공지를 옮겨 담는다. 날짜가 지났으면 없던 일로 한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 읽어 오는 일은 <see cref="PortalBoot"/> 가 한다 — 여기는 그 날것 JSON 을
    /// 받아 푸는 자리다. 그래서 <c>JSException</c> 을 살피지 않는다(JS 를 부르지
    /// 않으므로 날 수 없다).
    /// </para>
    ///
    /// <para>
    /// <b>로그인 화면도 같은 값을 읽고 쓴다</b>(theme.js 의 <c>jsiniNotice</c>).
    /// 칸 이름을 바꾸면 그쪽도 함께 고친다 — 어긋나면 오류가 아니라 「오늘
    /// 하루 보지 않기가 한쪽에서만 듣는다」로 나온다.
    /// </para>
    /// </remarks>
    private HashSet<string> ParseDismissed(string? raw)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var saved = System.Text.Json.JsonSerializer.Deserialize<DismissRecord>(raw);

            // 「오늘 하루」다. 날짜가 바뀌면 전부 다시 보인다.
            return saved is not null && saved.Until == Today()
                ? new HashSet<string>(saved.Ids, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }
        catch (System.Text.Json.JsonException ex)
        {
            // 값이 깨져 있으면 없는 것으로 본다. 공지를 한 번 더 보는 쪽이
            // 안 보이는 쪽보다 낫다.
            Log.LogDebug(ex, "저장된 「오늘 하루 보지 않기」를 읽지 못했습니다.");
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 브라우저 기준이 아니라 <b>서버 기준</b> 날짜다.
    ///
    /// <para>
    /// 회로 안에서는 서버 시간밖에 없다. 사용자가 다른 시간대에 있으면
    /// 「하루」의 경계가 몇 시간 어긋나는데, 어긋나는 결과는 공지를 한 번 더
    /// 보거나 몇 시간 덜 보는 것뿐이라 시간대를 받아 오지 않는다.
    /// </para>
    ///
    /// <para>
    /// <b>로그인 화면도 이 기준을 쓴다</b> — 그쪽은 회로가 없지만 서버가
    /// 날짜를 마크업에 실어 보낸다(<c>PublicNoticePopup.Today</c>).
    /// </para>
    /// </summary>
    private static string Today() => DateTime.Now.ToString("yyyy-MM-dd");

    /// <summary>localStorage 에 적어 두는 모양. Vue 때와 같은 칸 이름이다.</summary>
    private sealed class DismissRecord
    {
        public string Until { get; set; } = string.Empty;
        public List<string> Ids { get; set; } = [];
    }
}
