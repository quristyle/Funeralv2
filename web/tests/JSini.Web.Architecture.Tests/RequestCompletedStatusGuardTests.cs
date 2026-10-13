using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 완료 또는 종료 상태의 요청에 대한 댓글 작성, 수정, 삭제 방어 규칙 검증.
/// </summary>
public sealed class RequestCompletedStatusGuardTests
{
    [Fact]
    public void 요청_상세_화면은_완료_또는_종료_상태일때_댓글_작성을_차단하고_안내를_표시한다()
    {
        var detail = DetailPage();

        // 완료/종료 판정 프로퍼티 존재
        Assert.Contains("bool IsFinished => Status is \"Completed\" or \"UserCompleted\"", detail);

        // 댓글 작성 가능 여부에 IsFinished 반영
        Assert.Contains("CanWrite => !IsFinished && Context.IsLinked", detail);

        // 완료/종료 시 안내 문구 표시
        Assert.Contains("완료되거나 종료된 요청에는 댓글을 남길 수 없습니다.", detail);

        // AddAsync 메서드 내 방어 로직
        Assert.Contains("if (IsFinished)", detail);
    }

    [Fact]
    public void 요청_상세_화면은_완료_또는_종료_상태일때_댓글_삭제와_요청_삭제를_차단한다()
    {
        var detail = DetailPage();

        // 댓글 쓰레드에 댓글 삭제 권한 전달
        Assert.Contains("CanDeleteComment=\"@(!IsFinished)\"", detail);

        // 요청 삭제 권한 판정
        Assert.Contains("CanDeleteRequest => Context.IsSystemAdmin && !IsFinished", detail);

        // 요청 삭제 버튼 렌더링 조건
        Assert.Contains("@if (CanDeleteRequest)", detail);

        // DeleteAsync 및 DeleteCommentAsync 메서드 내 방어 로직
        Assert.Contains("if (!CanDeleteRequest)", detail);
        Assert.Contains("완료되거나 종료된 요청의 댓글은 삭제할 수 없습니다.", detail);
    }

    [Fact]
    public void 백엔드_API는_완료_또는_종료_상태일때_댓글_등록_수정_삭제와_요청_수정_삭제를_방어한다()
    {
        var commentEndpoints = CommentEndpointSource();
        var requestEndpoints = RequestEndpointSource();

        // 댓글 등록(POST), 수정(PUT), 삭제(DELETE) 방어
        Assert.Contains("request.Status == ImprovementStatus.Completed || request.Status == ImprovementStatus.UserCompleted", commentEndpoints);
        Assert.Contains("완료되거나 종료된 요청에는 댓글을 추가 작성할 수 없습니다.", commentEndpoints);
        Assert.Contains("완료되거나 종료된 요청의 댓글은 수정할 수 없습니다.", commentEndpoints);
        Assert.Contains("완료되거나 종료된 요청의 댓글은 삭제할 수 없습니다.", commentEndpoints);

        // 요청 수정(PUT), 삭제(DELETE) 방어
        Assert.Contains("req.Status == ImprovementStatus.Completed || req.Status == ImprovementStatus.UserCompleted", requestEndpoints);
        Assert.Contains("완료되거나 종료된 요청은 수정할 수 없습니다.", requestEndpoints);
        Assert.Contains("완료되거나 종료된 요청은 삭제할 수 없습니다.", requestEndpoints);
    }

    private static string DetailPage() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.HelpDesk", "Components", "Pages", "RequestDetail.razor"));

    private static string CommentEndpointSource() => File.ReadAllText(Path.Combine(
        RepoRoot(), "microservices", "HelpDeskServer", "Endpoints", "CommentEndpoints.cs"));

    private static string RequestEndpointSource() => File.ReadAllText(Path.Combine(
        RepoRoot(), "microservices", "HelpDeskServer", "Endpoints", "RequestEndpoints.cs"));

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
