using System.Text.Json;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 회로가 붙을 때 브라우저에서 읽어 올 것을 <b>한 번의 왕복으로</b> 읽는다.
///
/// [무엇을 고친 것인가]
///
/// 레이아웃이 뜰 때 부품 다섯이 저마다 저장소를 읽었고 워터마크를 거는 호출이
/// 하나 더 있었다 — 여섯이 직렬이었다.
///
/// <list type="table">
///   <item><term><c>MainLayout</c></term><description>워터마크 걸기</description></item>
///   <item><term><c>ScreenLock</c></term><description>잠금 표시</description></item>
///   <item><term><c>NoticeAutoPopup</c></term><description>닫힘 표시 · 「오늘 하루 보지 않기」</description></item>
///   <item><term><c>TabBar</c></term><description>고정 탭</description></item>
///   <item><term><c>ThemeToggle</c></term><description>지금 테마</description></item>
/// </list>
///
/// Blazor Server 에서 JS 호출 하나는 <b>브라우저까지 갔다 오는 왕복 하나</b>다.
/// 그리고 포털은 <b>업무를 넘나들 때마다 레이아웃을 새로 만들기 때문에</b>
/// (Piral 모듈 컨테이너가 갈리면서 그 안의 scoped 서비스도 함께 새로 생긴다)
/// 그 여섯이 화면 전환마다 다시 났다.
///
/// [먼저 부른 사람이 왕복을 낸다]
///
/// 부품들의 <c>OnAfterRenderAsync</c> 순서를 우리가 정하지 못한다 — Blazor 는
/// 자식을 부모보다 먼저 부른다. 그래서 <b>누가 먼저 불러도 되게</b> 만들었다.
/// 첫 사람이 왕복을 내고, 그 사이에 들어온 나머지는 <b>같은 <c>Task</c></b> 를
/// 기다린다. 순서에 기대는 코드를 두면 부품을 하나 더 붙이는 날 조용히
/// 왕복이 둘로 늘어난다.
///
/// [왜 업무 전환을 넘어서까지 들고 있지 않나]
///
/// <see cref="PortalBootstrapStore"/> 는 싱글턴 통으로 전환마다 나던 왕복을
/// 아예 없앴다. 여기서 같은 것을 하지 않는 이유는 <b>담긴 값의 임자가
/// 다르다</b>는 것이다 — 저것은 사용자의 것(어느 탭에서 봐도 같다)이고
/// 이것은 <b>탭의 것</b>이다. 탭 하나를 잠그면 그 탭만 잠겨야 하고, 고정
/// 탭도 창마다 다르다.
///
/// 사용자로 열쇠를 만들어 담으면 <b>두 탭이 서로의 상태를 물려받는다.</b>
/// 그런데 Blazor 는 부품에게 자기 회로 아이디를 알려 주지 않아서, 탭을
/// 가르는 열쇠를 만들 방법이 지금은 없다. 그래서 여기서 멈춘다 —
/// <b>여섯을 하나로 줄이는 것까지가 확실하고, 하나를 0 으로 만드는 것은
/// 탭이 섞이는 위험을 안고 가는 일이다.</b>
///
/// 뿌리(레이아웃이 왜 다시 만들어지는가)를 잡으면 이 고민 자체가 없어진다.
/// 그때 이 클래스는 「회로가 붙을 때 한 번 읽는 곳」으로 그대로 남는다.
/// </summary>
public sealed class PortalBoot(IJSRuntime js, ILogger<PortalBoot> logger)
{
    // ── 브라우저 저장소의 열쇠 ────────────────────────────────
    //
    // **여기가 정본이다.** 전에는 네 파일에 흩어져 있었고, 읽는 자리와 쓰는
    // 자리가 다른 파일에 있는 열쇠도 있었다. 한 곳에 모아 두면 새 값을 붙일 때
    // 왕복이 늘지 않는다는 것도 눈에 보인다 — 아래 목록에 한 줄 더할 뿐이다.
    //
    // theme.js 에는 적지 않는다. 양쪽에 적으면 한쪽만 고치는 날이 오고,
    // 그때 증상은 「그 표시가 조용히 안 읽힌다」다.

    /// <summary>잠금화면(D7). 창을 닫으면 함께 사라져야 해서 세션이다.</summary>
    public const string ScreenLockedKey = "jsini.screen-locked";

    /// <summary>로그인 뒤 공지를 이 탭에서 닫았다.</summary>
    public const string NoticeClosedUserKey = "jsini-notice-closed:user";

    /// <summary>
    /// 공개 공지를 이 탭에서 닫았다. <b>로그인용과 따로다</b> — 하나로 두면
    /// 로그인 화면에서 공개 공지를 닫은 사람이 로그인한 뒤 사내 공지를 못 본다.
    /// </summary>
    public const string NoticeClosedPublicKey = "jsini-notice-closed:public";

    /// <summary>「오늘 하루 보지 않기」. 날짜가 바뀌면 없던 일이 되므로 localStorage 다.</summary>
    public const string NoticeDismissedKey = "jsini-notice-dismissed";

