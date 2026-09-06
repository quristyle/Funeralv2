using DevExpress.Blazor;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 사용자가 고른 DevExpress 크기 모드 (Small · Medium · Large).
///
/// [왜 이런 것이 따로 있나 — 테마와 같은 길로 갈 수 없다]
///
/// 테마는 스타일시트다. 브라우저에서 <c>&lt;link&gt;</c> 를 갈아 끼우면 끝이고
/// 서버는 몰라도 된다(theme.js 가 혼자 한다). 크기는 다르다 — DevExpress 는
/// 부품 뿌리에 <c>dxbl-sm</c>/<c>dxbl-lg</c> 를 <b>서버가 HTML 을 만들 때</b>
/// 붙인다. 그래서 서버가 알아야 하고, 알려 주는 길이 이 서비스다.
///
/// [scoped 인 이유]
///
/// 회로 하나가 사용자 한 명의 창 하나다. 싱글턴으로 두면 누가 크게 바꾸는
/// 순간 모든 사람의 화면이 커진다 — <see cref="PortalTabs"/> 와 같은 이유다.
///
/// [값을 흘리는 것은 이 서비스가 아니라 <see cref="SizeModeScope"/> 다]
///
/// 여기는 "지금 무엇을 골랐나" 만 들고 있고, 그 값을 DevExpress 부품에게
/// 흘리는 일은 그 컴포넌트가 한다. 나눈 이유는 서랍(ThemeToggle)과 흘리는
/// 쪽이 화면 트리에서 서로 멀기 때문이다 — 서랍은 레이아웃 안, 흘리는 쪽은
/// 라우터 바깥이다.
/// </summary>
public sealed class ThemeSize
{
    /// <summary>
    /// 브라우저가 굽는 쿠키 이름. <b>theme.js 의 <c>SIZE_COOKIE</c> 와 같아야 한다.</b>
    /// 어긋나면 첫 그림만 기본 크기로 나오고 회로가 붙으면서 바뀐다 —
    /// 오류가 아니라 "가끔 화면이 출렁인다" 로만 보이는 종류의 어긋남이다.
    /// </summary>
    public const string CookieName = "jsini.size";

    /// <summary>
    /// DevExpress 부품이 크기를 받는 CascadingValue 이름.
    ///
    /// <b>DevExpress 가 정한 값이고 우리가 고를 수 없다.</b> 부품마다
    /// <c>[CascadingParameter(Name = "ParentSizeMode")]</c> 가 달려 있고,
    /// 그 이름은 <c>DxComponentBase.ParentSizeModeCascadeName</c> 에 상수로
    /// 있지만 <b>internal 이라 우리가 참조할 수 없다.</b> 그래서 글자를 다시
    /// 적는다 — 여기 한 곳에만 적어 두는 이유가 그것이다.
    ///
    /// 어긋나면 예외가 나지 않는다. 아무도 안 받아 가서 크기를 바꿔도
    /// DevExpress 부품만 그대로 있는다.
    /// </summary>
    public const string CascadeName = "ParentSizeMode";

    /// <summary>
    /// 기본값. <b>DevExpress 의 기본과 같은 자리다</b>(<c>GlobalOptions.DefaultSizeMode</c>).
    ///
    /// 한동안 화면마다 <c>SizeMode.Small</c> 을 손으로 박아 두어 사실상 Small 이
    /// 기본이었다. 그래서 "글씨가 작다" 는 말이 나왔고, 고르게 해 달라는 요구가
    /// 이 코드의 출발점이다. 작게 쓰던 사람은 서랍에서 Small 을 고르면 된다.
    /// </summary>
    public const SizeMode Default = SizeMode.Medium;

    private SizeMode _current = Default;

    /// <summary>지금 크기. DevExpress 부품이 이 값으로 그려진다.</summary>
    public SizeMode Current => _current;

    /// <summary>크기가 바뀌었다. <see cref="SizeModeScope"/> 가 듣고 다시 그린다.</summary>
    public event Action? Changed;

    /// <summary>
    /// 크기를 바꾼다. 같은 값이면 아무 일도 하지 않는다 —
    /// 알리면 화면 전체가 다시 그려지는데 바뀐 것이 없다.
    /// </summary>
    public void Set(SizeMode mode)
    {
        if (_current == mode)
        {
            return;
        }

        _current = mode;
        Changed?.Invoke();
    }

    /// <summary>
    /// 첫 값을 채운다. 알리지 않는다 — 아직 아무도 그리기 전이고,
    /// 여기서 알리면 컴포넌트가 초기화되는 도중에 다시 그리라는 말이 된다.
    /// </summary>
    public void Seed(SizeMode mode) => _current = mode;

    /// <summary>
    /// 쿠키·저장값의 글자를 크기로 옮긴다. 모르는 값이면 기본값이다.
    ///
    /// <c>Enum.TryParse</c> 를 쓰지 않는다. 그러면 <c>"1"</c> 같은 숫자 문자열도
    /// 통과하고, theme.js 가 보내지 않는 이름까지 받아 주게 된다.
    /// </summary>
    public static SizeMode Parse(string? value) => value switch
    {
        "small" => SizeMode.Small,
        "medium" => SizeMode.Medium,
        "large" => SizeMode.Large,
        _ => Default,
    };

    /// <summary>크기를 theme.js 가 쓰는 글자로 옮긴다.</summary>
    public static string Name(SizeMode mode) => mode switch
    {
        SizeMode.Small => "small",
        SizeMode.Large => "large",
        _ => "medium",
    };
}
