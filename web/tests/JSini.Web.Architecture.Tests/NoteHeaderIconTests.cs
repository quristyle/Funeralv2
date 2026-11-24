using JSini.Web.Components.Layout;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>상단 띠의 쪽지 쓰기(✉) 단추는 /admin/note/write 를 쓸 수 있는 사람에게만 선다.</summary>
public sealed class NoteHeaderIconTests
{
    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "jsini.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([SolutionRoot(), .. parts]));

    [Fact]
    public void note_아이콘의_열쇠는_쪽지_쓰기_화면이다()
    {
        var icon = HeaderIconManager.DefaultIcons.Single(x => x.IconKey == "note");

        Assert.Equal("admin.note.write", icon.RouteKey);
        Assert.Equal("/admin/note/write", icon.Path);
    }

    [Fact]
    public void note_단추는_권한표를_받은_뒤_쪽지_쓰기_권한이_있어야_보인다()
    {
        var code = Read("web", "src", "Shared", "JSini.Web.Components", "Layout", "HeaderTools.razor.cs");
        var markup = Read("web", "src", "Shared", "JSini.Web.Components", "Layout", "HeaderTools.razor");

        // 권한표를 받기 전에는 CanView 가 늘 참이다 — IsLoaded 를 먼저 봐야 권한 없는 사람에게 단추가 스치지 않는다.
        Assert.Contains("Permissions.IsLoaded", code, StringComparison.Ordinal);
        Assert.Contains("\"/admin/note/write\"", code, StringComparison.Ordinal);
        Assert.Contains("HasNotePermission && HeaderIcons.IsIconEnabled(\"note\")", code, StringComparison.Ordinal);

        // 여는 길도 같은 판정으로 막는다.
        var open = code[code.IndexOf("private void OpenNote()", StringComparison.Ordinal)..];
        Assert.Contains("if (!HasNotePermission)", open[..open.IndexOf('}')], StringComparison.Ordinal);

        Assert.Contains("@if (HasNotePermission)", markup, StringComparison.Ordinal);
    }
}
