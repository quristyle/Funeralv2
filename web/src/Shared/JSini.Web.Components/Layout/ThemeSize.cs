using DevExpress.Blazor;

namespace JSini.Web.Components.Layout;

/// <summary>사용자가 고른 DevExpress 크기 모드의 단계. 테마는 스타일시트라 브라우저가 갈아 끼우면 끝이지만, 크기는 DevExpress 가 부품 뿌리에 dxbl-sm/dxbl-lg 를 서버가 HTML 을 만들 때 붙여서 서버가 알아야 한다.
/// 고르는 단계는 아홉이고 DevExpress 모드는 셋이다. 넷째 SizeMode 를 만들지 않는다 — 없는 값을 흘리면 한 화면에 두 크기가 된다. 그래서 글자만 우리 사다리(--jsini-fs-*)로 바꾸고 부품 크기는 이웃한 모드를 쓴다(「크게」 아래 셋은 Small, 위 셋은 Large). 담는 값도 SizeMode 가 아니라 단계 이름이다 — 셋으로 뭉개면 서랍이 어느 칸을 골랐는지 모른다. scoped — 회로 하나가 사용자 한 명이다.</summary>
public sealed class ThemeSize
{
    /// <summary>브라우저가 굽는 쿠키 이름. theme.js 의 SIZE_COOKIE 와 같아야 한다 — 어긋나면 첫 그림만 기본 크기로 나오고 회로가 붙으며 바뀐다.</summary>
    public const string CookieName = "jsini.size";

    /// <summary>DevExpress 부품이 크기를 받는 CascadingValue 이름. DevExpress 가 정한 값이라 우리가 고를 수 없다 — ParentSizeModeCascadeName 은 internal 이라 글자를 다시 적는다. 어긋나면 예외 없이 부품 크기만 안 따라온다.</summary>
    public const string CascadeName = "ParentSizeMode";

    /// <summary>기본값. DevExpress 기본과 같은 자리다. 예전에는 화면마다 SizeMode.Small 을 박아 사실상 Small 이 기본이었다.</summary>
    public const SizeMode Default = SizeMode.Medium;

    /// <summary>기본 단계. <see cref="Default"/> 와 같은 자리를 가리킨다.</summary>
    public const string DefaultStep = "medium";

    /// <summary>고를 수 있는 단계와 그것이 쓸 DevExpress 모드. theme.js 의 SIZES 와 아이디가 같아야 한다 — 어긋나면 예외 없이 글자만 바뀌고 부품 크기가 안 따라온다.</summary>
    private static readonly (string Step, SizeMode Mode)[] Steps =
    [
        ("xxsmall", SizeMode.Small),
        ("xsmall", SizeMode.Small),
        ("small", SizeMode.Small),
        ("compact", SizeMode.Small),
        ("medium", SizeMode.Medium),
        ("large", SizeMode.Large),
        ("xlarge", SizeMode.Large),
        ("xxlarge", SizeMode.Large),
        ("xxxlarge", SizeMode.Large),
    ];

    private string _step = DefaultStep;

    /// <summary>지금 고른 단계(xxsmall ~ xxxlarge). 서랍이 어느 칸에 표시를 붙일지 이 값으로 정한다.</summary>
    public string Step => _step;

    /// <summary>지금 크기. DevExpress 부품이 이 값으로 그려진다 — 아홉 단계가 셋으로 접힌다(머리말 표).</summary>
    public SizeMode Current => ModeOf(_step);

    /// <summary>크기가 바뀌었다. <see cref="SizeModeScope"/> 가 듣고 다시 그린다.</summary>
    public event Action? Changed;

    /// <summary>크기를 바꾼다. 같은 값이면 아무 일도 하지 않는다. 단계로 견준다 — SizeMode 로 견주면 같은 모드 안의 이동(아주작게 → 작게)이 바뀐 것이 없다고 읽힌다.</summary>
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

    /// <summary>첫 값을 채운다. 알리지 않는다 — 아직 아무도 그리기 전이고 여기서 알리면 초기화 도중에 다시 그리라는 말이 된다.</summary>
    public void Seed(string? step) => _step = Parse(step);

    /// <summary>쿠키·저장값의 글자를 단계 이름으로 좁힌다. 모르는 값이면 기본값. Enum.TryParse 는 숫자 문자열도 받아 버려 쓰지 않는다.</summary>
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

    /// <summary>껍데기(헤더 아이콘 단추 · 그리드 아래 띠)가 멈추는 단계. app.css 의 --jsini-chrome-scale 캡과 같은 자리를 가리켜야 한다 — 어긋나면 그림은 멈추는데 여백만 자란다.</summary>
    public const string ChromeCapStep = "compact";

    /// <summary>껍데기용 크기. 캡을 넘지 않는다. 아이콘 그림은 CSS 가 「조금작게」에서 멈추지만 단추의 좌우 여백은 DevExpress 가 SizeMode 로 정해 「보통」·「크게」에서 자라 아이콘 사이가 벌어진다. 여백을 CSS 로 덮으면 테마마다 어긋나므로 그 자리에만 이 값을 흘린다. 헤더 전체를 묶으면 서랍·사용자 판도 작아지므로 아이콘 단추가 서는 네 자리만 감싼다.</summary>
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
