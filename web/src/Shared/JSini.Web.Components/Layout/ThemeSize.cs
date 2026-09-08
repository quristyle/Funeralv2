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
///
/// [고르는 단계는 여섯, DevExpress 모드는 셋이다]
///
/// <b>이 둘을 갈라 둔 것이 이 클래스의 요점이다.</b>
///
/// <list type="table">
///   <listheader>
///     <term>고르는 단계</term>
///     <description>글자 · DevExpress 모드</description>
///   </listheader>
///   <item><term><c>xxsmall</c> 가장작게</term><description>0.625rem · Small</description></item>
///   <item><term><c>xsmall</c> 아주작게</term><description>0.6875rem · Small</description></item>
///   <item><term><c>small</c> 작게</term><description>0.75rem · Small</description></item>
///   <item><term><c>compact</c> 조금작게</term><description>0.8125rem · Small</description></item>
///   <item><term><c>medium</c> 보통</term><description>0.875rem · Medium</description></item>
///   <item><term><c>large</c> 크게</term><description>1rem · Large</description></item>
/// </list>
///
/// <b>넷째 <c>SizeMode</c> 를 만들지 않는다.</b> DevExpress 가 주는 것이
/// <c>Small · Medium · Large</c> 셋뿐이고, 없는 값을 흘리면 그리드·달력·팝업이
/// 따라오지 않아 <b>한 화면에 두 크기</b>가 된다. 그래서 우리가 넣은 세 단계는
/// <b>글자만 바꾸고 부품 크기는 Small 을 그대로 쓴다.</b>
///
/// 글자는 우리 사다리(<c>--jsini-fs-*</c>)가 따로 갖고 있어서 그렇게 할 수 있다.
/// 뿌리(<c>--jsini-fs-base</c>)를 <c>&lt;html&gt;</c> 의 <c>data-dx-size</c> 로
/// 고르고, 그 속성은 theme.js 가 <c>&lt;head&gt;</c> 안에서 동기로 세운다 —
/// 첫 그림부터 맞는 글자 크기다.
///
/// [그래서 담는 값이 <c>SizeMode</c> 가 아니라 <b>단계 이름</b>이다]
///
/// 한동안 이 클래스가 <c>SizeMode</c> 만 들고 있었다. 그러면 여섯 단계가 셋으로
/// 뭉개져서 <b>서랍이 어느 칸을 고른 것인지 알 수 없다</b> — 아주작게를 골라도
/// 「작게」에 표시가 붙는다. 쿠키와 <c>PersistentComponentState</c> 도 같은
/// 이유로 단계 이름을 실어 나른다.
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

    /// <summary>기본 단계. <see cref="Default"/> 와 같은 자리를 가리킨다.</summary>
    public const string DefaultStep = "medium";

    /// <summary>
    /// 고를 수 있는 단계와 그것이 쓸 DevExpress 모드.
    ///
    /// <b>theme.js 의 <c>SIZES</c> 와 아이디가 같아야 한다.</b> 어긋나면
    /// 예외가 나지 않는다 — 서랍에서 고른 칸이 여기 없는 이름이면
    /// <see cref="Parse"/> 가 기본값으로 떨어뜨리고, 증상은 「글자는 바뀌는데
    /// 부품 크기가 안 따라온다」다(글자는 CSS 가 하고 부품은 이쪽이 한다).
    /// </summary>
    private static readonly (string Step, SizeMode Mode)[] Steps =
    [
        ("xxsmall", SizeMode.Small),
        ("xsmall", SizeMode.Small),
        ("small", SizeMode.Small),
        ("compact", SizeMode.Small),
        ("medium", SizeMode.Medium),
        ("large", SizeMode.Large),
    ];

    private string _step = DefaultStep;

    /// <summary>
    /// 지금 고른 단계 (<c>xxsmall</c> · <c>xsmall</c> · <c>small</c> ·
    /// <c>compact</c> · <c>medium</c> · <c>large</c>).
    /// <b>서랍이 어느 칸에 표시를 붙일지 이 값으로 정한다.</b>
    /// </summary>
    public string Step => _step;

    /// <summary>
    /// 지금 크기. DevExpress 부품이 이 값으로 그려진다.
    /// <b>여섯 단계가 셋으로 접힌다</b> — 클래스 머리말의 표 참고.
    /// </summary>
    public SizeMode Current => ModeOf(_step);

    /// <summary>크기가 바뀌었다. <see cref="SizeModeScope"/> 가 듣고 다시 그린다.</summary>
    public event Action? Changed;

    /// <summary>
    /// 크기를 바꾼다. 같은 값이면 아무 일도 하지 않는다 —
    /// 알리면 화면 전체가 다시 그려지는데 바뀐 것이 없다.
    /// </summary>
    /// <remarks>
    /// <b>단계로 견준다.</b> <c>SizeMode</c> 로 견주면 아주작게 → 작게처럼
    /// <b>같은 모드 안에서 옮기는 것</b>이 「바뀐 것이 없다」로 읽혀
    /// 글자 크기가 안 따라온다.
    /// </remarks>
    public void Set(string? step)
    {
        var next = Parse(step);

        if (_step == next)
        {
            return;
        }

        _step = next;
        Changed?.Invoke();
    }

    /// <summary>
    /// 첫 값을 채운다. 알리지 않는다 — 아직 아무도 그리기 전이고,
    /// 여기서 알리면 컴포넌트가 초기화되는 도중에 다시 그리라는 말이 된다.
    /// </summary>
    public void Seed(string? step) => _step = Parse(step);

    /// <summary>
    /// 쿠키·저장값의 글자를 <b>단계 이름</b>으로 좁힌다. 모르는 값이면 기본값이다.
    ///
    /// <c>Enum.TryParse</c> 를 쓰지 않는다. 그러면 <c>"1"</c> 같은 숫자 문자열도
    /// 통과하고, theme.js 가 보내지 않는 이름까지 받아 주게 된다.
    /// </summary>
    public static string Parse(string? value)
    {
        foreach (var (step, _) in Steps)
        {
            if (step == value)
            {
                return step;
            }
        }

        return DefaultStep;
    }

    /// <summary>
    /// 껍데기(헤더 아이콘 단추 · 그리드 아래 띠)가 <b>멈추는 단계</b>.
    /// </summary>
    /// <remarks>
    /// <c>app.css</c> 의 <c>--jsini-chrome-scale</c> 캡과 <b>같은 자리를 가리켜야
    /// 한다.</b> 어긋나면 아이콘 그림은 멈추는데 단추 여백만 계속 자라거나
    /// 그 반대가 된다 — 실제로 그 어긋남을 한 번 밟았다.
    /// </remarks>
    public const string ChromeCapStep = "compact";

    /// <summary>
    /// 껍데기용 크기. <b>캡을 넘지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// [왜 <see cref="Current"/> 와 따로 있나 — 여백이 계속 자랐다]
    /// </para>
    ///
    /// <para>
    /// 아이콘 그림은 <c>--jsini-icon-size</c> 가 「조금작게」에서 멈추게 해
    /// 두었는데, <b>단추의 좌우 여백은 DevExpress 가 <c>SizeMode</c> 로 정한다.</b>
    /// 그래서 「보통」·「크게」에서 여백만 계속 자랐다 — 재서 확인한 값이
    /// 3px → 5px → 7px 이고, 단추 폭이 28 → 32 → 36 이 됐다. 그림은 멈췄는데
    /// 자리는 벌어지니 <b>아이콘 사이가 계속 멀어지는 것</b>으로 보였다.
    /// </para>
    ///
    /// <para>
    /// 그 여백을 CSS 로 덮지 않는다. 테마가 스물둘이고 값이 테마마다 달라서,
    /// 우리가 숫자를 박으면 어느 테마에서는 어긋난다. 대신 <b>그 자리에만</b>
    /// 이 값을 흘려 준다 — DevExpress 가 정한 Small 여백을 그대로 쓰게 된다.
    /// </para>
    ///
    /// <para>
    /// <b>흘리는 범위가 좁아야 한다.</b> 헤더 전체를 묶으면 테마 서랍·AI 서랍·
    /// 사용자 판이 <b>헤더의 자손</b>이라 그 안까지 작아진다 — 그것들은 내용을
    /// 담는 판이라 사용자가 고른 크기를 그대로 따라야 한다. 그래서 아이콘
    /// 단추가 실제로 서는 네 자리만 감싼다.
    /// </para>
    /// </remarks>
    public SizeMode Chrome => ChromeModeOf(_step);

    /// <summary>이 단계의 껍데기용 모드. 캡보다 큰 단계면 캡의 것을 준다.</summary>
    public static SizeMode ChromeModeOf(string? step)
    {
        var chosen = IndexOf(Parse(step));
        var cap = IndexOf(ChromeCapStep);

        return Steps[Math.Min(chosen, cap)].Mode;
    }

    /// <summary>사다리에서 그 단계가 몇 번째인가. 모르는 이름이면 기본 단계 자리.</summary>
    private static int IndexOf(string step)
    {
        var fallback = 0;

        for (var i = 0; i < Steps.Length; i++)
        {
            if (Steps[i].Step == step)
            {
                return i;
            }

            if (Steps[i].Step == DefaultStep)
            {
                fallback = i;
            }
        }

        return fallback;
    }

    /// <summary>이 단계가 쓸 DevExpress 모드.</summary>
    public static SizeMode ModeOf(string? step)
    {
        foreach (var (name, mode) in Steps)
        {
            if (name == step)
            {
                return mode;
            }
        }

        return Default;
    }
}
