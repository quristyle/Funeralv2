using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 알림함 서랍(<c>NotificationInboxDrawer</c>)이 데스크톱 환경에서
/// 읽음 처리와 삭제 처리 아이콘을 모두 제공하는지 검사한다.
/// </summary>
public sealed class NotificationInboxDrawerTests
{
    [Fact]
    public void 알림함_서랍은_데스크톱에서_읽음과_삭제_아이콘을_제공한다()
    {
        var drawerRazorPath = Path.Combine(
            SolutionRoot(),
            "src", "Shared", "JSini.Web.Components", "Layout", "NotificationInboxDrawer.razor");

        Assert.True(File.Exists(drawerRazorPath), $"파일이 존재해야 합니다: {drawerRazorPath}");

        var content = File.ReadAllText(drawerRazorPath);
        var codeBehind = File.ReadAllText(drawerRazorPath + ".cs");

        // 데스크톱 환경(!IsPhone) 분기 확인
        Assert.Contains("!IsPhone", content);

        // 읽음 및 삭제 아이콘 존재 확인
        Assert.Contains("jsini-icon-check-circle", content);
        Assert.Contains("jsini-icon-trash", content);

        // 읽음 및 삭제 핸들러 메서드 확인
        Assert.Contains("MarkOneReadAsync", content);
        Assert.Contains("DeleteOneAsync", content);
        Assert.Contains("DeleteOneAsync", codeBehind);
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
