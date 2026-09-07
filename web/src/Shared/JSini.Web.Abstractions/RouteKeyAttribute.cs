namespace JSini.Web.Abstractions;

/// <summary>
/// 이 화면을 가리키는 <b>변하지 않는 열쇠</b>. DB 메뉴가 URL 대신 이것을 든다.
///
/// <code>
/// @page "/funeral/room-status"
/// @attribute [RouteKey("funeral.room-status")]
/// </code>
///
/// [왜 필요한가 — URL 이 열쇠이던 것이 족쇄였다]
///
/// DB(<c>scom.system_menus</c>)는 화면을 <c>path</c> 문자열로 가리켰다. 그런데
/// 같은 문자열을 <b>권한표와 즐겨찾기도 열쇠로 쓴다.</b> 그래서 URL 을 한 글자만
/// 고쳐도 그 메뉴의 권한과 즐겨찾기가 통째로 끊긴다 — 끊기는 방향이
/// <i>권한이 없는데 메뉴가 보이는</i> 쪽이라 특히 나쁘다.
///
/// 결과로 URL 이 사실상 못 바꾸는 값이 되었고, Vue 시절 경로 69건을
/// <c>RouteAliases</c> 가 떠받치는 상태가 이어졌다.
///
/// 열쇠를 URL 에서 떼면 그 매듭이 풀린다.
///
/// <list type="bullet">
///   <item>URL 은 코드가 소유한다 — <c>@page</c> 그대로다. 바꿔도 DB 는 모른다.</item>
///   <item>DB 는 열쇠만 든다 — 메뉴를 옮기고 이름을 바꿔도 화면이 안 끊긴다.</item>
///   <item>메뉴 관리 화면은 URL 을 <b>타이핑하지 않는다.</b> 실려 있는 화면
///         목록에서 고른다 — 없는 화면을 가리킬 방법 자체가 없어진다.</item>
/// </list>
///
/// [런타임 비용이 없다]
///
/// 열쇠→URL 변환은 기동 때 한 번 만든 사전 조회다(<c>RouteInventory.Catalog</c>).
/// 화면을 열 때 DB 를 보지 않으므로 라우팅 속도는 지금과 똑같다. DB 가 라우트를
/// 만드는 방식(Vue 때)과 갈리는 지점이 여기다.
///
/// [규칙]
///
/// <list type="number">
///   <item>화면 하나에 정확히 하나. 빠지면 아키텍처 테스트가 빌드를 세운다.</item>
///   <item>저장소 전체에서 유일하다.</item>
///   <item><c>{모듈}.</c> 로 시작한다 (<c>funeral.</c> · <c>helpdesk.</c>).</item>
///   <item>포괄 라우트(<c>_Pending.razor</c>)에는 붙이지 않는다 — 화면이 아니라
///         "아직 화면이 없다" 는 안내다.</item>
/// </list>
///
/// <b>한 번 정한 열쇠는 바꾸지 않는다.</b> 바꾸면 DB 의 그 메뉴가 화면을 잃는다 —
/// URL 을 바꿔도 안전하게 만들려고 도입한 것이므로, 열쇠 자체가 흔들리면 뜻이 없다.
/// 화면의 성격이 아니라 <b>위치</b>에서 딴 이름이라 대개 바꿀 일이 없지만,
/// 그래도 옮길 일이 생기면 DB 를 함께 고쳐야 한다.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RouteKeyAttribute(string key) : Attribute
{
    /// <summary>화면을 가리키는 열쇠 (<c>funeral.room-status</c>).</summary>
    public string Key { get; } = key;
}
