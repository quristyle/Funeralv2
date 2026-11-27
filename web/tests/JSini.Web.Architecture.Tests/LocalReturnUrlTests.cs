using Xunit;
using JSini.Web.Shell.Security;

namespace JSini.Web.Architecture.Tests;

/// <summary>로그인 뒤 돌아갈 주소는 이 사이트 안이어야 한다 — 브라우저가 역슬래시·탭을 / 로 고쳐 읽어 밖으로 나가는 꼴을 막는다.</summary>
public class LocalReturnUrlTests
{
    [Theory]
    [InlineData("/helpdesk/request/manage", "/helpdesk/request/manage")]
    [InlineData("/admin/system/account?q=1#top", "/admin/system/account?q=1#top")]
    [InlineData("/", "/")]
    public void 사이트_안의_경로는_그대로_둔다(string input, string expected) =>
        Assert.Equal(expected, LocalReturnUrl.Sanitize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/\tevil.example")]
    [InlineData("/\t/evil.example")]
    [InlineData("/a\\b")]
    [InlineData("/ /evil.example")]
    [InlineData("javascript:alert(1)")]
    public void 밖으로_나갈_수_있는_값은_첫_화면으로_돌린다(string? input) =>
        Assert.Equal("/", LocalReturnUrl.Sanitize(input));
}
