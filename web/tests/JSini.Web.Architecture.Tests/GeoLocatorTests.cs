using System.Text.RegularExpressions;
using JSini.Web.Components.Layout;
using JSini.Web.Components.Settings;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>위치 배관 둘을 지킨다 — 움직임을 재는 자와 C# 과 JS 를 잇는 이름. 둘 다 틀려도 예외 없이 조용히 어긋난다(좌표가 안 따라오거나, 이름이 어긋나 자동 재수집이 멎는다).</summary>
public class GeoLocatorTests
{
    // ── 움직임을 재는 자 ──────────────────────────────────────

    /// <summary>위도 0.001도는 약 111m 로 문턱(300m)보다 작아야 한다 — GPS 가 흔든 것이지 자리를 옮긴 것이 아니다.</summary>
    [Fact]
    public void 백미터쯤_움직인_것은_자리가_바뀐_것이_아니다()
    {
        var moved = GeoLocator.DistanceMeters(35.5384, 129.3114, 35.5394, 129.3114);

        Assert.InRange(moved, 100, 120);
        Assert.True(moved < GeoLocator.MoveThresholdMeters);
    }

    /// <summary>위도 0.005도는 약 550m 다. 문턱을 넘어야 한다.</summary>
    [Fact]
    public void 오백미터_넘게_움직이면_자리가_바뀐_것이다()
    {
        var moved = GeoLocator.DistanceMeters(35.5384, 129.3114, 35.5434, 129.3114);

        Assert.InRange(moved, 500, 600);
        Assert.True(moved >= GeoLocator.MoveThresholdMeters);
    }

    /// <summary>경도를 위도와 같은 자로 재면 안 된다 — 우리 위도에서 경도 1도는 위도 1도의 약 0.81배라, 동서 움직임을 크게 봐서 가만히 있는 사람도 「이사」가 된다.</summary>
    [Fact]
    public void 동서_거리는_위도에_따라_줄어든다()
    {
        var northSouth = GeoLocator.DistanceMeters(35.5384, 129.3114, 35.5484, 129.3114);
        var eastWest = GeoLocator.DistanceMeters(35.5384, 129.3114, 35.5384, 129.3214);

        Assert.True(eastWest < northSouth);
        Assert.InRange(eastWest / northSouth, 0.78, 0.84);
    }

    /// <summary>같은 자리는 0 이다. 문턱을 넘지 않아야 조용한 확인이 헛돌지 않는다.</summary>
    [Fact]
    public void 같은_자리는_움직이지_않은_것이다()
    {
        Assert.Equal(0, GeoLocator.DistanceMeters(35.5384, 129.3114, 35.5384, 129.3114), 6);
    }

    // ── C# 과 JS 를 잇는 이름 ─────────────────────────────────

    /// <summary>C# 이 부르는 jsiniGeo.* 가 geo.js 에 실제로 있는가. 없는 이름을 부르면 GeoLocator 가 예외를 삼켜 「못 잡는 브라우저」로 다뤄 조용한 확인이 영영 멎어도 모른다.</summary>
    [Fact]
    public void C샤프가_부르는_jsiniGeo_함수가_geo_js_에_다_있다()
    {
        var js = RazorSource.Read(Path.Combine(WebRoot(),
            "src", "Shell", "JSini.Web.Shell", "wwwroot", "js", "geo.js"));

        var exported = Regex.Matches(js, @"function\s+(?<name>\w+)\s*\(")
            .Cast<Match>()
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        // 내보내는 목록(`window.jsiniGeo = { … }`)에 든 것만 부를 수 있다.
        var exposed = Regex.Match(js, @"window\.jsiniGeo\s*=\s*\{(?<body>[^}]*)\}");
        Assert.True(exposed.Success, "geo.js 가 jsiniGeo 를 내보내지 않는다.");

        var names = exposed.Groups["body"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split(':')[0].Trim())
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<string>();

        foreach (var file in SourceFiles())
        {
            foreach (var match in Regex.Matches(RazorSource.Read(file),
                         @"""jsiniGeo\.(?<fn>\w+)""").Cast<Match>())
            {
                var fn = match.Groups["fn"].Value;

                if (!names.Contains(fn) || !exported.Contains(fn))
                {
                    missing.Add($"{Path.GetFileName(file)} → jsiniGeo.{fn}");
                }
            }
        }

        Assert.True(missing.Count == 0,
            "geo.js 에 없는 이름을 부른다. 예외는 콘솔에만 찍히고 화면은 "
            + "「위치를 못 잡는 브라우저」로 조용히 넘어간다.\n"
            + string.Join('\n', missing));
    }