    /// <summary>고정해 둔 탭. 이 브라우저에만 남는다 — 서버에 두면 즐겨찾기가 된다.</summary>
    public const string PinnedTabsKey = "jsini-tabs-pinned";

    private static readonly string[] SessionKeys =
    [
        ScreenLockedKey,
        NoticeClosedUserKey,
        NoticeClosedPublicKey,
    ];

    private static readonly string[] LocalKeys =
    [
        NoticeDismissedKey,
        PinnedTabsKey,
    ];

    /// <summary>
    /// 읽어 온 것. 못 읽었으면 <see cref="BrowserState.Empty"/> 다 —
    /// <c>null</c> 을 돌려주지 않는 이유는 부르는 자리가 다섯이고 그 다섯이
    /// 각자 <c>null</c> 을 살피게 두면 하나는 반드시 빠뜨리기 때문이다.
    /// </summary>
    private Task<BrowserState>? _reading;

    /// <summary>워터마크에 쓸 이름. 읽기 전에 정해지면 같은 왕복에 태운다.</summary>
    private string? _watermark;
    private bool _watermarkAsked;

    /// <summary>같은 왕복에 태워 지울 세션 열쇠.</summary>
    private readonly List<string> _forget = [];

    /// <summary>
    /// 워터마크에 쓸 이름을 알려 준다. <b>JS 를 부르지 않는다</b> — 읽기가
    /// 일어날 때 함께 태운다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>MainLayout</c> 이 <c>OnInitialized</c> 에서 부른다. 그 자리가
    /// <b>자식 부품이 생기기도 전</b>이라, 자식이 먼저 읽기를 시작하는
    /// 경우에도 이름이 이미 여기 들어 있다.
    /// </para>
    ///
    /// <para>
    /// 그래도 늦게 오는 경우가 있다 — 읽기가 이미 끝난 뒤다. 그때는
    /// <b>혼자 한 번 부른다.</b> 워터마크는 「누구 화면인지 사진에 남게」
    /// 하려고 있는 것이라 <b>빠지면 안 되는 쪽</b>이고, 왕복 하나를 아끼려고
    /// 걸릴지 말지를 순서에 맡길 자리가 아니다.
    /// </para>
    /// </remarks>
    public void UseWatermark(string? name)
    {
        _watermark = name;
        _watermarkAsked = true;

        if (_reading is null)
        {
            return;
        }

        _ = ShowWatermarkLateAsync(name);
    }

    /// <summary>
    /// 로그인 뒤 공지의 닫힘 표시를 지운다. 로그인 화면이 부른다 —
    /// 곧 로그인할 참이니 그 공지는 다시 떠야 한다.
    /// </summary>
    /// <remarks>
    /// 읽기 전에 부르면 같은 왕복에 실려 나가고, 그때 돌아오는 값은
    /// <b>지운 뒤의 값</b>이다(theme.js 가 지우기를 먼저 한다).
    /// </remarks>
    public void ForgetUserNoticeClosed()
    {
        if (_reading is null)
        {
            _forget.Add(NoticeClosedUserKey);
            return;
        }

        _ = ForgetLateAsync(NoticeClosedUserKey);
    }

    /// <summary>
    /// 브라우저 상태를 읽는다. <b>회로가 붙은 뒤에</b> 불러야 한다
    /// (<c>OnAfterRenderAsync</c>) — 프리렌더 중에는 JS 를 부를 수 없다.
    ///
    /// <para>
    /// 몇 번을 불러도 왕복은 한 번이다. 두 번째부터는 첫 번째가 받아 둔 것을
    /// 그대로 돌려준다.
    /// </para>
    /// </summary>
    public Task<BrowserState> ReadAsync() => _reading ??= ReadOnceAsync();

    private async Task<BrowserState> ReadOnceAsync()
    {
        // 요청을 만드는 사이에 UseWatermark 가 들어올 수는 없다 — 회로는
        // 한 번에 하나만 돌린다. 그래도 값을 미리 떠 두는 편이 읽기 쉽다.
        var request = new BootRequest
        {
            Watermark = _watermarkAsked ? _watermark : null,
            Forget = [.. _forget],
            Session = SessionKeys,
            Local = LocalKeys,
        };

        _forget.Clear();

        try
        {
            var wire = await js.InvokeAsync<BootWire>("jsiniBoot.read", request);

            return wire is null ? BrowserState.Empty : BrowserState.From(wire);
        }
        catch (JSException ex)
        {
            // theme.js 가 안 실렸거나 저장소를 통째로 막아 둔 브라우저다.
            //
            // **빈 값으로 넘어간다.** 읽기 실패로 화면을 세우지 않는다 —
            // 여기서 얻는 것은 「잠겨 있었다」·「닫아 두었다」 같은 편의뿐이고,
            // 그것을 못 읽었을 때의 올바른 동작은 전부 「없던 것으로 본다」다.
            // 특히 잠금은 못 읽었을 때 덮으면 풀 방법이 없는 상태에 갇힌다.
            logger.LogDebug(ex, "브라우저 상태를 읽지 못했다. 없는 것으로 본다.");
            return BrowserState.Empty;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // 모양이 어긋나거나 회로가 이미 끊긴 경우다. 위와 같이 다룬다.
            logger.LogDebug(ex, "브라우저 상태를 옮기지 못했다. 없는 것으로 본다.");
            return BrowserState.Empty;
        }
    }

