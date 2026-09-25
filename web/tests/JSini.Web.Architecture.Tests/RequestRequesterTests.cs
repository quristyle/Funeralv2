using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 요청 등록이 <b>요청자(고객)를 제대로 가리키는지</b> 지킨다.
///
/// <para>
/// [무슨 일이 있었나]
/// </para>
///
/// <para>
/// 글을 다 쓰고 「등록」을 누르면 이것만 떴다 —
/// 「An error occurred while saving the entity changes.」
/// <c>improvementrequest.customerid</c> 는 <c>customer</c> 를 가리키는
/// <b>NOT NULL 외래키</b>라, 가리킬 줄이 없으면 저장이 통째로 터진다.
/// </para>
///
/// <para>
/// 그 자리에 빠진 사람이 <b>거의 전부</b>였다. 운영 헬프데스크는 자료를
/// 가져오지 않기로 한 <b>새 DB</b> 를 쓰기 때문이다
/// (<c>web/docs/decisions-needed.md</c> D14) — 고객이 <b>0명</b>이라
/// 고를 수 있는 요청자 자체가 없었다. 화면이 보내던
/// <c>Context.HelpdeskUserId</c> 도 담당자에게는 <c>admin.id</c> 라
/// 고객 번호가 아니었다(옛 DB 에서 admin#4 → customer#4 「여우선」 —
/// 번호가 겹치면 <b>남의 이름으로</b> 들어갔다).
/// </para>
///
/// <para>
/// [지금 규칙]
/// </para>
///
/// <list type="number">
///   <item>고객으로 연결된 계정 — 언제나 자기 자신.</item>
///   <item>담당자가 「요청자」에서 고른 사람 — 대신 올리는 길.</item>
///   <item>그 밖 — <b>글을 쓴 사람 자신</b>. 가리킬 줄이 없으면 만든다.</item>
/// </list>
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 어느 쪽이든 <c>int</c> 하나라 <b>컴파일러가 잡아 줄 수 없다</b>. 그리고
/// 남의 이름으로 들어간 요청은 화면도 멀쩡하고 오류도 없다. 되돌아가는 것을
/// 막을 자리가 여기밖에 없다.
/// </para>
/// </summary>
public sealed class RequestRequesterTests
{
    /// <summary>
    /// 등록할 때 <b><c>HelpdeskUserId</c> 를 고객 번호로 쓰지 않는가</b>.
    /// </summary>
    [Fact]
    public void 등록은_담당자_번호를_고객_번호로_보내지_않는다()
    {
        var create = Between(Page(), "private async Task<int?> CreateAsync", "private async Task<int> UploadAsync");

        // 주석은 그 사고를 적어 두라고 남긴 것이라 뺀다. 검사는 **코드**만 본다.
        var code = StripComments(create);

        Assert.DoesNotContain("HelpdeskUserId", code);
        Assert.Matches(@"RequesterId is \{ \} customerId[\s\S]{0,300}?""CustomerId""", code);
    }

    /// <summary>
    /// 고객 번호를 정하는 자리가 <b>연결 종류를 가르는가</b>.
    /// </summary>
    /// <remarks>
    /// <c>Context.CustomerId</c> 는 고객으로 연결된 계정에만 값을 준다.
    /// 그것이 없으면 담당자가 고른 값을 쓴다.
    /// </remarks>
    [Fact]
    public void 요청자는_고객_연결이거나_고른_값이다()
    {
        Assert.Matches(
            @"RequesterId\s*=>[\s\S]{0,300}?Context\.CustomerId[\s\S]{0,300}?_requester",
            Page());
    }

    /// <summary>
    /// 요청자를 고르는 칸이 <b>고를 것이 있을 때만</b> 보이는가.
    /// </summary>
    /// <remarks>
    /// 고객이 0명인 DB 에서 빈 콤보를 띄우면, 사람이 할 수 있는 일이 없는데
    /// 사람의 잘못처럼 보인다. 그때는 칸을 감추고 자기 이름으로 등록한다.
    /// </remarks>
    [Fact]
    public void 요청자_칸은_고를_것이_있을_때만_보인다()
    {
        var page = Page();

        Assert.Matches(@"@if\s*\(!Context\.IsCustomer && Context\.CustomerOptions\.Count > 0\)", page);
        Assert.Matches(@"DxComboBox[\s\S]{0,400}?Context\.CustomerOptions", page);
        Assert.Matches(@"@bind-Value=""_requester""", page);

        // 목록을 안 받아 오면 콤보가 늘 비어 있다.
        Assert.Contains("LoadOrganizationsAsync", page);
    }

