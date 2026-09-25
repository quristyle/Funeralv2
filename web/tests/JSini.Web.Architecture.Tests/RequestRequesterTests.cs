using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 요청 등록 화면이 <b>요청자(고객)를 제대로 가리키는지</b> 지킨다.
///
/// <para>
/// [무슨 일이 있었나]
/// </para>
///
/// <para>
/// 화면은 <c>Context.HelpdeskUserId</c> 를 그대로 <c>CustomerId</c> 로 보냈다.
/// 그 값은 <b>고객으로 연결된 계정에만</b> 고객 번호이고, 담당자로 연결된
/// 계정에는 <c>admin.id</c> 다. 그래서 둘로 갈려 터졌다.
/// </para>
///
/// <list type="bullet">
///   <item>연결이 없는 계정 — 아무것도 안 보내 서버가 <c>0</c> 으로 읽었다.
///     <c>improvementrequest.customerid</c> 는 <c>customer</c> 를 가리키는
///     NOT NULL 외래키라 저장이 터졌고, 화면에는 DB 오류 문장이 그대로 떴다
///     (「An error occurred while saving the entity changes.」).
///     포털 계정 46 개 중 연결된 것은 하나뿐이라 <b>거의 모두</b>가 여기였다.</item>
///   <item>담당자로 연결된 계정 — 자기 <c>admin.id</c> 를 보냈다. 같은 번호의
///     고객이 있으면 저장은 되는데 <b>남의 이름으로 요청이 들어갔다</b>
///     (운영에서 admin#4 → customer#4 「여우선」). 오류보다 나쁜 쪽이다.</item>
/// </list>
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 둘 다 <b>컴파일러가 잡아 줄 수 없다</b> — 어느 쪽이든 <c>int</c> 하나다.
/// 그리고 뒤쪽(남의 이름으로 들어간 요청)은 화면도 멀쩡하고 오류도 없다.
/// 되돌아가는 것을 막을 자리가 여기밖에 없다.
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
    /// 담당자에게 <b>요청자를 고르는 칸</b>이 있는가.
    /// </summary>
    /// <remarks>
    /// 이것이 없으면 담당자는 요청자를 정할 길이 없고, 화면은 다시 등록할 수
    /// 없는 상태로 돌아간다.
    /// </remarks>
    [Fact]
    public void 담당자에게는_요청자_고르는_칸이_있다()
    {
        var page = Page();

        Assert.Matches(@"@if\s*\(!Context\.IsCustomer\)", page);
        Assert.Matches(@"DxComboBox[\s\S]{0,400}?Context\.CustomerOptions", page);
        Assert.Matches(@"@bind-Value=""_requester""", page);

        // 목록을 안 받아 오면 콤보가 늘 비어 있다.
        Assert.Contains("LoadOrganizationsAsync", page);
    }

    /// <summary>
    /// 요청자를 못 정했으면 <b>보내기 전에</b> 막는가.
    /// </summary>
    /// <remarks>
    /// 서버도 다시 보지만, 여기서 걸러야 글을 다 쓰고 누른 사람이 무엇을
    /// 골라야 하는지 그 자리에서 안다.
    /// </remarks>
    [Fact]
    public void 요청자가_없으면_등록을_시작하지_않는다()
    {
        var submit = Between(Page(), "private async Task SubmitAsync", "private async Task FlushContentAsync");

        Assert.Matches(@"RequesterId is null[\s\S]{0,200}?return;", submit);

        // 막는 자리가 실제로 **보내기 앞**이어야 한다.
        var guard = submit.IndexOf("RequesterId is null", StringComparison.Ordinal);
        var send = submit.IndexOf("CreateAsync(", StringComparison.Ordinal);

        Assert.True(guard >= 0 && send > guard,
            "요청자 확인이 CreateAsync 뒤에 있다. 그러면 서버까지 갔다 와야 안다.");
    }

    /// <summary>
    /// 서버도 <b>저장하기 전에</b> 요청자를 확인하는가.
    /// </summary>
    /// <remarks>
    /// 화면만 막으면 옛 화면·다른 클라이언트가 그대로 터뜨린다. 그리고 그때
    /// 나가는 것은 사람이 못 읽는 <c>DbUpdateException</c> 문장이다.
    /// </remarks>
    [Fact]
    public void 서버는_저장_전에_요청자를_확인한다()
    {
        var create = Between(Endpoint(), "// 요청 생성", "//접수, 반려, 완료 등을 반영한다.");

        Assert.Matches(
            @"db\.Customers\.AnyAsync\(c => c\.Id == customerId\)[\s\S]{0,300}?ApiResponseBuilder\.Fail\(",
            create);

        var check = create.IndexOf("db.Customers.AnyAsync", StringComparison.Ordinal);
        var add = create.IndexOf("db.Requests.Add(", StringComparison.Ordinal);

        Assert.True(check >= 0 && add > check,
            "요청자 확인이 db.Requests.Add 뒤에 있다. 그러면 여전히 DbUpdateException 이 나간다.");
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
