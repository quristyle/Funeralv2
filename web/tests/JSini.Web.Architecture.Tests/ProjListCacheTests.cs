using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>프로젝트 고르개(projlist)는 「내가 참여한 프로젝트」를 사람별로 10분 들고 있다(CommonCodes).
/// 참여를 바꾸는 화면이 저장 뒤에 그 통을 버리지 않으면 방금 넣은 프로젝트가 WBS·일정표의 고르개에 그동안 안 나온다 —
/// 서버와 DB 는 멀쩡해서 「참여했는데 목록에 없다」로만 보인다.</summary>
public sealed class ProjListCacheTests
{
    [Fact]
    public void 참여를_저장하는_화면은_고르개_통을_버린다()
    {
        var offenders = RazorFiles()
            .Where(f => RazorSource.Read(f).Contains("SetAssignmentsAsync(", StringComparison.Ordinal))
            .Where(f => !RazorSource.Read(f).Contains("Codes.Clear()", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "참여를 저장하고 Codes.Clear() 를 부르지 않습니다 — projlist 고르개가 10분 동안 옛 목록을 보입니다.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>검사할 .razor 파일들. 실행 디렉터리가 CI 와 로컬에서 달라 저장소 뿌리를 거슬러 올라가 찾는다.</summary>
    private static IEnumerable<string> RazorFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return Directory
            .EnumerateFiles(Path.Combine(dir!.FullName, "src"), "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }
}
