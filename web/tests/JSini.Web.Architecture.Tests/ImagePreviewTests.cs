using System.Text.Json;
using System.Text.RegularExpressions;
using JSini.Web.Components.Data;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>공통 그림 미리보기(ImagePreview · ImagePreviewHost · image-preview.js). 막으려는 것은 조용한 어긋남 하나다.
/// 보이는 모습의 정본은 브라우저에 있고 C# 은 JS 가 돌려주는 값을 ImagePreviewState 로 받는데, 두 모양이 어긋나도 예외 없이 「단추가 안 눌린다」로만 나타난다.</summary>
public sealed class ImagePreviewTests
{
    /// <summary>Blazor 의 JS 상호 운용이 쓰는 것과 같은 규칙(camelCase · 대소문자 무시).</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>JS 가 돌려주는 글자를 그대로 받는가. 본문은 image-preview.js 의 snapshot() 이 만드는 모양이다.</summary>
    [Fact]
    public void 브라우저가_돌려주는_모습을_그대로_받는다()
    {
        var state = JsonSerializer.Deserialize<ImagePreviewState>("""
            {
              "zoom": 2.4883199999999997,
              "rotate": 90,
              "flipX": true,
              "flipY": false,
              "gray": true,
              "invert": false,
              "brightness": 80,
              "contrast": 110
            }
            """, Json)!;

        Assert.Equal(2.48832, state.Zoom, 5);
        Assert.Equal(90, state.Rotate);
        Assert.True(state.FlipX);
        Assert.False(state.FlipY);
        Assert.True(state.Gray);
        Assert.False(state.Invert);
        Assert.Equal(80, state.Brightness);
        Assert.Equal(110, state.Contrast);
    }