    private async Task ShowWatermarkLateAsync(string? name)
    {
        try
        {
            await js.InvokeVoidAsync("jsiniWatermark.show", name);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "워터마크를 걸지 못했다.");
        }
    }

    private async Task ForgetLateAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("sessionStorage.removeItem", key);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "{Key} 표시를 지우지 못했다.", key);
        }
    }

    // ── 오가는 모양 ───────────────────────────────────────────

    /// <summary>theme.js 의 <c>jsiniBoot.read</c> 에 넘기는 것.</summary>
    private sealed class BootRequest
    {
        public string? Watermark { get; init; }
        public string[] Forget { get; init; } = [];
        public string[] Session { get; init; } = [];
        public string[] Local { get; init; } = [];
    }

    /// <summary>
    /// 그 응답. 사전 둘과 테마 하나다.
    ///
    /// <para>
    /// <c>internal</c> 인 것은 <see cref="BrowserState.From"/> 이 이것을 받기
    /// 때문이다. 저쪽이 <c>internal</c> 이라 여기도 그보다 좁을 수 없다.
    /// </para>
    /// </summary>
    internal sealed class BootWire
    {
        public Dictionary<string, string?> Session { get; init; } = [];
        public Dictionary<string, string?> Local { get; init; } = [];
        public ThemeWire? Theme { get; init; }
    }

    /// <summary>
    /// <c>jsiniTheme.current()</c> 의 모양 그대로.
    /// <c>ThemeToggle</c> 이 자기 <c>Current</c> 로 옮겨 담는다.
    /// </summary>
    public sealed class ThemeWire
    {
        public string Family { get; init; } = "fluent";
        public string? Mode { get; init; }
        public string? Accent { get; init; }
        public string? Custom { get; init; }
        public string? Classic { get; init; }
        public string? Bootstrap { get; init; }
        public string? Size { get; init; }
    }

    /// <summary>
    /// 브라우저에서 읽어 온 한 벌. <b>부품은 이 모양만 본다</b> —
    /// 열쇠 글자를 부품에 다시 적으면 읽는 자리가 또 흩어진다.
    /// </summary>
    public sealed class BrowserState
    {
        /// <summary>못 읽었을 때. 전부 「없다」다.</summary>
        public static readonly BrowserState Empty = new();

        /// <summary>이 탭이 잠겨 있었는가.</summary>
        public bool ScreenLocked { get; private init; }

        /// <summary>로그인 뒤 공지를 이 탭에서 닫았는가.</summary>
        public bool NoticeClosedForUser { get; private init; }

        /// <summary>공개 공지를 이 탭에서 닫았는가.</summary>
        public bool NoticeClosedForPublic { get; private init; }

        /// <summary>「오늘 하루 보지 않기」로 적어 둔 것. 날것 JSON 이고 없으면 <c>null</c>.</summary>
        public string? NoticeDismissedJson { get; private init; }

        /// <summary>고정해 둔 탭. 날것 JSON 이고 없으면 <c>null</c>.</summary>
        public string? PinnedTabsJson { get; private init; }

        /// <summary>지금 고른 테마. theme.js 가 안 실렸으면 <c>null</c>.</summary>
        public ThemeWire? Theme { get; private init; }

        /// <summary>공지 닫힘 표시. 공개용과 로그인용을 가려 준다.</summary>
        public bool NoticeClosed(bool isPublic) =>
            isPublic ? NoticeClosedForPublic : NoticeClosedForUser;

        internal static BrowserState From(BootWire wire) => new()
        {
            // 값이 "1" 이든 무엇이든 **있으면 그렇다는 뜻**이다. 옛 코드가
            // 잠금은 "1" 로만 인정하고 공지는 길이만 보았는데, 굽는 곳이
            // 우리뿐이라 둘을 가릴 이유가 없었다.
            ScreenLocked = Has(wire.Session, ScreenLockedKey),
            NoticeClosedForUser = Has(wire.Session, NoticeClosedUserKey),
            NoticeClosedForPublic = Has(wire.Session, NoticeClosedPublicKey),
            NoticeDismissedJson = Get(wire.Local, NoticeDismissedKey),
            PinnedTabsJson = Get(wire.Local, PinnedTabsKey),
            Theme = wire.Theme,
        };

        private static bool Has(Dictionary<string, string?> from, string key) =>
            Get(from, key) is { Length: > 0 };

        private static string? Get(Dictionary<string, string?> from, string key) =>
            from.TryGetValue(key, out var value) ? value : null;
    }
}
