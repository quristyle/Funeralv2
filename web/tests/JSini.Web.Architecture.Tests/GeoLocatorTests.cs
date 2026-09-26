using System.Text.RegularExpressions;
using JSini.Web.Components.Layout;
using JSini.Web.Components.Settings;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 위치 배관 둘을 지킨다 — <b>움직임을 재는 자</b>와 <b>C# 과 JS 를 잇는 이름</b>.
///
/// <para>
/// 둘 다 <b>틀려도 아무 일이 일어나지 않는</b> 자리다. 자가 틀리면 좌표가
/// 조용히 안 따라오거나 반대로 GPS 가 흔들 때마다 「잡은 때」가 갱신되고,
/// 이름이 어긋나면 브라우저 콘솔에만 예외가 찍힌 채 자동 재수집이 멎는다.
/// </para>
/// </summary>
public class GeoLocatorTests
{
    // ── 움직임을 재는 자 ──────────────────────────────────────

    /// <summary>
    /// 위도 0.001도는 약 111m 다. <b>문턱(300m)보다 작아야 한다</b> — 이 정도는
    /// 자리를 옮긴 것이 아니라 GPS 가 흔든 것이다.
    /// </summary>
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

    /// <summary>
    /// <b>경도를 위도와 같은 자로 재면 안 된다.</b> 우리 위도에서 경도 1도는
    /// 위도 1도의 약 0.81배라, 도 단위 차를 그대로 쓰면 동서 방향 움직임을
    /// 실제보다 크게 본다 — 그러면 가만히 있는 사람도 자꾸 「이사」가 된다.
    /// </summary>
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

    /// <summary>
    /// C# 이 부르는 <c>jsiniGeo.*</c> 가 <c>geo.js</c> 에 실제로 있는가.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>없는 이름을 불러도 화면은 멀쩡하다.</b> Blazor 는 식별자를 못 찾으면
    /// 던지는데 그 예외는 <c>GeoLocator</c> 가 삼켜(로그로만 남긴다) 「못 잡는
    /// 브라우저」로 다룬다 — 조용한 확인이 영영 멎어도 아무도 모른다.
    /// <c>JsIdentifierTests</c> 가 막는 함정(괄호)과 갈래가 다르다: 저쪽은
    /// 모양이 틀린 것이고 이쪽은 <b>이름이 없어진 것</b>이다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 조용한 확인은 <b>권한이 허용된 경우에만</b> 재야 한다. 그 방패가
    /// <c>quiet</c> 안에 있는지 글자로 확인한다 — 빠지면 아무도 부르지 않은
    /// 물음창이 화면 전환마다 튀어나온다.
    /// </summary>
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
    // 간격을 사람이 고르게 되면서(환경설정의 「위치 확인 간격」) 값이 두 곳에
    // 생겼다 — 화면이 고른 값과 판정하는 쪽이 보는 값. **어긋나도 예외가 나지
    // 않고** 「고른 간격과 실제로 도는 간격이 다르다」로만 나타난다.

    /// <summary>
    /// 모르는 값은 기본값으로 떨어진다. 사람이 저장소를 손으로 고쳤거나
    /// 고를 수 있는 값이 줄어든 뒤에 옛 값이 남은 경우다.
    /// </summary>
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

    /// <summary>
    /// <b>기본값이 고를 수 있는 목록 안에 있어야 한다.</b> 없으면 고르개가
    /// 빈 칸으로 열리고, 사람은 간격이 정해져 있지 않다고 읽는다.
    /// </summary>
    [Fact]
    public void 기본_확인_간격은_고르개_목록에_있다()
    {
        Assert.Contains(PortalBoot.DefaultGeoSyncMinutes, PortalBoot.GeoSyncMinuteChoices);
    }

    /// <summary>
    /// <b>고른 적이 없을 때의 간격 하나를 두 곳이 적고 있다.</b>
    /// <c>GeoLocator.SyncInterval</c>(머리말이 설명하는 값)과
    /// <c>PortalBoot.DefaultGeoSyncMinutes</c>(고르개의 기본값)이 같아야 한다.
    /// </summary>
    [Fact]
    public void 기본_간격은_두_곳에서_같다()
    {
        Assert.Equal(
            GeoLocator.SyncInterval,
            TimeSpan.FromMinutes(PortalBoot.DefaultGeoSyncMinutes));
    }

    /// <summary>
    /// 한 시간 칸이 있어야 한다. 사람이 고를 수 있는 가장 촘촘한 발송 간격이
    /// 세 시간이라, 그보다 촘촘한 확인이 없으면 낮에 자리를 옮긴 좌표가
    /// 다음 발송에 못 댄다(<c>GeoLocator.SyncInterval</c> 머리말).
    /// </summary>
    [Fact]
    public void 한_시간_칸이_있다()
    {
        Assert.Contains(60, PortalBoot.GeoSyncMinuteChoices);
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
