using System.Text.RegularExpressions;

using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 화면은 <see cref="DateTime.Now"/> · <see cref="DateTime.Today"/> 를 쓰지 않는다.
///
/// <para>
/// [왜 막는가]
/// </para>
///
/// <para>
/// 이 포털은 <b>Blazor Server</b> 라 그 둘은 「보는 사람의 시각」이 아니라
/// <b>서버 프로세스의 시각</b>이다. 그런데 서버가 주는 시각 값은 전부 UTC 이고
/// (DB 의 시각 칸이 모두 <c>timestamptz</c>다 —
/// <c>deploy/sql/utc-timestamptz-2026-09-29.sql</c>), 운영 컨테이너의 시계도
/// UTC 다(<c>deploy/docker</c> 의 <c>TZ=Etc/UTC</c>).
/// </para>
///
/// <para>
/// <b>그래서 틀림이 개발 장비에서만 난다.</b> 개발 장비는 한국 시각이라
/// <c>DateTime.Now - 서버가준시각</c> 이 아홉 시간 어긋나는데, 운영에 올리면
/// 그 차이가 사라진다 — 재현되는 자리와 드러나는 자리가 반대라 가장 늦게
/// 들키는 종류의 틀림이다. 반대로 「달력의 오늘」을 <c>DateTime.Today</c> 로
/// 물으면 <b>운영에서만</b> 틀린다(한국의 오전 9시 전 아홉 시간이 어제다).
/// </para>
///
/// <para>
/// 대신 <c>AppTime</c> 을 쓴다 — 견주기는 <c>AppTime.UtcNow</c>, 달력 날짜는
/// <c>AppTime.Today</c>, 보여 줄 때만 <c>Kst(...)</c>. 자세한 것은
/// <c>docs/utc-time.md</c>.
/// </para>
/// </summary>
public sealed class UtcTimeTests
{
    /// <summary>
    /// 문서 주석(<c>///</c>)과 줄 주석은 세지 않는다 — <c>AppTime</c> 머리말이
    /// 「그 둘을 쓰지 말라」고 적으려면 그 이름을 적을 수밖에 없다.
    /// </summary>
    private static readonly Regex Forbidden = new(
        @"(?<!///.*)\bDateTime\.(Now|Today)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// 규칙을 담고 있는 자리. <b>여기만 그 둘을 부를 수 있다</b> —
    /// 부르는 자리가 하나면 한 번 고쳐 전부가 따라온다.
    /// </summary>
    private static readonly string[] Allowed =
    [
        "src/Shared/JSini.Web.Components/Data/AppTime.cs",
        "src/Site/JSini.PublicSite/Components/Shared/SiteTime.cs",
    ];

    [Fact]
    public void 화면은_서버시계를_직접_묻지_않는다()
    {
        var offenders = SourceFiles()
            .Select(f => (Path: Relative(f), Lines: File.ReadAllLines(f)))
            .Where(x => !Allowed.Contains(x.Path, StringComparer.Ordinal))
            .SelectMany(x => x.Lines
                .Select((line, i) => (x.Path, No: i + 1, Line: line))
                .Where(l => Forbidden.IsMatch(Strip(l.Line))))
            .Select(l => $"{l.Path}:{l.No}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "`DateTime.Now`·`DateTime.Today` 는 서버 프로세스의 시각이라 "
            + "서버가 주는 UTC 값과 섞이면 아홉 시간 어긋난다. "
            + "견주기는 `AppTime.UtcNow`, 달력 날짜는 `AppTime.Today`(`TodayDate`), "
            + "보여 줄 때는 `Kst(...)` 를 쓴다(docs/utc-time.md).\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <c>AppTime</c> 은 <b>업무 앱 전부가 닿는 자리</b>에 있어야 한다.
    /// 한 앱 안으로 내려가면 나머지가 각자 베껴 적고, 베낀 것부터 갈라진다.
    /// </summary>
    [Fact]
    public void 시계는_공용자리에_하나다()
    {
        var homes = SourceFiles()
            .Where(f => File.ReadAllText(f).Contains("public static class AppTime", StringComparison.Ordinal))
            .Select(Relative)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["src/Shared/JSini.Web.Components/Data/AppTime.cs"], homes);
    }

    /// <summary>주석 부분을 걷어낸다. 글자 안의 <c>//</c> 까지 가리지는 않는다.</summary>
    private static string Strip(string line)
    {
        var at = line.IndexOf("//", StringComparison.Ordinal);
        var trimmed = at >= 0 ? line[..at] : line;

        // Razor 의 주석도 같이 걷어낸다.
        return Regex.Replace(trimmed, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(SolutionRoot(), path).Replace(Path.DirectorySeparatorChar, '/');

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

    private static IEnumerable<string> SourceFiles() =>
        new[] { "*.cs", "*.razor" }
            .SelectMany(pattern => Directory.EnumerateFiles(
                Path.Combine(SolutionRoot(), "src"), pattern, SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
