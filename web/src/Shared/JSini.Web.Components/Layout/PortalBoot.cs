using System.Text.Json;
using JSini.Web.Components.Settings;
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
public sealed class PortalBoot(IJSRuntime js, PortalPrefs prefs, ILogger<PortalBoot> logger)
{
    // ── 브라우저에 안 남는 다섯 ───────────────────────────────
    //
    // 아래 열쇠 목록 중 다섯은 이제 **계정에 담긴다**(`PortalPrefs`) —
    // 휴대폰 아래 띠(쓸지 · 무엇을 놓을지) · 바로가기 단추 둘의 사용 여부 ·
    // 토스트 자리. 기기마다 다른 띠가 뜨면 안 된다는 것이 그 다섯의 요구다.
    //
    // **그래도 여기 열쇠가 남아 있다.** 읽기 목록(`LocalKeys`)에서 빼지 않는
    // 까닭은 **옛 값을 한 번 들고 올라가야** 하기 때문이다 — 사람들이 이미
    // 브라우저에서 골라 둔 것이 있고, 그것을 버리면 쓰던 띠가 어느 날
    // 기본값으로 돌아간다. 올리고 나면 그 자리에서 지운다
    // (`AdoptBrowserPrefsAsync`).
    //
    // 읽는 쪽은 아무것도 달라지지 않는다 — `BrowserState` 의 같은 칸을 보고,
    // 그 칸을 채우는 곳만 저장소에서 계정으로 옮겼다.
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
    /// 데스크탑 환경에서 끌어 넓혀 둔 오른쪽 서랍(<see cref="RightSideDrawer"/>) 폭(px).
    /// 다음에 열 때도 기억된 창 너비로 열리도록 브라우저와 계정에 함께 보존한다.
    /// </summary>
    public const string RightDrawerWidthKey = "jsini_right_drawer_width";

    /// <summary>
    /// 빠른 지시 서랍(<c>jsini-quick-ask-drawer</c>)의 개별 저장 너비 열쇠.
    /// </summary>
    public const string QuickAskDrawerWidthKey = "jsini_drawer_width_jsini-quick-ask-drawer";

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
    /// <b>한동안 이 브라우저의 것이었다</b> — 「단추가 무엇을 가리는지는 화면
    /// 크기에 달렸다」는 셈이었다. 그런데 가려지는 것은 <b>자리</b>의 문제이고,
    /// <b>쓸 것인가</b>는 그 사람의 뜻이다. 휴대폰 둘을 번갈아 쓰는 사람이
    /// 양쪽에서 똑같이 감추고 다니는 일이 실제로 났다.
    /// </para>
    /// <para>
    /// <b>이 열쇠는 더 이상 쓰이지 않는다 — 값이 계정으로 올라갔다</b>
    /// (<see cref="PortalPrefs.FabHiddenKey"/>). 여기 남아 있는 것은 <b>옛 값을 한 번
    /// 들고 올라가기 위해서</b>이고, 올린 뒤에는 그 자리에서 지운다
    /// (<see cref="MovedToAccountKeys"/>). 까닭은 <see cref="PortalPrefs"/> 머리말.
    /// </para>
    /// <para>
    /// <b>감추면 헤더의 ☰ 가 휴대폰에서 되살아난다</b>(app.css). 휴대폰에서는
    /// 하는 일이 똑같다는 이유로 그 ☰ 를 빼고 이 단추만 남겨 두었기 때문에,
    /// 그냥 감추기만 하면 <b>메뉴를 열 길이 하나도 없어진다.</b>
    /// </para>
    /// </remarks>
    public const string FabHiddenKey = "jsini-fab-hidden";

    /// <summary>
    /// 헬프데스크 <b>요청 등록</b> 단추의 위치.
    /// <c>bottom-right</c>(기본), <c>bottom-left</c>, <c>top-left</c>, <c>top-right</c>.
    /// </summary>
    /// <remarks>
    /// <b>기본이 메뉴 단추와 반대 귀퉁이다.</b> 둘 다 왼쪽 아래로 두면 한 번도
    /// 고치지 않은 사람에게 동그라미 둘이 포개져 나온다 — 겹치지 않게 세로로
    /// 미는 규칙은 있지만(<c>.jsini-shell__fab--stacked</c>) 그것은 <b>같은
    /// 귀퉁이를 일부러 고른 사람</b>을 위한 것이고, 기본값끼리 부딪칠 까닭이 없다.
    /// </remarks>
    public const string HelpDeskFabPositionKey = "jsini-hdfab-position";

    /// <summary>
    /// 헬프데스크 <b>요청 등록</b> 단추를 <b>감출 것인가</b>. 열쇠가 있으면 감춘다 —
    /// 즉 <b>없는 것이 기본이고, 기본은 보인다</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="FabHiddenKey"/> 와 같은 꼴이다. 「감춘다」를 적는 이유도 같다 —
    /// 읽는 쪽의 판정이 「열쇠가 있으면 그렇다는 뜻」(<see cref="BrowserState.From"/>)
    /// 이라 「보인다」를 적으면 한 번도 안 고친 사람에게 단추가 사라진다.
    /// </para>
    /// <para>
    /// <b>이 열쇠가 권한을 대신하지 않는다.</b> 단추를 그릴지는
    /// <c>/helpdesk/request/new</c> 를 열 수 있는가로 먼저 갈리고
    /// (<c>MainLayout.HelpDeskFabAllowed</c>), 이 값은 <b>그 권한이 있는 사람이
    /// 그럼에도 안 보겠다</b>고 한 것만 뜻한다. 권한이 없으면 감추고 말고가 없다.
    /// </para>
    /// <para>
    /// <b>이 열쇠는 더 이상 쓰이지 않는다 — 값이 계정으로 올라갔다</b>
    /// (<see cref="PortalPrefs.HelpDeskFabHiddenKey"/>). 메뉴 단추와 같은
    /// 갈래다(<see cref="FabHiddenKey"/>) — 「쓸 것인가」는 사람의 뜻이고
    /// 「어느 귀퉁이에」만 기기의 것이다. 여기 남아 있는 것은 <b>옛 값을 한 번
    /// 들고 올라가기 위해서</b>이고, 올린 뒤에는 그 자리에서 지운다
    /// (<see cref="MovedToAccountKeys"/>).
    /// </para>
    /// </remarks>
    public const string HelpDeskFabHiddenKey = "jsini-hdfab-hidden";

