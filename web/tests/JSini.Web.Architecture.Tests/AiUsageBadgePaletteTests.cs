using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>AI 사용량 배지 색띠(.jsini-badge--usage-*) 검증. 이름이 갈려도 색이 안 갈리면 고쳐진 것이 아니라서, 두 수식어가 가리키는 색의 차이를 흰 판 위에서 잰다.
/// CSS 는 빌드가 못 보므로 정본 두 벌(app.css · projmng.css)을 글자로 읽는다.</summary>
public sealed class AiUsageBadgePaletteTests
{
    /// <summary>맨 마지막에 실리는 스타일시트. 바탕 규칙(.jsini-badge)을 다시 까므로 색띠가 여기 없으면 앞의 두 벌은 무의미하다.
    /// 싣는 차례는 JSiniHead.razor 가 정한다.</summary>
    private const string LastSheetName = "tabler.css";

    private static readonly string LastSheet =
        Path.Combine("src", "Shared", "JSini.Web.Components", "wwwroot", LastSheetName);

    /// <summary>색띠를 들고 있는 스타일시트 정본 세 벌. 모듈 CSS 는 셸을 거치지 않고 따로 실리고 Tabler 가 맨 마지막에 한 벌 더 실리므로, 갈라지면 화면마다 다른 색이 된다.
    /// tabler.css 를 뒤늦게 들였을 때 앞의 둘에만 깔린 색띠가 전부 회색이 된 적이 있다 — 바탕 규칙이 가장 늦게 실려 자릿수가 같은 --usage-* 가 졌다. 이름만 보는 검사로는 안 잡힌다.</summary>
    private static readonly string[] Sheets =
    [
        Path.Combine("src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"),
        Path.Combine("src", "Apps", "JSini.Web.ProjMng", "wwwroot", "projmng.css"),
        LastSheet,
    ];

    public static TheoryData<string> StyleSheets() => [.. Sheets];

    /// <summary>0, 5, 10 … 100 — 스물한 칸.</summary>
    private static readonly int[] Steps = [.. Enumerable.Range(0, 21).Select(i => i * 5)];

    /// <summary>글자색이 흰색으로 넘어가는 자리. 그 아래는 먹색이다.</summary>
    private const int WhiteInkFrom = 70;

    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void 스물한_칸이_모두_정의되어_있다(string relative)
    {
        var palette = Palette(relative);

        Assert.Equal(Steps.ToList(), palette.Keys.Order().ToList());
    }

