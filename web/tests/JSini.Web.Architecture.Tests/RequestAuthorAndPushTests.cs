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

    /// <summary>
    /// 요청자는 <b>이름과 계정을 한 덩이</b>로 보인다 — 「이순열(quristyle)」.
    /// </summary>
    /// <remarks>
    /// 전에는 「요청자」와 「작성자(계정)」이 <b>따로 선 두 줄</b>이었다. 둘은
    /// 거의 언제나 같은 사람이라 줄만 하나 더 먹으면서, 읽는 사람이 위아래를
    /// 짝지어 봐야 이름과 계정이 이어졌다. 다시 갈라 놓지 않도록 못박는다.
    /// </remarks>
    [Fact]
    public void 요청_상세는_작성계정과_요청자_이름을_함께_보인다()
    {
        var detail = DetailPage();

        Assert.Contains("<dt>요청자</dt><dd>@RequesterName()</dd>", detail);
        Assert.DoesNotContain("<dt>작성자(계정)</dt>", detail);

        // 이름은 요청자에서, 계정은 **실제로 글을 쓴 포털 계정**(`createdBy`)에서.
        Assert.Contains("NestedValue(\"customer\", \"userName\")", detail);
        Assert.Contains("Value(\"createdBy\")", detail);
        Assert.Contains("$\"{name}({loginId})\"", detail);
    }

    /// <summary>
    /// 접수자도 같은 모양이고, 고객사는 <b>서버가 푼 이름</b>을 쓴다.
    /// </summary>
    /// <remarks>
    /// 고객사 자리는 오래 <c>-</c> 였다. 헬프데스크에는 회사 표가 없어 응답에
    /// 회사 <b>아이디</b>만 있었기 때문이다. 이름을 푸는 쪽이 서버라는 것과,
    /// 못 풀었을 때 아이디로 물러선다는 것을 함께 못박는다.
    /// </remarks>
    [Fact]
    public void 요청_상세는_접수자도_계정과_함께_보이고_고객사는_서버가_푼_이름을_쓴다()
    {
        var detail = DetailPage();

        Assert.Contains("<dt>접수자</dt><dd>@AssigneeName()</dd>", detail);
        Assert.Contains("NestedValue(\"admin\", \"loginId\")", detail);

        Assert.Contains("<dt>고객사</dt><dd>@CompanyName()</dd>", detail);
        Assert.Contains("Value(\"companyName\")", detail);
        Assert.Contains("NestedValue(\"customer\", \"companyId\")", detail);

        // 제목은 바로 위 머리글이 적는다. 여기 다시 두면 한 화면에 두 번 나온다.
        Assert.DoesNotContain("<dt>제목</dt>", detail);
    }

    /// <summary>
    /// 상세 엔드포인트가 <b>고객사 이름을 풀어</b> 내려준다.
    /// </summary>
    /// <remarks>
    /// 회사의 정본은 포털이고 헬프데스크는 아이디만 들고 있다. 이름을 푸는
    /// 일이 여기서 빠지면 화면의 「고객사」가 조용히 아이디로 돌아간다.
    /// </remarks>
    [Fact]
    public void 요청_상세_엔드포인트는_고객사_이름을_풀어_담는다()
    {
        var code = StripComments(Between(
            Endpoint(), "// 요청 상세", "// 특정 요청에 대한 덧글 목록 조회"));

        Assert.Contains("IPortalCompanyDirectory companies", code);
        Assert.Contains("companies.GetNameAsync(request.Customer?.CompanyId", code);

        // 못 풀면 아이디를 그대로 둔다 — 회사를 통째로 감추지 않는다.
        Assert.Contains("?? request.Customer?.CompanyId", code);
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