    /// <summary>snapshot() 이 담는 칸과 ImagePreviewState 의 칸이 같은가 — 한쪽에만 칸을 늘리면 그 값은 도구띠에 영영 닿지 않는다.</summary>
    [Fact]
    public void 브라우저와_C샵이_같은_칸을_주고받는다()
    {
        var js = File.ReadAllText(ModulePath());

        // `function snapshot() { … return { … }; }` 의 그 객체만 떼어 낸다.
        var body = Regex.Match(js, @"function snapshot\(\)(?s).*?return \{(?<keys>.*?)\};");
        Assert.True(body.Success, "image-preview.js 에서 snapshot() 을 찾지 못했다");

        var jsKeys = Regex.Matches(body.Groups["keys"].Value, @"^\s*(?<k>[a-zA-Z]+):", RegexOptions.Multiline)
            .Select(m => m.Groups["k"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var csKeys = typeof(ImagePreviewState)
            .GetProperties()
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(jsKeys.SetEquals(csKeys),
            $"JS: {string.Join(", ", jsKeys.Order())} / C#: {string.Join(", ", csKeys.Order())}");
    }

    /// <summary>도구띠의 단추 이름이 둘 다에 있는가. C# 은 이름을 글자로 넘겨 오타를 컴파일러가 못 잡고, JS 의 switch 는 모르는 이름을 조용히 넘기므로 오타 난 단추는 눌러도 아무 일이 없다.</summary>
    [Fact]
    public void 도구띠_단추를_브라우저가_전부_안다()
    {
        var js = File.ReadAllText(ModulePath());

        var known = Regex.Matches(js, @"case '(?<n>[a-zA-Z]+)':")
            .Select(m => m.Groups["n"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var used = Regex.Matches(File.ReadAllText(HostPath()), @"new\(""(?<n>[a-zA-Z]+)"",")
            .Select(m => m.Groups["n"].Value)
            .ToList();

        Assert.NotEmpty(used);

        var missing = used.Where(n => !known.Contains(n)).ToList();
        Assert.True(missing.Count == 0, "브라우저가 모르는 단추: " + string.Join(", ", missing));

        // 마크업이 직접 부르는 둘은 목록에 없다.
        Assert.Contains("zoomIn", known);
        Assert.Contains("zoomOut", known);
    }

    /// <summary>한 장을 띄운다.</summary>
    [Fact]
    public void 한_장을_띄운다()
    {
        var preview = new ImagePreview();
        var bumped = 0;
        preview.Changed += () => bumped++;

        Assert.False(preview.IsOpen);

        preview.Open("/files/abc", "장애 사진.png");

        Assert.True(preview.IsOpen);
        Assert.Equal(1, preview.Count);
        Assert.Equal("/files/abc", preview.Current!.Url);
        Assert.Equal("장애 사진.png", preview.Current.Title);
        Assert.Equal(1, bumped);
    }

    /// <summary>주소가 없으면 창이 안 열린다 — 막지 않으면 빈 검은 화면이 뜨고, 왜 비었는지 적을 자리도 없다.</summary>
    [Fact]
    public void 주소가_없으면_열지_않는다()
    {
        var preview = new ImagePreview();
        var bumped = 0;
        preview.Changed += () => bumped++;

        preview.Open(null);
        preview.Open("   ");
        preview.OpenAll([]);
        preview.OpenAll([new ImagePreviewItem("")]);

        Assert.False(preview.IsOpen);
        Assert.Null(preview.Current);
        Assert.Equal(0, bumped);
    }

    /// <summary>여러 장이면 <b>끝에서 되돌아 온다</b>.</summary>
    [Fact]
    public void 앞뒤로_넘기면_끝에서_되돌아_온다()
    {
        var preview = new ImagePreview();

        preview.OpenAll([
            new ImagePreviewItem("/files/1"),
            new ImagePreviewItem("/files/2"),
            new ImagePreviewItem("/files/3"),
        ], index: 2);

        Assert.Equal(2, preview.Index);

        preview.Step(1);
        Assert.Equal(0, preview.Index);

        preview.Step(-1);
        Assert.Equal(2, preview.Index);
    }

    /// <summary>한 장뿐이면 넘길 곳이 없다 — 다시 그리지도 않는다.</summary>
    [Fact]
    public void 한_장뿐이면_넘기지_않는다()
    {
        var preview = new ImagePreview();
        preview.Open("/files/only");

        var bumped = 0;
        preview.Changed += () => bumped++;

        preview.Step(1);
        preview.Step(-1);

        Assert.Equal(0, preview.Index);
        Assert.Equal(0, bumped);
    }

    /// <summary>범위를 벗어난 자리는 가장자리로 잡아 준다.</summary>
    [Fact]
    public void 시작_자리가_범위를_벗어나면_가장자리다()
    {
        var preview = new ImagePreview();

        preview.OpenAll([new ImagePreviewItem("/files/1"), new ImagePreviewItem("/files/2")], index: 9);
        Assert.Equal(1, preview.Index);

        preview.OpenAll([new ImagePreviewItem("/files/1"), new ImagePreviewItem("/files/2")], index: -3);
        Assert.Equal(0, preview.Index);
    }

    /// <summary>닫으면 비고, 이미 닫혀 있으면 아무 일도 하지 않는다 — 닫힌 창을 다시 닫을 때마다 화면을 그리면 창 없는 화면이 까닭 없이 깜빡인다.</summary>
    [Fact]
    public void 닫으면_비고_두_번_닫아도_조용하다()
    {
        var preview = new ImagePreview();
        preview.Open("/files/abc");

        var bumped = 0;
        preview.Changed += () => bumped++;

        preview.Close();
        Assert.False(preview.IsOpen);
        Assert.Null(preview.Current);
        Assert.Equal(1, bumped);

        preview.Close();
        Assert.Equal(1, bumped);
    }

    /// <summary>「내려받기」는 따로 준 주소로, 안 주면 보고 있는 주소로 간다.</summary>
    [Fact]
    public void 내려받기_주소는_안_주면_보는_주소다()
    {
        Assert.Equal("/files/abc", new ImagePreviewItem("/files/abc").Download);
        Assert.Equal("/files/abc?name=x.png",
            new ImagePreviewItem("/files/abc", null, "/files/abc?name=x.png").Download);
    }

    private static string ModulePath() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "js", "image-preview.js");

    private static string HostPath() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "ImagePreviewHost.razor.cs");

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
