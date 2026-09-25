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
/// 이것은 <b>브라우저의 것</b>이다 — 고정 탭도 창마다 다르고, 공지를 닫은
/// 표시는 탭마다 다르다.
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

    /// <summary>
    /// 잠금화면(D7). <b>기기에 남는다</b>(localStorage).
    ///
    /// <para>
    /// 처음에는 세션이었다 — 「창을 닫으면 잠금도 함께 사라져야 한다」고 보았다.
    /// 그런데 이 포털은 <b>PWA 로 설치해서 앱처럼 쓴다.</b> 앱을 내리는 것은
    /// 자리를 비우는 흔한 방법이지 잠금을 푸는 방법이 아닌데, 세션이면
    /// <b>앱을 껐다 켜는 것만으로 덮개가 걷혔다.</b> 새로고침은 버티고 앱 재시작은
    /// 못 버티는 잠금은 잠금이 아니다.
    /// </para>
    ///
    /// <para>
    /// 그래서 기기에 남긴다. 대신 <b>로그인 화면을 지나면 지운다</b>
    /// (<c>Login.razor</c> 가 <c>jsiniLock.forget</c> 을 부른다) — 남겨 두면
    /// 잠금화면에서 로그아웃하고 다시 들어온 사람이 <b>방금 친 비밀번호를
    /// 한 번 더</b> 쳐야 하고, 남의 기기를 빌려 로그인한 사람에게는 자기가
    /// 잠근 적 없는 덮개가 뜬다.
    /// </para>
    /// </summary>
    public const string ScreenLockedKey = "jsini.screen-locked";

    /// <summary>로그인 뒤 공지를 이 탭에서 닫았다.</summary>
    public const string NoticeClosedUserKey = "jsini-notice-closed:user";

    /// <summary>
    /// 공개 공지를 이 탭에서 닫았다. <b>로그인용과 따로다</b> — 하나로 두면
    /// 로그인 화면에서 공개 공지를 닫은 사람이 로그인한 뒤 사내 공지를 못 본다.
    ///
    /// <para>
    /// <b>이 왕복으로 읽지 않는다.</b> 이 값을 보는 것은 로그인 화면뿐이고
    /// 그 화면에는 회로가 없다 — 읽고 쓰는 일은 theme.js 의
    /// <c>jsiniNotice</c> 가 한다(<see cref="PublicNoticePopup"/>). 열쇠 글자를
    /// 그쪽에 적지 않으려고 <b>정본만</b> 여기 남긴다.
    /// </para>
    /// </summary>
    public const string NoticeClosedPublicKey = "jsini-notice-closed:public";

    /// <summary>「오늘 하루 보지 않기」. 날짜가 바뀌면 없던 일이 되므로 localStorage 다.</summary>
    public const string NoticeDismissedKey = "jsini-notice-dismissed";

    /// <summary>고정해 둔 탭. 이 브라우저에만 남는다 — 서버에 두면 즐겨찾기가 된다.</summary>
    public const string PinnedTabsKey = "jsini-tabs-pinned";

    /// <summary>
    /// 알림 구독 권유 창을 <b>이 탭에서 닫았다</b>(<c>PushAskPopup</c>).
    /// 탭을 닫으면 사라지므로 다음에 새로 열면 다시 묻는다 — 「나중에」의 뜻이
    /// 그것이다.
    /// </summary>
    public const string PushAskClosedKey = "jsini-push-ask-closed";

    /// <summary>
    /// 그 창에서 <b>「다시 묻지 않기」</b>를 눌렀다. 영영 안 묻는다.
    ///
    /// <para>
    /// <b>기기에 남는다</b>(localStorage). 계정에 담지 않는 이유는 이 창이
    /// 묻는 것이 「이 브라우저로 받겠는가」라서다 — 회사 컴퓨터에서 그만
    /// 묻게 해 놓고 휴대폰에서는 권유를 받고 싶을 수 있다.
    /// </para>
    /// </summary>
    public const string PushAskNeverKey = "jsini-push-ask-never";

    /// <summary>
    /// 휴대폰에서 그 창을 <b>언제까지 접어 둘 것인가</b>(UTC, <c>o</c> 꼴).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 휴대폰에서는 권유를 <b>되풀이한다</b> — 설치와 구독이 거기서 실제로
    /// 값을 하는데, 한 번 「나중에」로 접히면 다시 물을 길이 없었다. 그래서
    /// 「닫았다/영영 그만」 대신 <b>기한</b>을 적어 둔다
    /// (<c>PushAskPopup.ReofferAfter</c>).
    /// </para>
    /// <para>
    /// <b>기기에 남는다</b>(localStorage). 탭 하나에만 남기면 홈 화면 앱을
    /// 껐다 켜는 것만으로 기한이 사라져 열 때마다 권유가 뜬다 —
    /// <see cref="ScreenLockedKey"/> 가 세션이었을 때 겪은 것과 같은 꼴이다.
    /// </para>
    /// </remarks>
    public const string PushAskSnoozeKey = "jsini-push-ask-snooze";

    /// <summary>
    /// 위치 권유 창을 <b>이 탭에서 닫았다</b>(<c>LocationAskPopup</c>).
    /// 「나중에」의 뜻이 그것이다 — 다음에 새로 열면 다시 묻는다.
    /// </summary>
    public const string GeoAskClosedKey = "jsini-geo-ask-closed";

    /// <summary>
    /// 그 창에서 <b>「다시 묻지 않기」</b>를 눌렀다. 영영 안 묻는다.
    ///
    /// <para>
    /// 알림 권유와 같은 까닭으로 <b>기기에 남는다</b>(<see cref="PushAskNeverKey"/>) —
    /// 회사 컴퓨터에서는 위치를 안 주고 휴대폰에서는 주고 싶을 수 있다.
    /// 게다가 위치 권한 자체가 브라우저마다 따로다.
    /// </para>
    /// </summary>
    public const string GeoAskNeverKey = "jsini-geo-ask-never";

    /// <summary>
    /// 이 브라우저에서 <b>위치 일을 마지막으로 다룬</b> 때(UTC, <c>o</c> 꼴).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「조용히 쟀다」만 뜻하지 않는다.</b> 저장했든, 물어보고 닫혔든,
    /// 「이미 줬더라」를 알아냈든 다 찍는다 — 이 표시가 묻는 것은 「지금 또
    /// 해야 하나」 하나뿐이다.
    /// </para>
    /// <para>
    /// <b>서버에도 확인 시각이 있는데 왜 브라우저에 또 두나.</b> 이 판단을
    /// <b>서버를 부르기 전에</b> 내리려고 있다 — 포털은 업무를 넘나들 때마다
    /// 레이아웃을 새로 만들고, 그때마다 설정을 읽어 확인하면 재지도 않으면서
    /// 왕복만 는다.
    /// </para>
    /// <para>
    /// 기기의 것이라는 뜻도 맞다. 조용한 확인은 <b>권한을 허용해 둔 이 브라우저</b>
    /// 에서만 도는 일이다.
    /// </para>
    /// </remarks>
    public const string GeoSyncedAtKey = "jsini-geo-synced-at";

    /// <summary>
    /// 끌어 넓혀 둔 사이드바 폭(px). <b>이 브라우저의 것이다</b> —
    /// 화면 크기에 따라 알맞은 폭이 다르므로 사용자가 아니라 기기에 남는다.
    /// </summary>
    public const string SidebarWidthKey = "jsini-sidebar-width";

    /// <summary>
    /// 모바일 화면의 떠다니는 메뉴 단추(FAB) 위치.
    /// <c>bottom-left</c>(기본), <c>top-left</c>, <c>bottom-right</c>, <c>top-right</c>.
    /// </summary>
    public const string FabPositionKey = "jsini-fab-position";

    /// <summary>
    /// 모바일 메뉴 단추(FAB)를 <b>아예 감출 것인가</b>. 열쇠가 있으면 감춘다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 브라우저의 것이다</b> — 단추가 무엇을 가리는지는 화면 크기와
    /// 그 사람이 그 기기로 하는 일에 달렸다. 계정에 저장하면 휴대폰에서
    /// 감춘 것이 큰 모니터까지 따라온다(위치 열쇠와 같은 갈래다).
    /// </para>
    /// <para>
    /// <b>감추면 헤더의 ☰ 가 휴대폰에서 되살아난다</b>(app.css). 휴대폰에서는
    /// 하는 일이 똑같다는 이유로 그 ☰ 를 빼고 이 단추만 남겨 두었기 때문에,
    /// 그냥 감추기만 하면 <b>메뉴를 열 길이 하나도 없어진다.</b>
    /// </para>
    /// </remarks>
    public const string FabHiddenKey = "jsini-fab-hidden";

    /// <summary>
    /// 휴대폰 화면 아래의 띠(<c>MobileBottomNav</c>)를 <b>쓰지 않을 것인가</b>.
    /// 열쇠가 있으면 안 쓴다 — 즉 <b>없는 것이 기본이고, 기본은 쓴다</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「쓴다」가 아니라 「안 쓴다」를 적어 두는 이유</b>가 있다. 읽는 쪽의
    /// 판정이 「열쇠가 있으면 그렇다는 뜻」(<see cref="BrowserState.From"/>)이라
    /// 「쓴다」를 적으면 <b>한 번도 안 고친 사람에게 띠가 사라진다.</b>
    /// 떠다니는 단추 숨김(<see cref="FabHiddenKey"/>)과 같은 꼴이다.
    /// </para>
    /// <para>
    /// <b>이 브라우저의 것이다</b> — 띠는 767px 아래에서만 보이므로 계정에
    /// 담아 봐야 큰 모니터에서는 쓰이지 않는다.
    /// </para>
    /// </remarks>
    public const string BottomNavHiddenKey = "jsini-bottomnav-hidden";

    /// <summary>
    /// 그 띠에 놓을 칸들(JSON). 없으면 <c>BottomNav.Defaults</c> 다.
    /// </summary>
    /// <remarks>
    /// 모양과 기본값은 <see cref="BottomNav"/> 가 갖는다 — 여기는 <b>열쇠
    /// 이름만</b> 안다. 읽어 온 글자를 그대로 넘기는 이유는 이 통이 화면의
    /// 말(<c>BottomNavItem</c>)을 모르게 두려는 것이다.
    /// </remarks>
    public const string BottomNavItemsKey = "jsini-bottomnav-items";

    /// <summary>
    /// 토스트(화면에 뜨는 알림) 위치.
    /// <c>bottom-right</c>(기본) 외 여섯 자리 중 하나다.
    /// </summary>
    /// <remarks>
    /// <b>이 브라우저의 것이다.</b> 알맞은 자리가 화면 크기와 쓰는 손에 따라
    /// 다르고, 계정에 저장하면 큰 모니터에서 고른 자리가 휴대폰까지 따라온다.
    /// </remarks>
    public const string ToastPositionKey = "jsini-toast-position";

    /// <summary>
    /// 위치를 <b>저절로 다시 확인하는 간격</b>(분). 없으면
    /// <see cref="DefaultGeoSyncMinutes"/> 다.
    /// </summary>
    /// <remarks>
    /// 기기에 남기는 것이 맞다 — 재는 일도, 그 값이 드는 배터리도 이 브라우저의
    /// 것이다. 휴대폰에서는 넓게, 자리에 앉아 쓰는 컴퓨터에서는 촘촘하게 두는
    /// 것이 자연스럽다.
    /// </remarks>
    public const string GeoSyncIntervalKey = "jsini-geo-sync-interval";

    /// <summary>
    /// 휴대폰·태블릿에서 두 손가락 확대(핀치 줌)를 <b>풀어 두었는가</b>.
    /// 열쇠가 있으면 푼 것이다 — 즉 <b>없는 것이 기본이고, 기본은 잠근다</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「잠근다」가 아니라 「풀었다」를 적는다.</b> 읽는 쪽의 판정이
    /// 「열쇠가 있으면 그렇다는 뜻」(<see cref="BrowserState.From"/>)이라,
    /// 기본값과 반대되는 쪽을 적어야 한 번도 안 고친 사람이 기본값을 받는다.
    /// <see cref="FabHiddenKey"/> · <see cref="BottomNavHiddenKey"/> 와 같은 꼴인데
    /// <b>기본이 반대라 적히는 말도 반대</b>다.
    /// </para>
    /// <para>
    /// <b>이 브라우저의 것이다</b> — 잠금이 걸리는 것은 좁은 화면뿐이라
    /// (<c>js/zoom.js</c>) 계정에 담아 봐야 큰 모니터에서는 쓰이지 않는다.
    /// </para>
    /// <para>
    /// <b>이 값을 실제로 읽는 것은 C# 이 아니라 <c>js/zoom.js</c> 다.</b> 그 파일이
    /// 회로보다 먼저 돌면서 같은 열쇠를 스스로 읽는다 — 여기 있는 것은 환경설정
    /// 스위치가 「지금 무엇으로 되어 있나」를 보여 주기 위해서다. <b>열쇠 글자가
    /// 두 곳에 적혀 있으므로 한쪽만 고치면 조용히 어긋난다.</b>
    /// </para>
    /// </remarks>
    public const string ZoomUnlockedKey = "jsini-zoom-unlocked";

    /// <summary>
    /// 고르지 않았을 때의 확인 간격 — <b>한 시간</b>이다.
    /// </summary>
    /// <remarks>
    /// 사람이 고를 수 있는 가장 촘촘한 발송 간격이 세 시간이므로, 한 시간이면
    /// 어느 쪽이든 발송 전에 적어도 두 번은 확인한다
    /// (<c>GeoLocator.SyncInterval</c> 의 머리말).
    /// </remarks>
    public const int DefaultGeoSyncMinutes = 60;

    private static readonly string[] SessionKeys =
    [
        NoticeClosedUserKey,
        PushAskClosedKey,
        GeoAskClosedKey,
    ];

    private static readonly string[] LocalKeys =
    [
        ScreenLockedKey,
        NoticeDismissedKey,
        PinnedTabsKey,
        SidebarWidthKey,
        PushAskNeverKey,
        PushAskSnoozeKey,
        GeoAskNeverKey,
        GeoSyncedAtKey,
        FabPositionKey,
        FabHiddenKey,
        BottomNavHiddenKey,
        BottomNavItemsKey,
        ToastPositionKey,
        GeoSyncIntervalKey,
        ZoomUnlockedKey,
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
            Session = SessionKeys,
            Local = LocalKeys,
        };

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

    /// <summary>
    /// 워터마크를 <b>지금</b> 걸거나 걷는다. 이름이 <c>null</c> 이면 걷는다.
    ///
    /// <para>
    /// <see cref="UseWatermark"/> 와 갈라 둔 이유는 <b>끄는 길이 필요</b>해서다.
    /// 그쪽은 「읽기 왕복에 태울 이름」을 받아 두는 자리이고, 태워 보내는
    /// 쪽(theme.js)은 <b>이름이 있을 때만</b> 건다 — 이름이 안 실린 왕복이
    /// 방금 걸어 둔 것을 지우지 않게 하려고 그렇게 만들어 두었다. 그래서
    /// 그 길로는 「걷어라」를 말할 수 없다.
    /// </para>
    ///
    /// <para>
    /// 관리자가 설정을 바꾸면 셸이 이것을 부른다.
    /// </para>
    ///
    /// <para>
    /// <b>담아 둔 이름도 함께 고친다.</b> 그러지 않으면 아직 안 나간 읽기
    /// 왕복이 <see cref="UseWatermark"/> 로 받아 둔 옛 이름을 그대로 싣고
    /// 나가서, <b>방금 내린 「걷어라」를 덮어쓴다.</b> 실제로 그랬다 —
    /// 워터마크를 끈 계정이 로그인하면 한 번 걸렸다가 30초 뒤(셸의 되묻는
    /// 시계) 사라졌다. 셸은 부트스트랩이 통에 맞으면 양보 없이 여기까지
    /// 오므로, 걷는 호출이 <b>거는 왕복보다 먼저</b> 나간 것이다.
    /// </para>
    /// </summary>
    public Task ApplyWatermarkAsync(string? name)
    {
        var wanted = string.IsNullOrWhiteSpace(name) ? null : name;

        _watermark = wanted;
        _watermarkAsked = true;

        if (wanted is null)
        {
            // 걷는 것은 <b>언제나 지금 부른다.</b> 읽기가 아직이라도 그렇다 —
            // 업무를 옮기면 이 통은 새로 생기지만(scoped) 화면에 걸린 것은
            // 그대로 남아 있어서, 「읽기가 태워 줄 것」에 맡기면 걷히지 않는다
            // (theme.js 는 <b>거는 것만</b> 한다). 프리렌더 중이면 호출이
            // 실패하지만 그때는 걸린 것도 없고, 위에서 이름을 지웠으므로
            // 뒤따르는 읽기가 다시 걸지 않는다.
            return HideWatermarkLateAsync();
        }

        // 거는 쪽은 읽기가 아직이면 맡긴다 — 왕복 하나를 아낀다.
        return _reading is null ? Task.CompletedTask : ShowWatermarkLateAsync(wanted);
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

    private async Task HideWatermarkLateAsync()
    {
        try
        {
            await js.InvokeVoidAsync("jsiniWatermark.hide");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "워터마크를 걷지 못했다.");
        }
    }

    /// <summary>
    /// 설치·구독 권유 창을 <paramref name="span"/> 동안 접어 둔다. 지난 뒤에는
    /// 다시 뜬다 — 휴대폰에서 권유를 되풀이하는 길이 이것이다
    /// (<see cref="PushAskSnoozeKey"/>).
    /// </summary>
    /// <returns>
    /// 적어 둔 기한. <b>돌려주는 이유는 부르는 쪽이 그때까지 기다리기</b>
    /// 때문이다 — 저장에 실패해도 이 탭에서는 그 시각을 지킨다.
    /// </returns>
    public async Task<DateTime> SnoozePushAskAsync(TimeSpan span)
    {
        var until = DateTime.UtcNow.Add(span);

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", PushAskSnoozeKey,
                until.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // 사생활 보호 모드에서는 setItem 이 던진다. 이 탭에서만 기한을
            // 지키게 되는 것이 전부라 사용자에게 말할 일이 아니다.
            logger.LogDebug(ex, "설치·구독 권유를 접어 둘 기한을 남기지 못했다.");
        }

        return until;
    }

    /// <summary>모바일 메뉴 단추(FAB) 위치가 바뀌었을 때 알린다.</summary>
    public event Action<string>? FabPositionChanged;

    /// <summary>
    /// 모바일 메뉴 단추(FAB) 위치를 저장하고 화면에 알린다.
    /// </summary>
    public async Task SetFabPositionAsync(string position)
    {
        var normalized = NormalizeFabPosition(position);

        try
        {
            // **열쇠와 값 둘뿐이다.** 한동안 그 사이에 토스트 열쇠가 끼어 있어
            // (인자 셋) 저장되는 값이 `jsini-toast-position` 이라는 **글자**였다 —
            // 다음에 읽으면 아는 자리가 아니라서 기본값으로 되돌아갔고, 고른
            // 자리가 새로고침마다 사라졌다.
            await js.InvokeVoidAsync("localStorage.setItem", FabPositionKey, normalized);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "모바일 메뉴 단추 위치를 브라우저에 저장하지 못했다.");
        }

        FabPositionChanged?.Invoke(normalized);
    }

    /// <summary>모바일 메뉴 단추를 감출지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? FabHiddenChanged;

    /// <summary>
    /// 모바일 메뉴 단추(FAB)를 감출지를 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <b>끌 때는 열쇠를 지운다.</b> <c>"0"</c> 을 적어 두면 읽는 쪽의
    /// 「있으면 그렇다는 뜻」 판정(<see cref="BrowserState.From"/>)에 걸려
    /// <b>꺼 둔 것이 켜 둔 것으로 읽힌다.</b>
    /// </remarks>
    public async Task SetFabHiddenAsync(bool hidden)
    {
        try
        {
            if (hidden)
            {
                await js.InvokeVoidAsync("localStorage.setItem", FabHiddenKey, "1");
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", FabHiddenKey);
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "모바일 메뉴 단추 숨김 여부를 브라우저에 저장하지 못했다.");
        }

        FabHiddenChanged?.Invoke(hidden);
    }

    /// <summary>확대 잠금을 풀지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? ZoomUnlockedChanged;

    /// <summary>
    /// 휴대폰 확대 잠금을 <b>풀지</b>를 저장하고, <b>지금 화면에도 곧바로 바른다</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 다시 잠글 때는 <b>열쇠를 지운다</b> — 잠금이 기본이라 적어 둘 것이 없다
    /// (<see cref="ZoomUnlockedKey"/>).
    /// </para>
    /// <para>
    /// <b>저장만 하면 안 된다.</b> <c>js/zoom.js</c> 는 문서가 열릴 때 한 번
    /// 읽으므로, 여기서 <c>jsiniZoom.lock</c> 을 부르지 않으면 <b>새로고침하기
    /// 전까지 스위치가 아무 일도 안 한 것으로 보인다.</b> 넓은 화면에서는 그
    /// 함수가 스스로 「걸지 않는다」로 판단하므로 여기서 화면 크기를 따지지 않는다.
    /// </para>
    /// </remarks>
    public async Task SetZoomUnlockedAsync(bool unlocked)
    {
        try
        {
            if (unlocked)
            {
                await js.InvokeVoidAsync("localStorage.setItem", ZoomUnlockedKey, "1");
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", ZoomUnlockedKey);
            }

            await js.InvokeVoidAsync("jsiniZoom.lock", !unlocked);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "화면 확대 잠금 여부를 브라우저에 반영하지 못했다.");
        }

        ZoomUnlockedChanged?.Invoke(unlocked);
    }

    /// <summary>아래 띠를 쓸지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? BottomNavHiddenChanged;

    /// <summary>
    /// 휴대폰 아래 띠를 <b>쓰지 않을지</b>를 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// 다시 쓰기로 할 때는 <b>열쇠를 지운다.</b> <c>"0"</c> 을 적으면 읽는 쪽의
    /// 「있으면 그렇다는 뜻」 판정에 걸려 <b>쓰기로 한 것이 안 쓰는 것으로
    /// 읽힌다</b>(<see cref="BottomNavHiddenKey"/>).
    /// </remarks>
    public async Task SetBottomNavHiddenAsync(bool hidden)
    {
        try
        {
            if (hidden)
            {
                await js.InvokeVoidAsync("localStorage.setItem", BottomNavHiddenKey, "1");
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", BottomNavHiddenKey);
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "하단 네비게이션 사용 여부를 브라우저에 저장하지 못했다.");
        }

        BottomNavHiddenChanged?.Invoke(hidden);
    }

    /// <summary>띠에 놓을 칸이 바뀌었을 때 알린다. 날것 JSON 을 그대로 준다.</summary>
    public event Action<string?>? BottomNavItemsChanged;

    /// <summary>
    /// 띠에 놓을 칸들을 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <paramref name="json"/> 이 비면 <b>열쇠를 지운다</b> — 「기본값으로
    /// 되돌리기」가 그 길이다. 빈 배열(<c>[]</c>)을 적어 두면 칸이 하나도 없는
    /// 빈 띠가 남고, 그 자리에서 사람은 고장으로 읽는다.
    /// </remarks>
    public async Task SetBottomNavItemsAsync(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json) ? null : json;

        try
        {
            if (value is null)
            {
                await js.InvokeVoidAsync("localStorage.removeItem", BottomNavItemsKey);
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.setItem", BottomNavItemsKey, value);
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "하단 네비게이션 항목을 브라우저에 저장하지 못했다.");
        }

        BottomNavItemsChanged?.Invoke(value);
    }

    /// <summary>토스트 알림 위치가 바뀌었을 때 알린다.</summary>
    public event Action<string>? ToastPositionChanged;

    /// <summary>
    /// 토스트 알림 위치를 저장하고 화면에 알린다.
    /// </summary>
    public async Task SetToastPositionAsync(string position)
    {
        var normalized = NormalizeToastPosition(position);

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", ToastPositionKey, normalized);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "토스트 알림 위치를 브라우저에 저장하지 못했다.");
        }

        ToastPositionChanged?.Invoke(normalized);
    }

    /// <summary>
    /// 토스트 알림 위치 식별자를 검증하고 표준화한다.
    /// 알 수 없는 값이면 기본값인 <c>bottom-right</c> 다.
    /// </summary>
    public static string NormalizeToastPosition(string? position) => position switch
    {
        "top-left" => "top-left",
        "top-center" => "top-center",
        "top-right" => "top-right",
        "bottom-left" => "bottom-left",
        "bottom-center" => "bottom-center",
        "bottom-right" => "bottom-right",
        _ => "bottom-right",
    };

    /// <summary>위치 확인 간격이 바뀌었을 때 알린다.</summary>
    /// <remarks>
    /// <b>듣는 쪽이 아직 없다.</b> 간격을 보는 곳(<c>LocationAskPopup</c>)은 화면을
    /// 옮길 때마다 새로 생겨 그때 읽으므로, 고친 값은 <b>다음 확인부터</b> 듣는다.
    /// 그래도 알림은 남겨 둔다 — 같은 화면에 간격을 보여 주는 자리가 생기면
    /// 그때 이것을 듣는다.
    /// </remarks>
    public event Action<int>? GeoSyncIntervalChanged;

    /// <summary>
    /// 위치를 저절로 다시 확인하는 간격(분)을 저장한다.
    /// </summary>
    public async Task SetGeoSyncIntervalAsync(int minutes)
    {
        var normalized = NormalizeGeoSyncMinutes(minutes);

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", GeoSyncIntervalKey,
                normalized.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "위치 확인 간격을 브라우저에 저장하지 못했다.");
        }

        GeoSyncIntervalChanged?.Invoke(normalized);
    }

    /// <summary>
    /// 고를 수 있는 확인 간격(분). <b>화면과 판정이 같은 목록을 본다</b> —
    /// 갈라 두면 화면에만 있는 값을 골랐을 때 조용히 기본값으로 돌아간다.
    /// </summary>
    /// <remarks>
    /// 30분보다 촘촘하게는 두지 않는다. 확인 한 번마다 게이트웨이 왕복과 GPS 가
    /// 붙고, 휴대폰에서는 그때마다 배터리를 쓴다. 반대 끝을 여섯 시간으로 둔 것은
    /// <b>하루에 두어 번이면 되는 사람</b>(한자리에서 일하는 사람)이 있기 때문이다.
    /// </remarks>
    public static readonly int[] GeoSyncMinuteChoices = [30, 60, 120, 180, 360];

    /// <summary>
    /// 확인 간격을 고를 수 있는 값으로 다듬는다. 모르는 값이면
    /// <see cref="DefaultGeoSyncMinutes"/> 다.
    /// </summary>
    public static int NormalizeGeoSyncMinutes(int? minutes) =>
        minutes is { } m && Array.IndexOf(GeoSyncMinuteChoices, m) >= 0
            ? m
            : DefaultGeoSyncMinutes;

    /// <summary>
    /// 모바일 메뉴 단추 위치 식별자를 검증하고 표준화한다.
    /// 알 수 없는 값이면 기본값인 <c>bottom-left</c> 다.
    /// </summary>
    public static string NormalizeFabPosition(string? position) => position switch
    {
        "top-left" => "top-left",
        "top-right" => "top-right",
        "bottom-right" => "bottom-right",
        _ => "bottom-left",
    };

    // ── 오가는 모양 ───────────────────────────────────────────

    /// <summary>theme.js 의 <c>jsiniBoot.read</c> 에 넘기는 것.</summary>
    private sealed class BootRequest
    {
        public string? Watermark { get; init; }
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

        /// <summary>
        /// 로그인 뒤 공지를 이 탭에서 닫았는가.
        ///
        /// <para>
        /// 공개 공지 쪽에는 짝이 없다 — 그 값을 보는 로그인 화면에 회로가
        /// 없어서 이 왕복에 실리지 않는다(<see cref="NoticeClosedPublicKey"/>).
        /// </para>
        /// </summary>
        public bool NoticeClosed { get; private init; }

        /// <summary>「오늘 하루 보지 않기」로 적어 둔 것. 날것 JSON 이고 없으면 <c>null</c>.</summary>
        public string? NoticeDismissedJson { get; private init; }

        /// <summary>구독 권유 창을 이 탭에서 닫았는가(「나중에」).</summary>
        public bool PushAskClosed { get; private init; }

        /// <summary>그 창을 이 브라우저에서 영영 안 보기로 했는가.</summary>
        public bool PushAskNever { get; private init; }

        /// <summary>
        /// 휴대폰에서 그 창을 <b>언제까지 접어 두기로</b> 했는가(UTC).
        /// 적어 둔 적이 없거나 읽을 수 없으면 <c>null</c> — 즉 지금 물어도 된다.
        /// </summary>
        public DateTime? PushAskSnoozedUntil { get; private init; }

        /// <summary>위치 권유 창을 이 탭에서 닫았는가(「나중에」).</summary>
        public bool GeoAskClosed { get; private init; }

        /// <summary>그 창을 이 브라우저에서 영영 안 보기로 했는가.</summary>
        public bool GeoAskNever { get; private init; }

        /// <summary>
        /// 이 브라우저에서 위치 일을 마지막으로 다룬 때(UTC). 없거나 읽을 수
        /// 없으면 <c>null</c> 이고, 그때는 <b>한 번도 안 한 것</b>으로 본다.
        /// </summary>
        public DateTime? GeoSyncedAt { get; private init; }

        /// <summary>고정해 둔 탭. 날것 JSON 이고 없으면 <c>null</c>.</summary>
        public string? PinnedTabsJson { get; private init; }

        /// <summary>
        /// 끌어 넓혀 둔 사이드바 폭(px). 없거나 읽을 수 없으면 <c>null</c> 이고,
        /// 그때는 기본 폭을 쓴다.
        ///
        /// <para>
        /// <b>여기서 숫자로 옮긴다.</b> 부품이 글자를 받아 각자 파싱하면
        /// 이상한 값(사람이 저장소를 고친 경우)을 어떻게 다룰지가 부품마다
        /// 갈린다 — 못 읽으면 없는 것으로 본다.
        /// </para>
        /// </summary>
        public int? SidebarWidthPx { get; private init; }

        /// <summary>지금 고른 테마. theme.js 가 안 실렸으면 <c>null</c>.</summary>
        public ThemeWire? Theme { get; private init; }

        /// <summary>
        /// 모바일 메뉴 단추(FAB) 위치. 없거나 잘못된 값이면 <c>bottom-left</c> 다.
        /// </summary>
        public string FabPosition { get; private init; } = "bottom-left";

        /// <summary>
        /// 모바일 메뉴 단추를 감춰 두었는가. 고른 적이 없으면 <c>false</c> —
        /// 즉 보인다.
        /// </summary>
        public bool FabHidden { get; private init; }

        /// <summary>
        /// 휴대폰 아래 띠를 <b>쓰지 않기로</b> 했는가. 고른 적이 없으면
        /// <c>false</c> — 즉 쓴다.
        /// </summary>
        public bool BottomNavHidden { get; private init; }

        /// <summary>
        /// 그 띠에 놓을 칸들. 날것 JSON 이고 고른 적이 없으면 <c>null</c> 이다.
        /// 옮겨 담는 일은 <see cref="BottomNav.Parse"/> 가 한다.
        /// </summary>
        public string? BottomNavItemsJson { get; private init; }

        /// <summary>
        /// 토스트 알림 위치. 없거나 잘못된 값이면 <c>bottom-right</c> 다.
        /// </summary>
        public string ToastPosition { get; private init; } = "bottom-right";

        /// <summary>
        /// 위치를 저절로 다시 확인하는 간격(분). 고른 적이 없으면
        /// <see cref="DefaultGeoSyncMinutes"/> 다.
        /// </summary>
        public int GeoSyncMinutes { get; private init; } = DefaultGeoSyncMinutes;

        /// <summary>그 간격을 <see cref="TimeSpan"/> 으로. 판정하는 쪽이 이것을 쓴다.</summary>
        public TimeSpan GeoSyncInterval => TimeSpan.FromMinutes(GeoSyncMinutes);

        /// <summary>
        /// 휴대폰 확대 잠금을 <b>풀어 두었는가</b>. 고른 적이 없으면
        /// <c>false</c> — 즉 잠근다.
        /// </summary>
        public bool ZoomUnlocked { get; private init; }

        internal static BrowserState From(BootWire wire) => new()
        {
            // 값이 "1" 이든 무엇이든 **있으면 그렇다는 뜻**이다. 옛 코드가
            // 잠금은 "1" 로만 인정하고 공지는 길이만 보았는데, 굽는 곳이
            // 우리뿐이라 둘을 가릴 이유가 없었다.
            ScreenLocked = Has(wire.Local, ScreenLockedKey),
            NoticeClosed = Has(wire.Session, NoticeClosedUserKey),
            NoticeDismissedJson = Get(wire.Local, NoticeDismissedKey),
            PushAskClosed = Has(wire.Session, PushAskClosedKey),
            PushAskNever = Has(wire.Local, PushAskNeverKey),
            PushAskSnoozedUntil = Moment(Get(wire.Local, PushAskSnoozeKey)),
            GeoAskClosed = Has(wire.Session, GeoAskClosedKey),
            GeoAskNever = Has(wire.Local, GeoAskNeverKey),
            GeoSyncedAt = Moment(Get(wire.Local, GeoSyncedAtKey)),
            PinnedTabsJson = Get(wire.Local, PinnedTabsKey),
            SidebarWidthPx = Pixels(Get(wire.Local, SidebarWidthKey)),
            Theme = wire.Theme,
            FabPosition = NormalizeFabPosition(Get(wire.Local, FabPositionKey)),
            FabHidden = Has(wire.Local, FabHiddenKey),
            BottomNavHidden = Has(wire.Local, BottomNavHiddenKey),
            BottomNavItemsJson = Get(wire.Local, BottomNavItemsKey),
            ToastPosition = NormalizeToastPosition(Get(wire.Local, ToastPositionKey)),
            GeoSyncMinutes = NormalizeGeoSyncMinutes(Minutes(Get(wire.Local, GeoSyncIntervalKey))),
            ZoomUnlocked = Has(wire.Local, ZoomUnlockedKey),
        };

        /// <summary>
        /// 저장해 둔 시각을 <b>UTC</b> 로. 이상한 값이면 <c>null</c> 이다.
        ///
        /// <para>
        /// <c>AdjustToUniversal</c> 을 빠뜨리면 안 된다 — 적어 둔 글자는 UTC 인데
        /// 파서는 기본으로 <b>기기 시간대의 시각</b>으로 풀어 놓는다. 그것을 UTC
        /// 「지금」과 빼면 <b>시차만큼(우리는 9시간) 어긋난다</b> — 한 시간 문턱이
        /// 아예 안 오거나 매번 지난 것이 된다.
        /// (<c>RoundtripKind</c> 와는 함께 못 쓴다. 그 짝은 예외를 던진다.)
        /// </para>
        /// </summary>
        private static DateTime? Moment(string? value) =>
            DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal
                | System.Globalization.DateTimeStyles.AssumeUniversal, out var at)
                ? at
                : null;

        /// <summary>
        /// 저장해 둔 간격을 숫자로. 이상한 값이면 <c>null</c> 이고, 그때는
        /// <see cref="NormalizeGeoSyncMinutes"/> 가 기본값으로 되돌린다.
        /// </summary>
        private static int? Minutes(string? value) =>
            int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var minutes)
                ? minutes
                : null;

        /// <summary>저장해 둔 폭을 숫자로. 이상한 값이면 <c>null</c>.</summary>
        private static int? Pixels(string? value) =>
            int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var px) && px > 0
                ? px
                : null;

        private static bool Has(Dictionary<string, string?> from, string key) =>
            Get(from, key) is { Length: > 0 };

        private static string? Get(Dictionary<string, string?> from, string key) =>
            from.TryGetValue(key, out var value) ? value : null;
    }
}
