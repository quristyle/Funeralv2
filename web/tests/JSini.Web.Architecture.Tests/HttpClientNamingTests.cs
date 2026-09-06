using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <c>AddHttpClient&lt;T&gt;</c> 로 등록하는 타입 이름은 <b>저장소 전체에서 유일해야 한다.</b>
///
/// <para>
/// [실제로 밟았다 — 모듈 하나가 반쯤 등록된 채로 떴다]
/// </para>
///
/// <para>
/// <c>AddHttpClient&lt;T&gt;</c> 는 클라이언트 이름을 <b>네임스페이스를 빼고
/// 타입 이름만으로</b> 짓는다. 그래서 <c>JSini.Web.Funeral.Api.FileUploadClient</c>
/// 와 <c>JSini.Web.Admin.Api.FileUploadClient</c> 가 <b>같은 이름</b>이 되고,
/// 나중에 등록되는 쪽이 던진다 —
/// </para>
///
/// <code>
/// The HttpClient factory already has a registered client with the name
/// 'FileUploadClient'. Client names are computed based on the type name
/// without considering the namespace.
/// </code>
///
/// <para>
/// <b>그 예외가 어디에도 드러나지 않는 것이 진짜 문제다.</b>
/// </para>
///
/// <list type="bullet">
///   <item>
///     <c>PortalModuleRegistry</c> 가 「모듈 하나가 깨져도 포털은 뜬다」로
///     잡아 삼킨다. 로그에 한 줄 남을 뿐이다.
///   </item>
///   <item>
///     모듈은 <c>ConfigureServices</c> 를 부르기 <b>전에</b> 이미 목록에
///     들어가 있어서, 기동 대조(<c>PortalApps</c>)와 첫 화면의 진단도 통과한다.
///   </item>
///   <item>
///     결과는 <b>그 모듈의 서비스 절반만 등록된 채 뜨는 것</b>이다. 화면은
///     열리고 메뉴도 돌아간다. 그 서비스를 실제로 쓰는 동작에서만 죽는다.
///   </item>
/// </list>
///
/// <para>
/// 그래서 빌드 때 막는다. 이름이 겹치면 <b>둘 중 하나를 그 모듈의 뜻이 담긴
/// 이름으로 바꾼다</b>(<c>NoticeUploadClient</c>). 세 번째 모듈이 같은 것을
/// 필요로 하면 그때는 복제가 아니라 승격이다(web/CLAUDE.md).
/// </para>
/// </summary>
public sealed class HttpClientNamingTests
{
    /// <summary>
    /// <c>AddHttpClient&lt;Foo&gt;(</c> 에서 <c>Foo</c> 를 집는다.
    /// 이름을 직접 주는 오버로드(<c>AddHttpClient("이름", …)</c>)는 겹치지
    /// 않으므로 보지 않는다.
    /// </summary>
    private static readonly Regex TypedClient =
        new(@"AddHttpClient<\s*([A-Za-z_][A-Za-z0-9_]*)\s*>", RegexOptions.Compiled);

    [Fact]
    public void AddHttpClient_로_등록하는_타입_이름이_겹치지_않는다()
    {
        var seen = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in SourceFiles())
        {
            foreach (Match match in TypedClient.Matches(File.ReadAllText(file)))
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
