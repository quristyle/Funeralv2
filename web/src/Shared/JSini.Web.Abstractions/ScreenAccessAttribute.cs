namespace JSini.Web.Abstractions;

/// <summary>
/// 이 화면의 권한이 <b>어디서 오는가</b>.
/// </summary>
public enum ScreenAccessKind
{
    /// <summary>
    /// 메뉴 권한을 따른다. <b>기본값이고, 못 찾으면 막는다.</b>
    ///
    /// <para>
    /// 찾는 차례는 셋이다 — ① 지금 주소로 메뉴를 찾고(사이드바가 건 링크와
    /// 같은 값) ② 화면이 선언한 <see cref="RouteKeyAttribute"/> 로 찾고
    /// ③ <see cref="ScreenAccessAttribute.InheritsFrom"/> 가 가리키는 메뉴로
    /// 찾는다. 셋 다 빈손이면 <b>그 화면은 권한관리 밖</b>이라 막는다.
    /// </para>
    /// </summary>
    Menu = 0,

    /// <summary>
    /// 권한관리 밖에 두기로 <b>명시적으로</b> 정한 화면.
    ///
    /// <para>
    /// 로그인·오류·「권한 없음」 안내·첫 화면처럼 <b>메뉴에 올릴 수 없는</b>
    /// 자리다. 업무 자료를 보여 주는 화면에는 쓰지 않는다 — 쓰는 순간
    /// 기본 차단이 그 화면만 비켜 가고, 비켜 간 사실이 아무 데도 안 남는다.
    /// </para>
    /// </summary>
    Open = 1,
}

/// <summary>
/// 화면이 자기 <b>권한의 출처</b>를 신고한다.
///
/// <code>
/// @page "/admin/system/account/{LoginId}/app"
/// @attribute [RouteKey("admin.system.account.app")]
/// @attribute [ScreenAccess("admin.system.account")]   // 계정 관리의 권한을 따른다
/// </code>
///
/// <para>
/// [왜 필요한가 — 메뉴에 없는 화면이 통째로 열려 있었다]
/// </para>
///
/// <para>
/// 권한 판정의 열쇠는 DB 메뉴의 <c>path</c> 다. 그래서 <b>메뉴에 줄이 없는
/// 화면은 따질 열쇠가 없었고</b>, 레이아웃은 그런 화면을 막지 않고 지나쳤다
/// (「메뉴에 없는 화면은 권한표에 줄이 없으므로 여기서 막지 않는다」).
/// 상세 화면은 대개 목록에서 줄을 골라야 들어가지므로 그 길이 오래 안 보였는데,
/// <b>주소를 직접 치면 목록을 안 거친다</b> — 계정 관리 권한이 없는 사람이
/// <c>/admin/system/account/{남의 아이디}/app</c> 으로 남의 기기·구독·발송
/// 기록을 열어 볼 수 있었다(2026-10-13).
/// </para>
///
/// <para>
/// 고치는 방향이 둘인데 하나만 옳다. 그런 화면마다 DB 메뉴를 만들어 주는 것은
/// <b>사이드바에 세울 수 없는 것을 메뉴 표에 억지로 넣는</b> 일이고(「누구의
/// 것인지 없는 주소」), 메뉴가 늘수록 권한 화면이 읽기 어려워진다. 그래서
/// <b>화면이 자기 부모를 가리키게</b> 한다 — 목록을 열 수 있는 사람만 그
/// 목록의 상세를 연다는 뜻이고, 권한 화면에서는 줄 하나가 그대로 유지된다.
/// </para>
///
/// <para>
/// [신고하지 않으면 막힌다]
/// </para>
///
/// <para>
/// 이 속성이 없고 <see cref="RouteKeyAttribute"/> 의 열쇠로도 메뉴를 못 찾으면
/// <b>그 화면은 열리지 않고 차단 기록에 남는다</b>(<c>docs/screen-access.md</c>).
/// 틀리는 방향을 「안 열리는」 쪽으로 둔 것이다 — 반대로 두면 새로 만든 화면이
/// 아무에게나 열리는데, 그 사실이 아무 데도 안 남아 영영 안 드러난다.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ScreenAccessAttribute : Attribute
{
    /// <summary>
    /// 다른 메뉴의 권한을 물려받는다.
    /// </summary>
    /// <param name="inheritsFrom">
    /// 부모 화면의 <see cref="RouteKeyAttribute">열쇠</see>
    /// (<c>admin.system.account</c>). 그 열쇠를 든 DB 메뉴의 <c>path</c> 로
    /// 열람 권한을 따진다.
    /// </param>
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

    /// <summary>
    /// 왜 이렇게 두었는지 한 줄. <b>진단 화면이 그대로 보여 준다</b> —
    /// <see cref="ScreenAccessKind.Open"/> 은 기본 차단을 비켜 가는 선언이라
    /// 까닭이 코드 주석에만 있으면 나중에 아무도 되짚지 못한다.
    /// </summary>
    public string? Reason { get; init; }
}