    /// <summary>바탕이 투명하면 안 된다 — 알파로 깔면 스물한 칸이 전부 흰색 가까이로 몰린다.</summary>
    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void 바탕색은_불투명한_값이다(string relative)
    {
        foreach (var (pct, (_, _)) in Palette(relative))
        {
            var raw = RawBackground(relative, pct);

            Assert.False(
                raw.Contains("rgba", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("transparent", StringComparison.OrdinalIgnoreCase),
                $"{pct}% 의 바탕이 투명하다: {raw}");
        }
    }

    /// <summary>쓸수록 짙어진다. 밝기가 한 번도 되튀지 않고 내려가야 「아래쪽일수록 많이 썼다」로 읽힌다.</summary>
    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void 밝기가_단조롭게_떨어진다(string relative)
    {
        var palette = Palette(relative);

        foreach (var (lower, upper) in Steps.Zip(Steps.Skip(1)))
        {
            var a = Lab(palette[lower].Background)[0];
            var b = Lab(palette[upper].Background)[0];

            Assert.True(b < a, $"{upper}% 가 {lower}% 보다 밝다 (L* {b:0.0} ≥ {a:0.0})");
        }
    }

    /// <summary>이웃한 두 칸이 눈에 갈려야 한다. ΔE 5 는 작은 딱지에서 겨우 알아볼 만한 선이다.</summary>
    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void 이웃한_칸끼리_색이_갈린다(string relative)
    {
        var palette = Palette(relative);

        foreach (var (lower, upper) in Steps.Zip(Steps.Skip(1)))
        {
            var delta = DeltaE(palette[lower].Background, palette[upper].Background);

            Assert.True(delta >= 5, $"{lower}% 와 {upper}% 의 색차가 ΔE {delta:0.0} 뿐이다");
        }
    }

    /// <summary>70% 와 95%(=99% 가 떨어지는 칸)는 한눈에 갈려야 한다 — 이름만 갈리고 색이 같은 고장을 잡는다.</summary>
    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void 칠십퍼센트와_구십구퍼센트는_한눈에_갈린다(string relative)
    {
        var palette = Palette(relative);
        var delta = DeltaE(palette[70].Background, palette[95].Background);

        Assert.True(delta >= 30, $"70% 와 99% 의 색차가 ΔE {delta:0.0} 뿐이다 (고치기 전 13.1)");
    }

    /// <summary>글자가 바탕 위에서 읽혀야 한다 — WCAG AA(4.5:1).</summary>
    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void 글자가_바탕_위에서_읽힌다(string relative)
    {
        foreach (var (pct, entry) in Palette(relative))
        {
            var contrast = Contrast(entry.Background, entry.Foreground);

            Assert.True(contrast >= 4.5, $"{pct}% 의 글자 대비가 {contrast:0.00}:1 뿐이다");
        }
    }

    /// <summary>짙은 칸에만 흰 글자를 쓴다 — 밝은 바탕에 흰 글자가 얹히면 대비 검사는 통과해도 눈에는 날아간다.</summary>
    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void 흰_글자는_짙은_칸에만_쓴다(string relative)
    {
        foreach (var (pct, entry) in Palette(relative))
        {
            var white = entry.Foreground is (255, 255, 255);

            Assert.True(white == (pct >= WhiteInkFrom), $"{pct}% 의 글자색이 자리에 안 맞는다");
        }
    }

    /// <summary>테두리를 적지 않는다 — 바탕 규칙이 border: 0 이라 한 픽셀도 안 그려지고, 적어 두면 안 갈리는 칸을 갈린다고 착각하게 만든다.</summary>
    [Theory]
    [MemberData(nameof(StyleSheets))]
    public void 안_그려지는_테두리색을_적지_않는다(string relative)
    {
        foreach (var pct in Steps)
        {
            Assert.DoesNotContain("border-color", Declaration(relative, pct));
        }
    }

    /// <summary>세 정본이 같은 색띠를 들고 있다.</summary>
    [Fact]
    public void 공통과_모듈_스타일시트의_색띠가_같다()
    {
        var first = Palette(Sheets[0]);

        foreach (var other in Sheets.Skip(1))
        {
            Assert.Equal(first, Palette(other));
        }
    }

    /// <summary>맨 마지막 스타일시트 안에서도 차례가 맞아야 한다 — 색띠가 바탕 규칙보다 앞에 적히면 뒤엣것이 이겨 다시 회색이 된다.</summary>
    [Fact]
    public void 마지막_스타일시트에서_색띠가_바탕_규칙보다_뒤에_있다()
    {
        var css = File.ReadAllText(Path.Combine(SolutionRoot(), LastSheet));

        var baseAt = css.LastIndexOf(".jsini-badge {", StringComparison.Ordinal);
        var paletteAt = css.IndexOf(".jsini-badge--usage-0", StringComparison.Ordinal);

        Assert.True(baseAt >= 0, $"{LastSheetName} 에 .jsini-badge 바탕 규칙이 없다");
        Assert.True(paletteAt > baseAt, $"{LastSheetName} 의 색띠가 바탕 규칙보다 앞에 있다");
    }

    // ── 읽기 ──────────────────────────────────────────────────────

    private readonly record struct Entry((int R, int G, int B) Background, (int R, int G, int B) Foreground);

    /// <summary>한 칸의 선언부(<c>{ … }</c> 안쪽) 원문.</summary>
    private static string Declaration(string relative, int pct)
    {
        var path = Path.Combine(SolutionRoot(), relative);
        Assert.True(File.Exists(path), $"파일 없음: {path}");

        var css = File.ReadAllText(path);
        var match = Regex.Match(css, $@"^\.jsini-badge--usage-{pct}\s*\{{([^}}]*)\}}", RegexOptions.Multiline);

        Assert.True(match.Success, $"{relative} 에 {pct}% 칸이 없다");
        return match.Groups[1].Value;
    }

    private static string RawBackground(string relative, int pct)
        => Regex.Match(Declaration(relative, pct), @"background:\s*([^;]+)").Groups[1].Value.Trim();

    private static Dictionary<int, Entry> Palette(string relative)
    {
        var palette = new Dictionary<int, Entry>();

        foreach (var pct in Steps)
        {
            var body = Declaration(relative, pct);

            palette[pct] = new Entry(
                Hex(Regex.Match(body, @"background:\s*(#[0-9a-fA-F]{6})").Groups[1].Value),
                Hex(Regex.Match(body, @"(?<!-)color:\s*(#[0-9a-fA-F]{6})").Groups[1].Value));
        }

        return palette;
    }

    private static (int R, int G, int B) Hex(string hex)
    {
        Assert.Equal(7, hex.Length);

        static int Part(string hex, int at)
            => int.Parse(hex.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return (Part(hex, 1), Part(hex, 3), Part(hex, 5));
    }

    // ── 색 재기 ───────────────────────────────────────────────────

    private static double Linear(int channel)
    {
        var v = channel / 255.0;

        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>WCAG 상대 휘도.</summary>
    private static double Luminance((int R, int G, int B) c)
        => (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));

    private static double Contrast((int R, int G, int B) a, (int R, int G, int B) b)
    {
        var (hi, lo) = (Luminance(a), Luminance(b));

        return hi < lo ? (lo + 0.05) / (hi + 0.05) : (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>CIELAB. 밝기(L*)와 색차(ΔE)를 재려고 쓴다.</summary>
    private static double[] Lab((int R, int G, int B) c)
    {
        double r = Linear(c.R), g = Linear(c.G), b = Linear(c.B);

        var x = (((0.4124 * r) + (0.3576 * g) + (0.1805 * b)) / 0.95047).Pivot();
        var y = ((0.2126 * r) + (0.7152 * g) + (0.0722 * b)).Pivot();
        var z = (((0.0193 * r) + (0.1192 * g) + (0.9505 * b)) / 1.08883).Pivot();

        return [(116 * y) - 16, 500 * (x - y), 200 * (y - z)];
    }

    private static double DeltaE((int R, int G, int B) a, (int R, int G, int B) b)
    {
        double[] p = Lab(a), q = Lab(b);

        return Math.Sqrt(Math.Pow(p[0] - q[0], 2) + Math.Pow(p[1] - q[1], 2) + Math.Pow(p[2] - q[2], 2));
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}

file static class LabPivot
{
    /// <summary>CIELAB 의 세제곱근 꺾임.</summary>
    internal static double Pivot(this double t)
        => t > 0.008856 ? Math.Cbrt(t) : ((7.787 * t) + (16.0 / 116.0));
}