    /// <summary>
    /// 헬프데스크 <b>요청 등록</b> 단추를 <b>넓은 화면에서도 띄울 것인가</b>.
    /// 열쇠가 있으면 띄운다 — 즉 <b>없는 것이 기본이고, 기본은 휴대폰에서만 뜬다</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 떠다니는 단추 둘은 app.css 의 <c>@media (max-width: 767px)</c> 안에서만
    /// 켜진다 — 휴대폰에서 손가락이 닿기 어려운 자리를 메우는 것이고, 책상에서는
    /// 헤더와 메뉴가 그 일을 하기 때문이다.
    /// </para>
    /// <para>
    /// <b>요청 등록만은 그 셈이 다르다.</b> 고장·문의를 적는 일은 자리에 앉아서도
    /// 생기고(전화를 받다가, 회의 중에) 메뉴를 열고 업무를 고르고 두 단계를 더
    /// 내려가야 닿는 것은 넓은 화면에서도 똑같다. 그래서 <b>물어서</b> 띄운다 —
    /// 늘 띄우면 책상에서 한 번도 안 쓰는 사람의 오른쪽 아래를 영영 먹는다.
    /// </para>
    /// <para>
    /// <b><see cref="HelpDeskFabHiddenKey"/> 가 이것보다 세다.</b> 감춰 둔 사람에게는
    /// 이 열쇠가 있어도 안 보인다(app.css 에서 감추기 규칙을 뒤에 둔다) — 「감췄다」는
    /// 넓든 좁든 안 보겠다는 말이다.
    /// </para>
    /// <para>
    /// <b>이 브라우저의 것이다</b> — 위 둘과 같은 갈래다. 책상 컴퓨터에서 띄운 것이
    /// 휴대폰에 따라가 봐야 거기서는 원래 떠 있다.
    /// </para>
    /// </remarks>
    public const string HelpDeskFabDesktopKey = "jsini-hdfab-desktop";

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
    /// <b>한동안 이 브라우저의 것이었다</b> — 「띠는 767px 아래에서만 보이므로
    /// 계정에 담아 봐야 큰 모니터에서는 쓰이지 않는다」는 셈이었고, 그것은
    /// <b>휴대폰이 한 대라는 전제</b>였다.
    /// </para>
    /// <para>
    /// <b>이 열쇠는 더 이상 쓰이지 않는다 — 값이 계정으로 올라갔다</b>
    /// (<see cref="PortalPrefs.BottomNavHiddenKey"/>). 여기 남아 있는 것은 <b>옛 값을 한 번
    /// 들고 올라가기 위해서</b>이고, 올린 뒤에는 그 자리에서 지운다
    /// (<see cref="MovedToAccountKeys"/>). 까닭은 <see cref="PortalPrefs"/> 머리말.
    /// </para>
    /// </remarks>
    public const string BottomNavHiddenKey = "jsini-bottomnav-hidden";

    /// <summary>
    /// 그 띠에 놓을 칸들(JSON). 없으면 <c>BottomNav.Defaults</c> 다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 모양과 기본값은 <see cref="BottomNav"/> 가 갖는다 — 여기는 <b>열쇠
    /// 이름만</b> 안다. 읽어 온 글자를 그대로 넘기는 이유는 이 통이 화면의
    /// 말(<c>BottomNavItem</c>)을 모르게 두려는 것이다.
    /// </para>
    /// <para>
    /// <b>이 열쇠는 더 이상 쓰이지 않는다 — 값이 계정으로 올라갔다</b>
    /// (<see cref="PortalPrefs.BottomNavItemsKey"/>). 여기 남아 있는 것은 <b>옛 값을 한 번
    /// 들고 올라가기 위해서</b>이고, 올린 뒤에는 그 자리에서 지운다
    /// (<see cref="MovedToAccountKeys"/>). 까닭은 <see cref="PortalPrefs"/> 머리말.
    /// </para>
    /// </remarks>
    public const string BottomNavItemsKey = "jsini-bottomnav-items";

