using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 차단 접근기록 화면(/admin/status/blocked-access)의 항목 카드 선택 및
/// 차단된 사용자와 시각 등 상세 정보 표시 연동을 검증한다.
/// </summary>
public sealed class BlockedAccessDetailTests
{
    private const string RazorPath =
        "src/Apps/JSini.Web.Admin/Components/Pages/BlockedAccessList.razor";

    private const string CodeBehindPath =
        "src/Apps/JSini.Web.Admin/Components/Pages/BlockedAccessList.razor.cs";

    [Fact]
    public void 차단접근기록_마크업에_카드선택_핸들러와_상세팝업이_있다()
    {
        var path = Path.Combine(SolutionRoot(), RazorPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"파일 없음: {path}");

        var html = RazorSource.Read(path);

        // 카드 및 행 선택/클릭 핸들러 연동 확인
        Assert.Contains("SelectedItemChanged=\"@OnScreenPickedAsync\"", html);
        Assert.Contains("OnRowClick=\"@OpenScreenDetailAsync\"", html);
        Assert.Contains("OnRowDoubleClick=\"@OpenScreenDetailAsync\"", html);

        // 컨텍스트 메뉴 항목
        Assert.Contains("차단 상세 보기", html);

        // 상세 팝업 및 차단된 사용자와 시각 컬럼/라벨 포함 확인
        Assert.Contains("<CommPopup", html);
        Assert.Contains("@bind-Visible=\"_detailVisible\"", html);
        Assert.Contains("차단 시각", html);
        Assert.Contains("차단된 사용자", html);
        Assert.Contains("시도 주소", html);
        Assert.Contains("ad-blocked__facts", html);
    }

    [Fact]
    public void 차단접근기록_코드비하인드에_카드선택_및_상세조회_메서드가_있다()
    {
        var path = Path.Combine(SolutionRoot(), CodeBehindPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"파일 없음: {path}");

        var code = File.ReadAllText(path);

        Assert.Contains("OnScreenPickedAsync", code);
        Assert.Contains("OpenScreenDetailAsync", code);
        Assert.Contains("OnUserPickedAsync", code);
        Assert.Contains("OpenUserDetailAsync", code);
        Assert.Contains("OnLogPickedAsync", code);
        Assert.Contains("OpenLogDetail", code);
        Assert.Contains("_detailVisible", code);
        Assert.Contains("_detailLogs", code);
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
