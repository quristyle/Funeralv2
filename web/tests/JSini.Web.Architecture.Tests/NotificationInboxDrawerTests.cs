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

        // 프로필 영역(프로필 사진 아래쪽)에 배치되었는지 확인
        var faceIdx = content.IndexOf("jsini-note-drawer__face", StringComparison.Ordinal);
        var actionsIdx = content.IndexOf("jsini-note-drawer__actions", StringComparison.Ordinal);
        var textIdx = content.IndexOf("jsini-note-drawer__text", StringComparison.Ordinal);

        Assert.True(faceIdx >= 0 && actionsIdx > faceIdx, "액션 아이콘은 프로필 사진보다 뒤(아래)에 위치해야 합니다.");
        Assert.True(textIdx > actionsIdx, "텍스트 영역은 프로필+액션 영역 뒤에 위치하여 가로 공간을 확보해야 합니다.");
    }

    [Fact]
    public void 알림목록_스타일은_공통_글자_크기_토큰에_준하여_정의된다()
    {
        var cssPath = Path.Combine(
            SolutionRoot(),
            "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css");

        Assert.True(File.Exists(cssPath), $"파일이 존재해야 합니다: {cssPath}");

        var css = File.ReadAllText(cssPath);

        // 알림 서랍 카드 및 텍스트 폰트 토큰 적용 확인
        Assert.Contains(".jsini-note-drawer__text .card-title", css);
        Assert.Contains(".jsini-note-drawer__text .card-text", css);
        Assert.Contains(".jsini-note-drawer__date", css);
        Assert.Contains("var(--jsini-fs-md)", css);
        Assert.Contains("var(--jsini-fs-sm)", css);
        Assert.Contains("var(--jsini-fs-2xs)", css);
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
