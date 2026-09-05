namespace JSini.Web.Abstractions;

/// <summary>
/// 지금 사용자에게 보여 줄 메뉴 트리를 내놓는 곳.
///
/// 원본은 <c>/auth/menu/all</c> 이고, 그것을 <see cref="IPermissionContext"/> 와
/// 화면 크기로 거른 결과가 <see cref="VisibleMenus"/> 다.
///
/// [거르는 것은 목록뿐이다]
///
/// 목록에서 빠진 화면도 라우트는 살아 있다 — 주소로 직접 들어가면 열린다.
/// 거기서 실제로 막는 것은 라우트 가드와 서버다. 목록에서 지우는 것은 통제가 아니라
/// 정리다. 이 둘을 섞으면 "목록에 없으니 안전하다" 는 착각이 생긴다.
/// Vue 의 <c>menu-visibility.ts</c> 가 같은 말을 주석으로 달고 있었고, 그 원칙을 옮긴다.
/// </summary>
public interface IMenuProvider
{
    /// <summary>권한과 화면 크기로 거른 메뉴 트리.</summary>
    IReadOnlyList<MenuNode> VisibleMenus { get; }

    /// <summary>거르기 전 원본 트리. 진단 화면과 라우트 대조에 쓴다.</summary>
    IReadOnlyList<MenuNode> AllMenus { get; }

    /// <summary>메뉴가 다시 걸러졌을 때 알린다(권한 갱신·화면 크기 변경).</summary>
    event Action? MenusChanged;

    /// <summary>
    /// 이 주소에 이르는 메뉴 줄기를 뿌리부터 돌려준다. 못 찾으면 빈 목록.
    ///
    /// 브레드크럼과 탭 이름이 쓴다. <b>거르기 전 원본</b>에서 찾는다 —
    /// 걸러진 목록에서 찾으면 권한 때문에 사이드바에 없는 화면을 주소로 열었을 때
    /// 브레드크럼이 통째로 비어 "여기가 어디인지" 를 알 수 없다.
    ///
    /// 주소는 <b>링크 주소</b>(<see cref="MenuNode.LinkTarget"/>)로 맞춘다 —
    /// 브라우저에 떠 있는 값이 그것이기 때문이다.
    /// </summary>
    /// <param name="href">전체 경로 (<c>/projmng/proj/wbs</c>). 질의 문자열은 뗀 것.</param>
    IReadOnlyList<MenuNode> Trail(string? href);

    /// <summary>서버에서 메뉴 트리를 다시 읽고 거른다.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 화면 크기가 바뀌었음을 알린다. 들고 있던 원본을 새 기준으로 다시 거른다.
    ///
    /// 기기 회전·창 크기 조절·개발자 도구의 기기 모드에서 불린다. Vue 에서는
    /// <c>matchMedia</c> 를 직접 구독했지만, Blazor Server 는 브라우저 상태를
    /// 모르므로 셸의 JS interop 이 이 메서드로 알려 준다.
    /// </summary>
    void SetViewport(Viewport viewport);
}

/// <summary>
/// 화면 크기 구분. 데스크톱은 <c>use_mobile</c>·<c>use_tablet</c> 과 무관하게 다 보인다.
/// 경계값은 Vue 때와 같게 맞춘다 — 사용자가 같은 기기에서 다른 메뉴를 보면 안 된다.
/// </summary>
public enum Viewport
{
    /// <summary>767px 이하. vben 의 <c>isMobile</c>(md 미만)과 같은 기준.</summary>
    Phone,

    /// <summary>768px 이상 1023px 이하. tailwind 의 md 이상 lg 미만.</summary>
    Tablet,

    /// <summary>1024px 이상. 크기 규칙을 따지지 않는다.</summary>
    Desktop,
}