    /// <summary>
    /// 토스트(화면에 뜨는 알림) 위치.
    /// <c>bottom-right</c>(기본) 외 여섯 자리 중 하나다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>한동안 이 브라우저의 것이었다.</b> 그런데 알림이 뜨는 자리는 화면
    /// 크기가 아니라 <b>그 사람이 화면을 쓰는 버릇</b>에서 나오는 값이다 —
    /// 오른손잡이가 오른쪽 아래를 엄지로 가리고 있느냐 같은 것. 그것은 기기를
    /// 바꿔도 안 바뀐다.
    /// </para>
    /// <para>
    /// <b>이 열쇠는 더 이상 쓰이지 않는다 — 값이 계정으로 올라갔다</b>
    /// (<see cref="PortalPrefs.ToastPositionKey"/>). 여기 남아 있는 것은 <b>옛 값을 한 번
    /// 들고 올라가기 위해서</b>이고, 올린 뒤에는 그 자리에서 지운다
    /// (<see cref="MovedToAccountKeys"/>). 까닭은 <see cref="PortalPrefs"/> 머리말.
    /// </para>
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
    /// 고르지 않았을 때의 확인 간격 — <b>30분</b>이다.
    /// </summary>
    /// <remarks>
    /// 한 시간이었다. 좌표가 「지금 어디」 한 줄이던 동안에는 그걸로 됐는데,
    /// 그 좌표가 <b>지나온 자리</b>로도 쌓이면서(<c>scom.location_tracks</c>)
    /// 한 시간은 너무 성글어졌다 — 점심에 나갔다 온 자리가 통째로 빠진다.
    /// 까닭은 <c>GeoLocator.SyncInterval</c> 의 머리말.
    /// </remarks>
    public const int DefaultGeoSyncMinutes = 30;

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
        RightDrawerWidthKey,
        QuickAskDrawerWidthKey,
        PushAskNeverKey,
        PushAskSnoozeKey,
        GeoAskNeverKey,
        GeoSyncedAtKey,
        FabPositionKey,
        FabHiddenKey,
        HelpDeskFabPositionKey,
        HelpDeskFabHiddenKey,
        HelpDeskFabDesktopKey,
        BottomNavHiddenKey,
        BottomNavItemsKey,
        ToastPositionKey,
        GeoSyncIntervalKey,
        ZoomUnlockedKey,
    ];

    /// <summary>
    /// 계정으로 올라간 다섯(<see cref="PortalPrefs"/>). <b>올린 뒤에는
    /// 브라우저에서 지운다</b> — 두 곳에 남겨 두면 어느 쪽이 참인지가
    /// 기기마다 갈린다.
    /// </summary>
    private static readonly string[] MovedToAccountKeys =
    [
        BottomNavHiddenKey,
        BottomNavItemsKey,
        FabHiddenKey,
        HelpDeskFabHiddenKey,
        ToastPositionKey,
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

    /// <summary>
    /// 읽어 둔 한 벌에 <b>방금 고친 값을 반영해 둔다.</b>
    /// </summary>
    /// <remarks>
    /// <b>아직 다 읽지 못했으면 아무 일도 하지 않는다.</b> 한 번도 안 읽었으면
    /// 다음 읽기가 브라우저에서 새 값을 그대로 가져오므로 할 일이 없고, 읽는
    /// 중이면 갈아 끼울 것이 아직 없다. 뒤엣것은 <b>첫 그림이 끝나기 전에
    /// 값을 고친</b> 경우라야 걸리는데, 그 자리에 조작이 없다.
    /// </remarks>
    /// <seealso cref="BrowserState.Patch"/>
    private void Remember(Func<BrowserState, BrowserState> change)
    {
        if (_reading is { IsCompletedSuccessfully: true } done)
        {
            _reading = Task.FromResult(change(done.Result));
        }
    }

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

            var state = wire is null ? BrowserState.Empty : BrowserState.From(wire);

            SeedGeoSync(state);

            // **다섯은 계정이 이긴다.** 브라우저에서 읽은 것은 옛 값이고,
            // 올라간 적이 없으면 여기서 한 번 올라간다(그 뒤에 브라우저 쪽을
            // 걷는다). 둘을 같이 두면 어느 쪽이 참인지가 기기마다 갈린다.
            return await WithAccountPrefsAsync(state);
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

            // **계정에 담긴 다섯은 그래도 읽는다.** 저장소를 막아 둔 브라우저
            // 에서도 아래 띠와 토스트 자리는 제 값으로 떠야 한다 — 그쪽은
            // 애초에 저장소에 기대지 않는다.
            return await WithAccountPrefsAsync(BrowserState.Empty);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // 모양이 어긋나거나 회로가 이미 끊긴 경우다. 위와 같이 다룬다.
            logger.LogDebug(ex, "브라우저 상태를 옮기지 못했다. 없는 것으로 본다.");
            return await WithAccountPrefsAsync(BrowserState.Empty);
        }
    }

    /// <summary>
    /// 브라우저에서 읽은 한 벌 위에 <b>계정에 담긴 다섯</b>을 얹는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 계정에 그 열쇠가 <b>없으면</b> 브라우저에 남아 있던 옛 값을 한 번
    /// 올리고(<see cref="PortalPrefs.AdoptFromBrowserAsync"/>) 올린 자리를
    /// 브라우저에서 걷는다. 안 걷으면 계정 쪽에서 기본값으로 되돌린 사람이
    /// <b>다음 로그인에 옛 값을 다시 받는다.</b>
    /// </para>
    /// <para>
    /// 올릴 것이 없으면 왕복도 없다 — 대부분의 로그인이 그렇다.
    /// </para>
    /// </remarks>
    private async Task<BrowserState> WithAccountPrefsAsync(BrowserState state)
    {
        var account = await prefs.AdoptFromBrowserAsync(
            state.BottomNavHidden,
            state.BottomNavItemsJson,
            state.FabHidden,
            state.HelpDeskFabHidden,
            state.ToastPositionStored);

        if (account is null)
        {
            account = await prefs.ReadAsync();
        }
        else
        {
            await ForgetMovedKeysAsync();
        }

        return state.WithAccountPrefs(account);
    }

    /// <summary>
    /// 계정으로 올린 다섯을 브라우저에서 지운다. 못 지워도 그냥 간다 —
    /// 다음 로그인에 다시 올라가려 해도 계정에 열쇠가 이미 있어서 안 올라간다.
    /// </summary>
    private async Task ForgetMovedKeysAsync()
    {
        foreach (var key in MovedToAccountKeys)
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.removeItem", key);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                logger.LogDebug(ex, "계정으로 옮긴 설정을 브라우저에서 지우지 못했다.");
                return;
            }
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

        Remember(state => state.WithFabPosition(normalized));
        FabPositionChanged?.Invoke(normalized);
    }

    /// <summary>모바일 메뉴 단추를 감출지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? FabHiddenChanged;

    /// <summary>
    /// 모바일 메뉴 단추(FAB)를 감출지를 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>계정에 담긴다</b>(<see cref="PortalPrefs.FabHiddenKey"/>) — 「이
    /// 단추를 쓸 것인가」는 기기가 아니라 사람의 뜻이라, 휴대폰에서 감춘
    /// 사람이 태블릿에서 다시 감추게 할 까닭이 없다. 자리(<see
    /// cref="SetFabPositionAsync"/>)는 그대로 브라우저에 남는다 — 그쪽은
    /// 손가락으로 끌어 옮기는 값이라 기기마다 다른 것이 맞다.
    /// </para>
    /// <para>
    /// <b>보이기로 할 때는 열쇠를 지운다</b>(<c>null</c> 을 보낸다). 거짓을
    /// 적어 두어도 읽히기는 하지만, 그러면 <b>아무것도 안 고친 사람</b>과
    /// <b>보이기를 고른 사람</b>이 담긴 모양으로 갈리지 않는다 — 옛 값을
    /// 들고 올릴지 말지를 그 갈림으로 판정한다.
    /// </para>
    /// </remarks>
    public async Task SetFabHiddenAsync(bool hidden)
    {
        Remember(state => state.WithFabHidden(hidden));
        FabHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.FabHiddenKey, hidden ? true : null);
    }

    /// <summary>헬프데스크 요청 등록 단추 위치가 바뀌었을 때 알린다.</summary>
    public event Action<string>? HelpDeskFabPositionChanged;

    /// <summary>
    /// 헬프데스크 요청 등록 단추 위치를 저장하고 화면에 알린다.
    /// </summary>
    public async Task SetHelpDeskFabPositionAsync(string position)
    {
        var normalized = NormalizeHelpDeskFabPosition(position);

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", HelpDeskFabPositionKey, normalized);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "헬프데스크 요청 등록 단추 위치를 브라우저에 저장하지 못했다.");
        }

        Remember(state => state.WithHelpDeskFabPosition(normalized));
        HelpDeskFabPositionChanged?.Invoke(normalized);
    }

    /// <summary>헬프데스크 요청 등록 단추를 감출지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? HelpDeskFabHiddenChanged;

    /// <summary>
    /// 헬프데스크 요청 등록 단추를 감출지를 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <b>계정에 담긴다</b>(<see cref="PortalPrefs.HelpDeskFabHiddenKey"/>) —
    /// 위 <see cref="SetFabHiddenAsync"/> 와 같은 꼴이고 까닭도 같다.
    /// 보이기로 할 때 열쇠를 지우는 것도 같다.
    /// </remarks>
    public async Task SetHelpDeskFabHiddenAsync(bool hidden)
    {
        Remember(state => state.WithHelpDeskFabHidden(hidden));
        HelpDeskFabHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.HelpDeskFabHiddenKey, hidden ? true : null);
    }

    /// <summary>요청 등록 단추를 넓은 화면에도 띄울지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? HelpDeskFabDesktopChanged;

    /// <summary>
    /// 요청 등록 단추를 <b>넓은 화면에도 띄울지</b>를 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <b>안 띄우기로 할 때는 열쇠를 지운다.</b> <c>"0"</c> 을 적어 두면 읽는 쪽의
    /// 「있으면 그렇다는 뜻」 판정(<see cref="BrowserState.From"/>)에 걸려
    /// <b>껐는데 켠 것으로 읽힌다</b>(<see cref="SetHelpDeskFabHiddenAsync"/> 와 같다).
    /// </remarks>
    public async Task SetHelpDeskFabDesktopAsync(bool shown)
    {
        try
        {
            if (shown)
            {
                await js.InvokeVoidAsync("localStorage.setItem", HelpDeskFabDesktopKey, "1");
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", HelpDeskFabDesktopKey);
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "헬프데스크 요청 등록 단추를 넓은 화면에 띄울지를 브라우저에 저장하지 못했다.");
        }

        Remember(state => state.WithHelpDeskFabDesktop(shown));
        HelpDeskFabDesktopChanged?.Invoke(shown);
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

        Remember(state => state.WithZoomUnlocked(unlocked));
        ZoomUnlockedChanged?.Invoke(unlocked);
    }

    /// <summary>아래 띠를 쓸지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? BottomNavHiddenChanged;

    /// <summary>
    /// 휴대폰 아래 띠를 <b>쓰지 않을지</b>를 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <b>계정에 담긴다</b>(<see cref="PortalPrefs.BottomNavHiddenKey"/>).
    /// 아래 <see cref="SetBottomNavItemsAsync"/> 와 한 묶음이라 담기는 곳도
    /// 같아야 한다 — 갈라 두면 <b>칸은 따라오는데 띠가 안 뜨는</b> 기기가 생긴다.
    /// 다시 쓰기로 할 때 열쇠를 지우는 까닭은 <see cref="SetFabHiddenAsync"/> 와 같다.
    /// </remarks>
    public async Task SetBottomNavHiddenAsync(bool hidden)
    {
        Remember(state => state.WithBottomNavHidden(hidden));
        BottomNavHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.BottomNavHiddenKey, hidden ? true : null);
    }

    /// <summary>띠의 이름을 감출지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? BottomNavLabelsHiddenChanged;

    /// <summary>
    /// 띠의 칸에서 <b>이름을 감출지</b>를 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>계정에 담긴다</b>(<see cref="PortalPrefs.BottomNavLabelsHiddenKey"/>).
    /// 위 <see cref="SetBottomNavHiddenAsync"/> 와 한 묶음이라 담기는 곳도 같아야
    /// 한다 — 갈라 두면 <b>띠는 따라오는데 생김새만 기기마다 다른</b> 자리가 생긴다.
    /// </para>
    /// <para>
    /// 이름을 다시 보이기로 하면 <b>열쇠를 지운다</b>(<c>null</c>). 거짓을 적어
    /// 두면 「한 번도 안 고친 사람」과 「보이기로 고른 사람」이 계정에서 갈려,
    /// 나중에 기본값을 바꿀 때 뒤엣사람만 옛 값에 붙박인다 —
    /// <see cref="SetFabHiddenAsync"/> 와 같은 까닭이다.
    /// </para>
    /// </remarks>
    public async Task SetBottomNavLabelsHiddenAsync(bool hidden)
    {
        Remember(state => state.WithBottomNavLabelsHidden(hidden));
        BottomNavLabelsHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.BottomNavLabelsHiddenKey, hidden ? true : null);
    }

    /// <summary>같은 칸들을 넓은 화면 헤더에 세울지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? BottomNavDesktopHiddenChanged;

    /// <summary>
    /// 띠에 고른 칸들을 <b>넓은 화면 헤더 오른쪽에 세우지 않을지</b>를 저장하고
    /// 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>계정에 담긴다</b>(<see cref="PortalPrefs.BottomNavDesktopHiddenKey"/>).
    /// 칸 목록과 한 묶음이라 담기는 곳도 같아야 한다 — 갈라 두면 <b>칸은 따라오는데
    /// 헤더만 기기마다 다른</b> 자리가 생긴다.
    /// </para>
    /// <para>
    /// 다시 세우기로 하면 <b>열쇠를 지운다</b>(<c>null</c>). 거짓을 적어 두면
    /// 「한 번도 안 고친 사람」과 「세우기로 고른 사람」이 계정에서 갈려, 나중에
    /// 기본값을 바꿀 때 뒤엣사람만 옛 값에 붙박인다 —
    /// <see cref="SetBottomNavLabelsHiddenAsync"/> 와 같은 까닭이다.
    /// </para>
    /// </remarks>
    public async Task SetBottomNavDesktopHiddenAsync(bool hidden)
    {
        Remember(state => state.WithBottomNavDesktopHidden(hidden));
        BottomNavDesktopHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.BottomNavDesktopHiddenKey, hidden ? true : null);
    }

    /// <summary>띠에 놓을 칸이 바뀌었을 때 알린다. 날것 JSON 을 그대로 준다.</summary>
    public event Action<string?>? BottomNavItemsChanged;

    /// <summary>
    /// 띠에 놓을 칸들을 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>계정에 담긴다</b>(<see cref="PortalPrefs.BottomNavItemsKey"/>).
    /// <b>이 값 때문에 다섯이 통째로 옮겨 왔다</b> — 띠에 무엇을 놓을지는
    /// 「내가 자주 쓰는 메뉴」를 고르는 일이고, 사람이 손에 드는 기기는
    /// 하나가 아니다. 브라우저에 두면 회사 휴대폰과 개인 휴대폰과 홈 화면에
    /// 깐 앱이 <b>서로 다른 띠</b>를 띄운다.
    /// </para>
    /// <para>
    /// <paramref name="json"/> 이 비면 <b>열쇠를 지운다</b> — 「기본값으로
    /// 되돌리기」가 그 길이다. 빈 배열(<c>[]</c>)을 적어 두면 칸이 하나도 없는
    /// 빈 띠가 남고, 그 자리에서 사람은 고장으로 읽는다.
    /// </para>
    /// <para>
    /// 담길 때는 <b>글자가 아니라 배열</b>이다. 글자로 싸서 담으면 DB 에서
    /// 눈으로 못 읽고, 읽는 쪽이 한 겹을 더 벗겨야 한다. 못 푸는 글자가
    /// 들어오면 <b>안 고친 것으로 본다</b> — 여기서 막지 않으면 모양이
    /// 아닌 것이 계정에 박혀 모든 기기의 띠가 함께 망가진다.
    /// </para>
    /// </remarks>
    public async Task SetBottomNavItemsAsync(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json) ? null : json;

        Remember(state => state.WithBottomNavItems(value));
        BottomNavItemsChanged?.Invoke(value);

        await prefs.SetAsync(PortalPrefs.BottomNavItemsKey, AsArray(value));
    }

    /// <summary>
    /// 날것 JSON 을 담을 수 있는 배열로. 비었거나 배열이 아니면 <c>null</c>
    /// 이고, 그때는 <see cref="PortalPrefs.SetAsync"/> 가 열쇠를 지운다.
    /// </summary>
    private static JsonElement? AsArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var element = JsonDocument.Parse(json).RootElement.Clone();
            return element.ValueKind == JsonValueKind.Array ? element : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>토스트 알림 위치가 바뀌었을 때 알린다.</summary>
    public event Action<string>? ToastPositionChanged;

    /// <summary>
    /// 토스트 알림 위치를 저장하고 화면에 알린다.
    /// </summary>
    /// <remarks>
    /// <b>계정에 담긴다</b>(<see cref="PortalPrefs.ToastPositionKey"/>).
    /// 알림이 뜨는 자리는 그 사람이 화면을 쓰는 버릇에서 나오는 값이라
    /// (오른손잡이가 오른쪽 아래를 가리고 있느냐) 기기를 가릴 까닭이 없다.
    /// <b>여기만 지우는 길이 없다</b> — 고르개에 「안 고름」이 없어서 늘
    /// 글자 하나가 담긴다.
    /// </remarks>
    public async Task SetToastPositionAsync(string position)
    {
        var normalized = NormalizeToastPosition(position);

        Remember(state => state.WithToastPosition(normalized));
        ToastPositionChanged?.Invoke(normalized);

        await prefs.SetAsync(PortalPrefs.ToastPositionKey, normalized);
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
        "top-center-1-5" => "top-center-1-5",
        "top-center-1-4" => "top-center-1-4",
        "top-center-1-3" => "top-center-1-3",
        "bottom-left" => "bottom-left",
        "bottom-center" => "bottom-center",
        "bottom-right" => "bottom-right",
        "bottom-center-2-3" => "bottom-center-2-3",
        "bottom-center-1-3" => "bottom-center-2-3",
        "bottom-center-1-4" => "bottom-center-1-4",
        "bottom-center-3-4" => "bottom-center-1-4",
        "bottom-center-1-5" => "bottom-center-1-5",
        "bottom-center-4-5" => "bottom-center-1-5",
        "center" => "center",
        _ => "bottom-right",
    };

    /// <summary>위치 확인 간격이 바뀌었을 때 알린다.</summary>
    /// <remarks>
    /// <b>듣는 쪽이 꼭 있어야 하는 것은 아니다.</b> 간격을 보는 곳
    /// (<c>LocationAskPopup</c> 의 시계)은 깰 때마다 <see cref="GeoSyncInterval"/>
    /// 을 다시 읽으므로, 듣지 않아도 <b>다음 확인부터</b> 새 간격을 따른다.
    /// 지금 자고 있는 시계를 <b>당장</b> 고쳐 걸고 싶은 쪽이 이것을 듣는다.
    /// </remarks>
    public event Action<int>? GeoSyncIntervalChanged;

    /// <summary>
    /// 위치를 저절로 다시 확인하는 간격(분)을 저장한다.
    /// </summary>
    public async Task SetGeoSyncIntervalAsync(int minutes)
    {
        var normalized = NormalizeGeoSyncMinutes(minutes);

        // **회로가 든 값을 먼저 고친다.** 브라우저에 쓰는 것이 실패해도
        // 이 탭에서 도는 시계는 방금 고른 간격을 따라야 한다.
        _geoSyncMinutes = normalized;
        _geoSeeded = true;

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

    // ── 위치를 마지막으로 다룬 때 ────────────────────────────────
    //
    // **읽기(`ReadAsync`)가 든 값은 회로가 붙던 순간의 것이다.** 몇 번을 불러도
    // 왕복은 한 번이라(머리말) 그 뒤에 찍은 표시는 거기 안 비친다. 전에는 보는
    // 쪽이 화면 전환마다 새로 생겨서 그 낡음이 드러나지 않았는데, 시계가 같은
    // 회로 안에서 되풀이 깨게 되면서 **「쟀는데도 영영 잴 때」** 가 된다.
    //
    // 그래서 「마지막으로 다룬 때」와 「간격」은 **여기가 들고 있는다.**

    private DateTime? _geoSyncedAt;
    private int? _geoSyncMinutes;
    private bool _geoSeeded;

    /// <summary>
    /// 이 브라우저에서 위치를 마지막으로 다룬 때(UTC). 한 번도 안 다뤘으면
    /// <c>null</c> 이다. <see cref="ReadAsync"/> 가 심고 <see cref="StampGeoSyncAsync"/>
    /// 가 고친다.
    /// </summary>
    public DateTime? GeoSyncedAt => _geoSyncedAt;

    /// <summary>
    /// 지금 쓰는 확인 간격. 고른 적이 없으면 <see cref="DefaultGeoSyncMinutes"/> 다.
    /// </summary>
    public TimeSpan GeoSyncInterval =>
        TimeSpan.FromMinutes(_geoSyncMinutes ?? DefaultGeoSyncMinutes);

    /// <summary>
    /// 읽어 온 상태에서 위 둘을 심는다. <b>이미 심었으면 건드리지 않는다</b> —
    /// 읽기가 늦게 끝나는 사이에 간격을 고쳤거나 좌표를 쟀을 수 있고, 그때
    /// 덮어쓰면 방금 한 일이 없던 일이 된다.
    /// </summary>
    private void SeedGeoSync(BrowserState state)
    {
        if (_geoSeeded)
        {
            return;
        }

        _geoSeeded = true;
        _geoSyncedAt = state.GeoSyncedAt;
        _geoSyncMinutes = state.GeoSyncMinutes;
    }

    /// <summary>
    /// 방금 다뤘다고 브라우저에 적어 둔다. <b>UTC 로 적는다</b> — 읽는 쪽이
    /// UTC 「지금」과 빼서 문턱을 잰다(<see cref="GeoSyncedAtKey"/>).
    /// </summary>
    /// <remarks>
    /// 글자를 굽는 자리를 여기 둔 까닭은 <b>열쇠와 꼴을 아는 곳을 하나로</b>
    /// 두기 위해서다(<see cref="SnoozePushAskAsync"/> 와 같은 꼴). 못 적어도
    /// 회로가 든 값은 고친다 — 사생활 보호 모드에서 <c>setItem</c> 이 던지는데,
    /// 그때 시계까지 멈추면 그 브라우저는 <b>잴 때마다 쟀다는 것을 잊는다</b>.
    /// </remarks>
    public async Task<DateTime> StampGeoSyncAsync()
    {
        var now = DateTime.UtcNow;

        _geoSyncedAt = now;
        _geoSeeded = true;

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", GeoSyncedAtKey,
                now.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "위치를 확인한 때를 브라우저에 적지 못했다.");
        }

        return now;
    }

    /// <summary>
    /// 그 표시를 <b>브라우저에서 다시</b> 읽는다. 못 읽으면 회로가 든 값이다.
    /// </summary>
    /// <remarks>
    /// <b>탭이 둘일 때를 위한 것이다.</b> 표시는 <c>localStorage</c> 에 있어
    /// 탭끼리 나누어 쓰지만 회로는 탭마다 따로 돈다 — 옆 탭이 방금 쟀는데도
    /// 이 탭의 시계가 또 재면 사람 하나가 간격마다 둘씩 쌓는다.
    /// <para>
    /// <b>잴 때가 됐을 때만 부른다.</b> 왕복 하나를 내는 길이라, 아직 멀었으면
    /// 회로가 든 값으로 먼저 거른다.
    /// </para>
    /// </remarks>
    public async Task<DateTime?> ReadGeoSyncedAtAsync()
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", GeoSyncedAtKey);

            if (BrowserState.Moment(raw) is { } at)
            {
                _geoSyncedAt = at;
                _geoSeeded = true;
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "위치를 확인한 때를 다시 읽지 못했다.");
        }

        return _geoSyncedAt;
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
    public static string NormalizeFabPosition(string? position) =>
        NormalizeCorner(position, "bottom-left");

    /// <summary>
    /// 헬프데스크 요청 등록 단추 위치를 검증하고 표준화한다.
    /// 알 수 없는 값이면 기본값인 <c>bottom-right</c> 다.
    /// </summary>
    /// <remarks>
    /// 고를 수 있는 네 귀퉁이는 메뉴 단추와 같고 <b>기본값만 다르다</b>
    /// (<see cref="HelpDeskFabPositionKey"/> 머리말). 그래서 아는 값의 목록을
    /// <see cref="NormalizeCorner"/> 한 자리에 둔다 — 갈라 두면 한쪽에만 귀퉁이를
    /// 더하는 날 그 값이 다른 쪽에서 조용히 기본값으로 되돌아간다.
    /// </remarks>
    public static string NormalizeHelpDeskFabPosition(string? position) =>
        NormalizeCorner(position, "bottom-right");

    /// <summary>떠다니는 단추가 설 수 있는 네 귀퉁이. 모르는 값이면 <paramref name="fallback"/> 이다.</summary>
    private static string NormalizeCorner(string? position, string fallback) => position switch
    {
        "top-left" => "top-left",
        "top-right" => "top-right",
        "bottom-right" => "bottom-right",
        "center" => "center",
        "bottom-left" => "bottom-left",
        _ => fallback,
    };

    /// <summary>오른쪽 서랍 너비가 바뀌었을 때 알린다.</summary>
    public event Action<int>? RightDrawerWidthChanged;

    /// <summary>
    /// 데스크탑 환경에서 조절한 오른쪽 서랍 너비(px)를 브라우저와 계정에 함께 저장하고 알린다.
    /// </summary>
    public async Task SetRightDrawerWidthAsync(int widthPx)
    {
        var clamped = Math.Clamp(widthPx, 300, 1200);
        try
        {
            var raw = clamped.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await js.InvokeVoidAsync("localStorage.setItem", RightDrawerWidthKey, raw);
            await js.InvokeVoidAsync("localStorage.setItem", QuickAskDrawerWidthKey, raw);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "오른쪽 서랍 너비를 브라우저에 저장하지 못했다.");
        }

        Remember(state => state.WithRightDrawerWidth(clamped));
        RightDrawerWidthChanged?.Invoke(clamped);

        await prefs.SetAsync(PortalPrefs.RightDrawerWidthKey, clamped);
    }

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
        public string? Mode { get; init; }
        public string? Color { get; init; }
        public string? Base { get; init; }
        public string? Radius { get; init; }
        public string? Size { get; init; }
        public string? Font { get; init; }
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

        /// <summary>
        /// 데스크탑에서 조절해 둔 오른쪽 서랍 폭(px). 없거나 읽을 수 없으면 <c>null</c> 이고,
        /// 그때는 기본 폭(416px)을 쓴다.
        /// </summary>
        public int? RightDrawerWidthPx { get; private set; }

        /// <summary>지금 고른 테마. theme.js 가 안 실렸으면 <c>null</c>.</summary>
        public ThemeWire? Theme { get; private init; }

        /// <summary>
        /// 모바일 메뉴 단추(FAB) 위치. 없거나 잘못된 값이면 <c>bottom-left</c> 다.
        /// </summary>
        public string FabPosition { get; private set; } = "bottom-left";

        /// <summary>
        /// 모바일 메뉴 단추를 감춰 두었는가. 고른 적이 없으면 <c>false</c> —
        /// 즉 보인다.
        /// </summary>
        public bool FabHidden { get; private set; }

        /// <summary>
        /// 헬프데스크 요청 등록 단추 위치. 없거나 잘못된 값이면 <c>bottom-right</c> 다.
        /// </summary>
        public string HelpDeskFabPosition { get; private set; } = "bottom-right";

        /// <summary>
        /// 그 단추를 감춰 두었는가. 고른 적이 없으면 <c>false</c> — 즉 보인다
        /// (권한이 있을 때에 한한다 — <see cref="HelpDeskFabHiddenKey"/> 머리말).
        /// </summary>
        public bool HelpDeskFabHidden { get; private set; }

        /// <summary>
        /// 그 단추를 <b>넓은 화면에도 띄우기로</b> 했는가. 고른 적이 없으면
        /// <c>false</c> — 즉 휴대폰에서만 뜬다(<see cref="HelpDeskFabDesktopKey"/>).
        /// </summary>
        public bool HelpDeskFabDesktop { get; private set; }

        /// <summary>
        /// 휴대폰 아래 띠를 <b>쓰지 않기로</b> 했는가. 고른 적이 없으면
        /// <c>false</c> — 즉 쓴다.
        /// </summary>
        public bool BottomNavHidden { get; private set; }

        /// <summary>
        /// 그 띠에 놓을 칸들. 날것 JSON 이고 고른 적이 없으면 <c>null</c> 이다.
        /// 옮겨 담는 일은 <see cref="BottomNav.Parse"/> 가 한다.
        /// </summary>
        public string? BottomNavItemsJson { get; private set; }

        /// <summary>
        /// 그 띠의 칸에서 <b>이름을 감추기로</b> 했는가(아이콘만 보기).
        /// 고른 적이 없으면 <c>false</c> — 즉 이름이 보인다.
        /// </summary>
        /// <remarks>
        /// <b>브라우저 열쇠가 없다.</b> 위 둘과 달리 이 값은 처음부터 계정에
        /// 났으므로(<see cref="PortalPrefs.BottomNavLabelsHiddenKey"/>) 여기에
        /// 실리는 길은 <see cref="WithAccountPrefs"/> 하나다 —
        /// <see cref="From"/> 이 이 칸을 안 건드리는 까닭이다.
        /// </remarks>
        public bool BottomNavLabelsHidden { get; private set; }

        /// <summary>
        /// 같은 칸들을 <b>넓은 화면 헤더에 세우지 않기로</b> 했는가.
        /// 고른 적이 없으면 <c>false</c> — 즉 선다.
        /// </summary>
        /// <remarks>
        /// <b>브라우저 열쇠가 없다.</b> 바로 위와 같이 처음부터 계정에 난 값이라
        /// (<see cref="PortalPrefs.BottomNavDesktopHiddenKey"/>) 여기에 실리는 길은
        /// <see cref="WithAccountPrefs"/> 하나다.
        /// </remarks>
        public bool BottomNavDesktopHidden { get; private set; }

        /// <summary>
        /// 토스트 알림 위치. 없거나 잘못된 값이면 <c>bottom-right</c> 다.
        /// </summary>
        public string ToastPosition { get; private set; } = "bottom-right";

        /// <summary>
        /// 브라우저에 <b>적혀 있던 그대로</b>의 토스트 자리. 적힌 적이 없으면
        /// <c>null</c> 이다.
        /// </summary>
        /// <remarks>
        /// <see cref="ToastPosition"/> 과 갈라 두는 까닭은 <b>고른 적이
        /// 없는 것</b>과 <b>기본값을 고른 것</b>이 저쪽에서는 똑같이
        /// <c>bottom-right</c> 로 보이기 때문이다. 옛 값을 계정으로 올릴 때
        /// 그 둘을 못 가리면 <b>아무것도 안 고른 사람 전원</b>의 계정에
        /// 「bottom-right 를 골랐다」가 적힌다.
        /// </remarks>
        internal string? ToastPositionStored { get; private init; }

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
        public bool ZoomUnlocked { get; private set; }

        /// <summary>
        /// 이 한 벌에서 <b>한 칸만 갈아 끼운 새 것.</b> 나머지는 그대로 옮긴다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>왜 갈아 끼울 일이 생기나.</b> <see cref="PortalBoot.ReadAsync"/> 는
        /// 회로마다 <b>한 번</b> 읽어 둔 것을 그대로 돌려준다(몇 번을 불러도
        /// 왕복은 한 번이다). 그래서 <c>Set…Async</c> 로 고친 뒤에 생긴 부품이
        /// 그것을 읽으면 <b>고치기 전 값</b>을 받는다 — 바뀜 알림은 그때 이미
        /// 지나간 뒤라 들을 수도 없다.
        /// </para>
        /// <para>
        /// 실제로 그랬다. 떠다니는 단추를 길게 눌러 자리를 옮긴 다음
        /// <b>업무를 옮기면</b>(셸의 레이아웃이 새로 생긴다) 단추가 옛 귀퉁이로
        /// 되돌아갔고, 환경설정 화면을 열면 고르개가 <b>옛 자리를 가리켰다.</b>
        /// 저장은 멀쩡히 됐으므로 <b>새로고침하면 나아서</b> 더 헷갈렸다.
        /// </para>
        /// <para>
        /// <b>지우지 않고 갈아 끼운다</b>(= 다시 읽지 않는다). 다시 읽게 하면
        /// 값 하나 고칠 때마다 왕복이 하나 늘고, 그 왕복은 <b>방금 우리가 적은
        /// 값</b>을 도로 받아 오는 일이다.
        /// </para>
        /// </remarks>
        private BrowserState Patch(Action<BrowserState> change)
        {
            var copy = (BrowserState)MemberwiseClone();
            change(copy);
            return copy;
        }

        internal BrowserState WithRightDrawerWidth(int value) =>
            Patch(c => c.RightDrawerWidthPx = value);

        internal BrowserState WithFabPosition(string value) => Patch(c => c.FabPosition = value);

        internal BrowserState WithFabHidden(bool value) => Patch(c => c.FabHidden = value);

        internal BrowserState WithHelpDeskFabPosition(string value) =>
            Patch(c => c.HelpDeskFabPosition = value);

        internal BrowserState WithHelpDeskFabHidden(bool value) =>
            Patch(c => c.HelpDeskFabHidden = value);

        internal BrowserState WithHelpDeskFabDesktop(bool value) =>
            Patch(c => c.HelpDeskFabDesktop = value);

        internal BrowserState WithBottomNavHidden(bool value) => Patch(c => c.BottomNavHidden = value);

        internal BrowserState WithBottomNavItems(string? value) =>
            Patch(c => c.BottomNavItemsJson = value);

        internal BrowserState WithBottomNavLabelsHidden(bool value) =>
            Patch(c => c.BottomNavLabelsHidden = value);

        internal BrowserState WithBottomNavDesktopHidden(bool value) =>
            Patch(c => c.BottomNavDesktopHidden = value);

        internal BrowserState WithToastPosition(string value) => Patch(c => c.ToastPosition = value);

        internal BrowserState WithZoomUnlocked(bool value) => Patch(c => c.ZoomUnlocked = value);

        /// <summary>
        /// 계정에 담긴 것들을 얹은 새 한 벌(<see cref="PortalPrefs"/>).
        /// </summary>
        /// <remarks>
        /// <b>고른 적이 없는 칸은 건드리지 않는다.</b> 계정에 열쇠가 없다는
        /// 것은 「기본값」이라는 뜻이고, 그 기본값은 이미 이 한 벌에 들어
        /// 있다 — 덮어쓰면 올리기 전에 읽힌 값이 한 번 번쩍인다.
        /// </remarks>
        internal BrowserState WithAccountPrefs(PortalPrefsData account) => Patch(c =>
        {
            c.BottomNavHidden = account.Flag(PortalPrefs.BottomNavHiddenKey) ?? c.BottomNavHidden;
            c.BottomNavItemsJson = account.Raw(PortalPrefs.BottomNavItemsKey) ?? c.BottomNavItemsJson;
            c.BottomNavLabelsHidden =
                account.Flag(PortalPrefs.BottomNavLabelsHiddenKey) ?? c.BottomNavLabelsHidden;
            c.BottomNavDesktopHidden =
                account.Flag(PortalPrefs.BottomNavDesktopHiddenKey) ?? c.BottomNavDesktopHidden;
            c.FabHidden = account.Flag(PortalPrefs.FabHiddenKey) ?? c.FabHidden;
            c.HelpDeskFabHidden = account.Flag(PortalPrefs.HelpDeskFabHiddenKey) ?? c.HelpDeskFabHidden;
            c.ToastPosition = NormalizeToastPosition(
                account.Text(PortalPrefs.ToastPositionKey) ?? c.ToastPosition);
            c.RightDrawerWidthPx = account.Number(PortalPrefs.RightDrawerWidthKey) ?? c.RightDrawerWidthPx;
        });

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
            RightDrawerWidthPx = Pixels(Get(wire.Local, QuickAskDrawerWidthKey))
                ?? Pixels(Get(wire.Local, RightDrawerWidthKey)),
            Theme = wire.Theme,
            FabPosition = NormalizeFabPosition(Get(wire.Local, FabPositionKey)),
            FabHidden = Has(wire.Local, FabHiddenKey),
            HelpDeskFabPosition = NormalizeHelpDeskFabPosition(Get(wire.Local, HelpDeskFabPositionKey)),
            HelpDeskFabHidden = Has(wire.Local, HelpDeskFabHiddenKey),
            HelpDeskFabDesktop = Has(wire.Local, HelpDeskFabDesktopKey),
            BottomNavHidden = Has(wire.Local, BottomNavHiddenKey),
            BottomNavItemsJson = Get(wire.Local, BottomNavItemsKey),
            ToastPosition = NormalizeToastPosition(Get(wire.Local, ToastPositionKey)),
            ToastPositionStored = Get(wire.Local, ToastPositionKey),
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
        internal static DateTime? Moment(string? value) =>
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