    /// <summary>조용한 확인은 권한이 허용된 경우에만 잰다 — 그 방패가 quiet 안에 있는지 글자로 확인한다. 빠지면 화면 전환마다 물음창이 튀어나온다.</summary>
    [Fact]
    public void 조용한_확인은_권한을_먼저_본다()
    {
        var js = RazorSource.Read(Path.Combine(WebRoot(),
            "src", "Shell", "JSini.Web.Shell", "wwwroot", "js", "geo.js"));

        var quiet = js[js.IndexOf("async function quiet(", StringComparison.Ordinal)..];
        quiet = quiet[..quiet.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.Contains("permission()", quiet, StringComparison.Ordinal);
        Assert.Contains("'granted'", quiet, StringComparison.Ordinal);
    }

    // ── 고를 수 있는 확인 간격 ────────────────────────────────
    //
    // 간격을 고르게 되면서 값이 두 곳(화면이 고른 값, 판정하는 쪽이 보는 값)에 생겼다.
    // 어긋나도 예외는 없고 「고른 간격과 도는 간격이 다르다」로만 나타난다.

    /// <summary>모르는 값은 기본값으로 떨어진다 — 저장소를 손으로 고쳤거나 고를 수 있는 값이 줄어든 뒤 옛 값이 남은 경우다.</summary>
    [Fact]
    public void 모르는_확인_간격은_기본값으로_본다()
    {
        Assert.Equal(PortalBoot.DefaultGeoSyncMinutes, PortalBoot.NormalizeGeoSyncMinutes(null));
        Assert.Equal(PortalBoot.DefaultGeoSyncMinutes, PortalBoot.NormalizeGeoSyncMinutes(7));
        Assert.Equal(PortalBoot.DefaultGeoSyncMinutes, PortalBoot.NormalizeGeoSyncMinutes(0));
        Assert.Equal(PortalBoot.DefaultGeoSyncMinutes, PortalBoot.NormalizeGeoSyncMinutes(-60));
    }

    /// <summary>고를 수 있는 값은 그대로 남는다.</summary>
    [Fact]
    public void 고를_수_있는_확인_간격은_그대로_남는다()
    {
        foreach (var minutes in PortalBoot.GeoSyncMinuteChoices)
        {
            Assert.Equal(minutes, PortalBoot.NormalizeGeoSyncMinutes(minutes));
        }
    }

    /// <summary>기본값이 고를 수 있는 목록 안에 있어야 한다 — 없으면 고르개가 빈 칸으로 열린다.</summary>
    [Fact]
    public void 기본_확인_간격은_고르개_목록에_있다()
    {
        Assert.Contains(PortalBoot.DefaultGeoSyncMinutes, PortalBoot.GeoSyncMinuteChoices);
    }

    /// <summary>고른 적이 없을 때의 간격을 두 곳이 적고 있다 — GeoLocator.SyncInterval 과 PortalBoot.DefaultGeoSyncMinutes 가 같아야 한다.</summary>
    [Fact]
    public void 기본_간격은_두_곳에서_같다()
    {
        Assert.Equal(
            GeoLocator.SyncInterval,
            TimeSpan.FromMinutes(PortalBoot.DefaultGeoSyncMinutes));
    }

    /// <summary>한 시간 칸이 있어야 한다 — 가장 촘촘한 발송 간격이 세 시간이라 그보다 촘촘한 확인이 없으면 낮에 옮긴 좌표가 다음 발송에 못 댄다.</summary>
    [Fact]
    public void 한_시간_칸이_있다()
    {
        Assert.Contains(60, PortalBoot.GeoSyncMinuteChoices);
    }

    /// <summary>고른 적이 없을 때 30분이어야 한다 — 좌표가 지나온 자리(location_tracks)로도 쌓여, 간격이 곧 이동 경로의 성김이다.</summary>
    [Fact]
    public void 기본_확인_간격은_삼십분이다()
    {
        Assert.Equal(30, PortalBoot.DefaultGeoSyncMinutes);
        Assert.Equal(TimeSpan.FromMinutes(30), GeoLocator.SyncInterval);
    }

    /// <summary>조용한 확인이 받아 쓰는 묵은 좌표의 한도(maximumAge)는 재는 간격보다 넉넉히 짧아야 한다 — 같아지면 경로가 한 칸씩 밀린다.</summary>
    [Fact]
    public void 조용한_확인은_간격보다_훨씬_덜_묵은_좌표만_받아_쓴다()
    {
        var js = RazorSource.Read(Path.Combine(WebRoot(),
            "src", "Shell", "JSini.Web.Shell", "wwwroot", "js", "geo.js"));

        var quiet = js[js.IndexOf("async function quiet(", StringComparison.Ordinal)..];
        quiet = quiet[..quiet.IndexOf("\n    }", StringComparison.Ordinal)];

        var found = Regex.Match(quiet, @"maximumAge:\s*(\d+)");

        Assert.True(found.Success, "조용한 확인에 maximumAge 가 적혀 있어야 한다.");

        var maxAge = TimeSpan.FromMilliseconds(int.Parse(found.Groups[1].Value));

        Assert.True(maxAge <= GeoLocator.SyncInterval / 4,
            $"묵은 좌표 한도({maxAge}) 가 확인 간격({GeoLocator.SyncInterval}) 의 1/4 보다 길다.");
    }

    // ── 화면 전환이 없어도 재는가 ─────────────────────────────

    /// <summary>권유 창에 시계가 걸려 있어야 한다 — 살핌이 레이아웃 재생성 하나에만 걸려 있으면 한 화면을 열어 둔 사람의 좌표가 간격이 지나도 안 따라온다. 글자로 지킨다.</summary>
    [Fact]
    public void 위치_권유창은_다음_잴_때에_시계를_건다()
    {
        var source = RazorSource.Read(Path.Combine(WebRoot(),
            "src", "Shared", "JSini.Web.Components", "Layout", "LocationAskPopup.razor"));

        Assert.Contains("ScheduleNextSync", source, StringComparison.Ordinal);
        Assert.Contains("Task.Delay", source, StringComparison.Ordinal);

        // 걷는 길이 없으면 업무를 옮길 때마다 시계가 하나씩 쌓인다.
        Assert.Contains("IDisposable", source, StringComparison.Ordinal);
        Assert.Contains("_clock?.Cancel()", source, StringComparison.Ordinal);
    }

    /// <summary>시계가 깨울 때는 창을 띄우지 않는다 — 「나중에」로 닫은 사람에게 같은 창을 다시 미는 것은 조르는 것이다.</summary>
    [Fact]
    public void 시계가_깨우는_길은_창을_띄우지_않는다()
    {
        var source = RazorSource.Read(Path.Combine(WebRoot(),
            "src", "Shared", "JSini.Web.Components", "Layout", "LocationAskPopup.razor"));

        var wake = source[source.IndexOf(
            "private async Task SyncOnScheduleAsync(", StringComparison.Ordinal)..];
        wake = wake[..wake.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.DoesNotContain("_open = true", wake, StringComparison.Ordinal);
        Assert.Contains("SyncQuietlyAsync", wake, StringComparison.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>검사할 <c>.cs</c>·<c>.razor</c>. <c>JsIdentifierTests</c> 와 같은 방식이다.</summary>
    private static IEnumerable<string> SourceFiles() =>
        Directory
            .EnumerateFiles(Path.Combine(WebRoot(), "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
