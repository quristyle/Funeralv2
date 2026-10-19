namespace JSini.Web.Abstractions;

/// <summary>
/// 이 화면의 권한이 <b>어디서 오는가</b>.
/// </summary>
public enum ScreenAccessKind
{
    /// <summary>
    /// 메뉴 권한을 따른다. 기본값이고, 못 찾으면 막는다. 찾는 차례는 ① 지금 주소 ② RouteKey ③ InheritsFrom 이다. 셋 다 빈손이면 권한관리 밖이라 막는다.
    /// </summary>
    Menu = 0,

    /// <summary>권한관리 밖에 두기로 명시적으로 정한 화면(로그인·오류·권한 없음 안내·첫 화면). 업무 자료를 보여 주는 화면에는 쓰지 않는다. 쓰는 순간 그 화면만 기본 차단을 비켜 가고 그 사실이 남지 않는다.</summary>
    Open = 1,
}

/// <summary>
/// 화면이 자기 권한의 출처를 신고한다. 신고하지 않고 RouteKey 로도 메뉴를 못 찾으면 그 화면은 열리지 않고 차단 기록에 남는다(docs/screen-access.md).
/// 메뉴에 없는 상세 화면은 DB 메뉴를 새로 만들지 않고 부모 화면을 가리킨다. 목록을 열 수 있는 사람만 그 상세를 연다는 뜻이다.
/// </summary>
/// <code>
/// @page "/admin/system/account/{LoginId}/app"
/// @attribute [RouteKey("admin.system.account.app")]
/// @attribute [ScreenAccess("admin.system.account")]
/// </code>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ScreenAccessAttribute : Attribute
{
    /// <summary>다른 메뉴의 권한을 물려받는다.</summary>
    /// <param name="inheritsFrom">부모 화면의 <see cref="RouteKeyAttribute">열쇠</see>(<c>admin.system.account</c>). 그 열쇠를 든 DB 메뉴의 path 로 열람 권한을 따진다.</param>
    public ScreenAccessAttribute(string inheritsFrom)
    {
        Kind = ScreenAccessKind.Menu;
        InheritsFrom = inheritsFrom;
    }

    /// <summary>권한의 출처를 종류로 못 박는다.</summary>
    public ScreenAccessAttribute(ScreenAccessKind kind)
    {
        Kind = kind;
    }

    /// <summary>권한이 어디서 오는가.</summary>
    public ScreenAccessKind Kind { get; }

    /// <summary>물려받을 부모 메뉴의 열쇠. 없으면 <c>null</c>.</summary>
    public string? InheritsFrom { get; }

    /// <summary>왜 이렇게 두었는지 한 줄. 진단 화면이 그대로 보여 준다. Open 은 기본 차단을 비켜 가는 선언이라 까닭이 코드 주석에만 있으면 아무도 되짚지 못한다.</summary>
    public string? Reason { get; init; }
}
