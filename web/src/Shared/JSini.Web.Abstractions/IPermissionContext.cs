namespace JSini.Web.Abstractions;

/// <summary>
/// 지금 로그인한 사용자가 무엇을 할 수 있는지 답하는 곳.
///
/// [판정은 반드시 한 곳뿐이어야 한다]
///
/// 사이드바 거르기와 라우트 진입 가드가 <b>같은 판정</b>을 써야 "목록에 보이는데
/// 누르면 403" 이 생기지 않는다. Vue 에서 <c>canViewMenu()</c> 하나로 모았던 이유가
/// 그것이고, 여기서도 그대로 지킨다 — 셸의 어디에서도 권한을 직접 계산하지 않고
/// 이 인터페이스에만 묻는다.
/// </summary>
public interface IPermissionContext
{
    /// <summary>
    /// 권한표를 이미 받아 두었는가.
    ///
    /// 받기 <b>전에는 아무것도 거르지 않는다</b>. 못 받은 상태를 "권한 없음" 으로
    /// 다루면 로그인 직후 사이드바가 한 번 비었다가 채워진다. 반대로 통과시키면
    /// 걸러지지 않은 목록이 잠깐 보인다 — 둘 다 눈에 띄지만, 실제 통제는 서버가
    /// 하므로 후자가 덜 위험하고 덜 거슬린다.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>이 경로의 화면을 열 수 있는가. <c>use_view</c> 를 본다.</summary>
    bool CanView(string path);

    /// <summary>이 경로의 화면에서 그 동작을 할 수 있는가.</summary>
    bool Can(string path, MenuAction action);

    /// <summary>
    /// 권한표를 서버에서 다시 읽는다.
    ///
    /// [메뉴 관리]에서 권한을 고치면 화면을 새로 열지 않고도 반영되어야 한다.
    /// Vue 때는 이걸 하려고 라우트까지 다시 만들어야 했지만(<c>refreshAccessMenus</c>),
    /// 이제 라우트는 컴파일 시점에 고정이라 권한표와 메뉴 트리만 다시 읽으면 된다.
    /// </summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