    /// <summary>
    /// 요청자를 안 골랐다고 <b>화면이 등록을 막지 않는가</b>.
    /// </summary>
    /// <remarks>
    /// 막던 시절에는 고객이 0명인 운영에서 <b>아무도</b> 등록할 수 없었다.
    /// 안 고르면 서버가 글을 쓴 사람 자신을 요청자로 삼는다.
    /// </remarks>
    [Fact]
    public void 요청자를_안_골라도_등록을_막지_않는다()
    {
        var submit = Between(Page(), "private async Task SubmitAsync", "private async Task FlushContentAsync");

        Assert.DoesNotContain("RequesterId is null", StripComments(submit));
    }

    /// <summary>
    /// 서버가 <b>저장하기 전에</b> 요청자를 정하는가.
    /// </summary>
    /// <remarks>
    /// 화면만 고치면 옛 화면·다른 클라이언트가 그대로 터뜨린다. 그리고 그때
    /// 나가는 것은 사람이 못 읽는 <c>DbUpdateException</c> 문장이다.
    /// </remarks>
    [Fact]
    public void 서버는_저장_전에_요청자를_정한다()
    {
        var create = Between(Endpoint(), "// 요청 생성", "//접수, 반려, 완료 등을 반영한다.");

        var resolve = create.IndexOf("requesters.ResolveAsync(", StringComparison.Ordinal);
        var add = create.IndexOf("db.Requests.Add(", StringComparison.Ordinal);

        Assert.True(resolve >= 0, "요청자를 정하는 자리가 없다. 그러면 폼 값이 그대로 외래키로 들어간다.");
        Assert.True(add > resolve, "요청자 결정이 db.Requests.Add 뒤에 있다. 그러면 여전히 DbUpdateException 이 나간다.");

        // 정하지 못한 경우에도 예외 글귀가 아니라 안내가 나가야 한다.
        Assert.Matches(@"requesterId is not \{ \} customerId[\s\S]{0,300}?ApiResponseBuilder\.Fail\(", create);
    }

    /// <summary>
    /// 가리킬 고객이 없으면 <b>만들어서라도</b> 등록되게 하는가.
    /// </summary>
    /// <remarks>
    /// 운영은 자료를 가져오지 않은 새 DB 라 고객이 0명이다. 만들어 주지 않으면
    /// 아무도 요청을 쓸 수 없다 — 신고된 증상 그대로다.
    /// </remarks>
    [Fact]
    public void 가리킬_고객이_없으면_만들어_쓴다()
    {
        var code = StripComments(Provisioner());

        // 로그인 아이디만 보고 남의 줄을 집어 오면 안 된다. 이름까지 같아야 한다.
        Assert.Matches(@"c\.LoginId == loginId && c\.UserName == userName", code);
        Assert.Contains("_db.Customers.Add(", code);

        // 회사도 NOT NULL 외래키다. 없으면 고객을 만들 수 없다.
        Assert.Contains("EnsureCompanyAsync", code);

        // 이미 있는 연결(담당자로 이어 둔 계정)을 고객으로 덮으면 권한이 사라진다.
        Assert.Matches(@"AuthUserLinks\.AnyAsync\([\s\S]{0,120}?\)\) return;", code);
    }

    /// <summary>주석을 걷어낸다. 사고를 적어 둔 글이 검사에 걸리면 안 된다.</summary>
    private static string StripComments(string code) =>
        Regex.Replace(Regex.Replace(code, @"/\*[\s\S]*?\*/", " "), @"//[^\n]*", " ");

    private static string Between(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{from}' 을(를) 찾지 못했다. 이름이 바뀌었으면 이 검사도 함께 옮겨야 한다.");

        var end = text.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(end > start, $"'{to}' 을(를) 찾지 못했다. 이름이 바뀌었으면 이 검사도 함께 옮겨야 한다.");

        return text[start..end];
    }

    private static string Page() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.HelpDesk", "Components", "Pages", "RequestNew.razor"));

    private static string Endpoint() => File.ReadAllText(Path.Combine(
        RepoRoot(), "microservices", "HelpDeskServer", "Endpoints", "RequestEndpoints.cs"));

    private static string Provisioner() => File.ReadAllText(Path.Combine(
        RepoRoot(), "microservices", "HelpDeskServer", "Services", "RequesterProvisioner.cs"));

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
