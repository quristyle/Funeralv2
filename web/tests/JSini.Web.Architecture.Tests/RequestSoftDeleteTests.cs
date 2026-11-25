using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 요청글 삭제는 지움 표시만 세우고, 지운 글을 보고 · 되살리고 · 완전삭제하는 것은 시스템관리자뿐이다.
/// 어느 한 자리가 빠져도 오류 없이 「지운 글이 남에게 보인다」나 「파일이 주인 없이 남는다」로만 드러난다.
/// </summary>
public sealed class RequestSoftDeleteTests
{
    [Fact]
    public void 지운_요청글과_그_댓글은_전역_걸름으로_가린다()
    {
        var db = Read("microservices", "HelpDeskServer", "Data", "AppDbContext.cs");

        Assert.Contains("modelBuilder.Entity<ImprovementRequest>().HasQueryFilter(r => !r.IsDel);", db);
        Assert.Contains("modelBuilder.Entity<ImprovementComment>().HasQueryFilter(c => !c.Request!.IsDel);", db);
    }

    [Fact]
    public void 삭제는_줄을_지우지_않고_완전삭제와_복원은_시스템관리자만_한다()
    {
        var requests = Read("microservices", "HelpDeskServer", "Endpoints", "RequestEndpoints.cs");
        var comments = Read("microservices", "HelpDeskServer", "Endpoints", "CommentEndpoints.cs");
        var purger = Read("microservices", "HelpDeskServer", "Services", "RequestPurger.cs");

        Assert.DoesNotContain("db.Requests.Remove(", requests);
        Assert.Contains("req.IsDel = true;", requests);
        Assert.Contains("group.MapPut(\"/{id}/restore\"", requests);
        Assert.Contains("group.MapDelete(\"/{id}/purge\"", requests);
        Assert.Contains("group.MapDelete(\"/{id}/purge\"", comments);
        Assert.DoesNotContain("DeleteImageFileDir", comments);

        // 완전삭제는 시스템관리자 확인 · 지움 표시 확인을 거치고, 파일(FileServer · 로컬)을 지운 뒤에 줄을 지운다.
        Assert.Contains("RequireSystemAdmin()", purger);
        Assert.Contains("삭제 처리된 요청만 완전삭제할 수 있습니다.", purger);
        Assert.Contains("삭제 처리된 댓글만 완전삭제할 수 있습니다.", purger);
        Assert.True(purger.IndexOf("RemoveFilesAsync(attachments", StringComparison.Ordinal)
                    < purger.IndexOf("ExecuteDeleteAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void 화면은_지운_글에_복원과_완전삭제만_내놓는다()
    {
        var detail = Page("RequestDetail");
        var manage = Page("RequestManage");

        Assert.Contains("requests/{Id}/restore", detail);
        Assert.Contains("requests/{Id}/purge", detail);
        Assert.Contains("comments/{commentId}/purge", detail);
        Assert.Contains("CanPurge=\"@Context.IsSystemAdmin\"", detail);
        Assert.Contains("query[\"trash\"] = \"true\";", manage);
        Assert.Contains("@if (Context.IsSystemAdmin)", manage);
    }

    private static string Page(string name) => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.HelpDesk", "Components", "Pages", name + ".razor"));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([RepoRoot(), .. parts]));

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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(SolutionRoot());
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "microservices")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
