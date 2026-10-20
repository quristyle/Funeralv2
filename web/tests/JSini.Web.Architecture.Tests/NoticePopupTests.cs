using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class NoticePopupTests
{
    [Fact]
    public void 로그인_후_오늘_안_보기_체크가_저장하고_팝업을_닫는다()
    {
        var razor = RazorSource.Read(Path.Combine(Components(), "Layout", "NoticePopup.razor"));
        var code = RazorSource.Read(Path.Combine(Components(), "Layout", "NoticePopup.razor.cs"));

        Assert.Contains("CheckedChanged=\"@((bool dismiss) => DismissChangedAsync(dismiss))\"", razor, StringComparison.Ordinal);
        Assert.Contains("private async Task DismissChangedAsync(bool dismiss)", code, StringComparison.Ordinal);
        Assert.Contains("if (dismiss)", code, StringComparison.Ordinal);
        Assert.Contains("await SetVisibleAsync(false);", code, StringComparison.Ordinal);
        Assert.Contains("await RememberAsync();", code, StringComparison.Ordinal);
    }

    [Fact]
    public void 로그인_화면_오늘_안_보기_체크가_선택한_공지를_저장하고_팝업을_닫는다()
    {
        var js = RazorSource.Read(Path.Combine(Components(), "wwwroot", "theme.js"));

        Assert.Contains("check.addEventListener('change'", js, StringComparison.Ordinal);
        Assert.Contains("picked[live[at].getAttribute('data-notice-id')] = check.checked;", js, StringComparison.Ordinal);
        Assert.Contains("if (check.checked) close();", js, StringComparison.Ordinal);
    }

    private static string Components()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(), "src",
            "Shared", "JSini.Web.Components");
    }
}
