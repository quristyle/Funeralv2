namespace JSini.Web.Abstractions;

/// <summary>
/// 지금 사용자에게 보여 줄 메뉴 트리를 내놓는다. 원본은 /auth/menu/all 이고, 권한과 화면 크기로 거른 결과가 VisibleMenus 다.
/// 거르는 것은 목록뿐이다. 목록에서 빠진 화면도 주소로 들어가면 열린다. 실제 통제는 라우트 가드와 서버가 한다. 목록에서 지우는 것은 정리일 뿐 통제가 아니다.
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
    /// 이 주소에 이르는 메뉴 줄기를 뿌리부터 돌려준다(못 찾으면 빈 목록). 브레드크럼과 탭 이름이 쓴다.
    /// 거르기 전 원본에서 찾는다. 걸러진 목록에서 찾으면 사이드바에 없는 화면을 주소로 열었을 때 브레드크럼이 통째로 빈다. 주소는 링크 주소(LinkTarget)로 맞춘다.
    /// </summary>
    /// <param name="href">전체 경로 (<c>/projmng/proj/wbs</c>). 질의 문자열은 뗀 것.</param>
    IReadOnlyList<MenuNode> Trail(string? href);

    /// <summary>서버에서 메뉴 트리를 다시 읽고 거른다.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>화면 크기가 바뀌었음을 알린다. 들고 있던 원본을 새 기준으로 다시 거른다. Blazor Server 는 브라우저 상태를 모르므로 셸의 JS interop 이 이 메서드로 알려 준다.</summary>
    void SetViewport(Viewport viewport);
}

/// <summary>
/// 화면 크기 구분. 데스크톱은 use_mobile·use_tablet 과 무관하게 다 보인다. 경계값은 이전 포털과 같게 맞춘다(같은 기기에서 다른 메뉴가 보이면 안 된다).
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
