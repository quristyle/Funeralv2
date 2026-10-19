using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>AddHttpClient&lt;T&gt; 로 등록하는 타입 이름은 저장소 전체에서 유일해야 한다. 클라이언트 이름은 네임스페이스를 빼고 타입 이름만으로 짓기 때문에, 같은 이름이면 나중에 등록되는 쪽이 던진다.
/// 포털은 그 모듈만 반쯤 등록된 채 떠서 화면은 열리고 그 서비스를 쓰는 동작에서만 죽는다. 빌드 때 막고, 겹치면 모듈의 뜻이 담긴 이름으로 바꾼다.</summary>
public sealed class HttpClientNamingTests
{
    /// <summary>AddHttpClient&lt;Foo&gt;( 에서 Foo 를 집는다. 이름을 직접 주는 오버로드는 겹치지 않으므로 보지 않는다.</summary>
    private static readonly Regex TypedClient =
        new(@"AddHttpClient<\s*([A-Za-z_][A-Za-z0-9_]*)\s*>", RegexOptions.Compiled);

    [Fact]
    public void AddHttpClient_로_등록하는_타입_이름이_겹치지_않는다()
    {
        var seen = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in SourceFiles())
        {
            foreach (Match match in TypedClient.Matches(RazorSource.Read(file)))
            {
                var name = match.Groups[1].Value;

                if (!seen.TryGetValue(name, out var places))
                {
                    places = [];
                    seen[name] = places;
                }

                var where = Path.GetFileName(file);

                if (!places.Contains(where))
                {
                    places.Add(where);
                }
            }
        }

        var clashes = seen
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key} ← {string.Join(" · ", pair.Value)}")
            .ToList();

        Assert.True(
            clashes.Count == 0,
            "AddHttpClient<T> 의 타입 이름이 겹칩니다. 클라이언트 이름은 네임스페이스를\n"
            + "빼고 짓기 때문에, 나중 등록이 던지고 그 모듈은 서비스가 반만 등록된 채 뜹니다.\n"
            + "(예외는 PortalModuleRegistry 가 삼켜서 화면에는 아무 표시도 나지 않습니다)\n  "
            + string.Join("\n  ", clashes));
    }

    /// <summary>
    /// 검사할 <c>.cs</c>·<c>.razor</c> 파일들. 빌드 산출물은 뺀다.
    /// </summary>
    private static IEnumerable<string> SourceFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return Directory
            .EnumerateFiles(Path.Combine(dir!.FullName, "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }
}
