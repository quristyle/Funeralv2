using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class RequestAuthorAndPushTests
{
    [Fact]
    public void 요청_등록_푸시는_RabbitMQ_연결과_분리되어_관리자에게_간다()
    {
        var create = Between(Endpoint(), "// 요청 생성", "//접수, 반려, 완료 등을 반영한다.");
        var code = StripComments(create);
        var push = code.IndexOf("/notifications/push", StringComparison.Ordinal);
        var rabbitGate = code.IndexOf("if (provider.IsConnected)", StringComparison.Ordinal);

        Assert.True(push >= 0, "요청 등록 뒤 관리자 푸시 발송이 없다.");
        Assert.True(rabbitGate > push, "푸시 발송이 RabbitMQ 연결 조건 안에 들어 있다.");
        Assert.Contains("roles = new[] { \"SYSTEM_ADMINISTRATOR\" }", code);
        Assert.Contains("X-User-Id\", \"HELPDESK_REQUEST\"", code);
        Assert.Contains("/helpdesk/request/detail/{request.Id}", code);
    }

    [Fact]
    public void 요청_상세는_작성계정과_요청자_이름을_함께_보인다()
    {
        var detail = DetailPage();

        Assert.Contains("<dt>작성자(계정)</dt><dd>@Text(\"createdBy\")</dd>", detail);
        Assert.Contains("<dt>요청자</dt><dd>@RequesterName()</dd>", detail);
        Assert.Contains("NestedValue(\"customer\", \"userName\")", detail);
    }

    private static string StripComments(string code)
    {
        var withoutBlocks = System.Text.RegularExpressions.Regex.Replace(code, @"/\*[\s\S]*?\*/", " ");
        return System.Text.RegularExpressions.Regex.Replace(withoutBlocks, @"//[^\n]*", " ");
    }

    private static string Between(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{from}' 을(를) 찾지 못했다.");

        var end = text.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(end > start, $"'{to}' 을(를) 찾지 못했다.");

        return text[start..end];
    }

    private static string DetailPage() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.HelpDesk", "Components", "Pages", "RequestDetail.razor"));

    private static string Endpoint() => RazorSource.Read(Path.Combine(
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
