using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 메일 보내기(/admin/mail/send)의 보낸 메일 확인 팝업이 다크모드에서 올바르게 표시되는지 검증한다.
/// 흰 배경에 흰 글씨가 되어 본문이 가려지는 문제를 방지한다.
/// </summary>
public sealed class MailSendDarkModeTests
{
    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("솔루션 루트를 찾을 수 없습니다.");
    }

    [Fact]
    public void 보낸메일_본문과_요약박스_다크모드_스타일이_admin_css에_선언되어_있다()
    {
        var cssPath = Path.Combine(SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "wwwroot", "admin.css");
        var css = File.ReadAllText(cssPath);

        Assert.Contains(":root[data-bs-theme='dark'] .ad-mailsend__body", css);
        Assert.Contains(":root[data-theme='dark'] .ad-mailsend__body", css);

        // 흰색 계열 배경 override 검증
        Assert.Contains(".ad-mailsend__body [style*=\"background:#f1f3f5\"]", css);
        Assert.Contains(".ad-mailsend__body [style*=\"background:#ffffff\"]", css);
        Assert.Contains(".ad-mailsend__body [style*=\"background:white\"]", css);

        // 요약 박스 클래스 스타일 검증
        Assert.Contains(".ad-mailsend__summary-box", css);
    }

    [Fact]
    public void AI요약_본문_삽입시_요약박스_클래스와_기본_글자색이_지정되어_있다()
    {
        var csPath = Path.Combine(SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "MailSendPage.razor.cs");
        var cs = File.ReadAllText(csPath);

        Assert.Contains("class=\\\"ad-mailsend__summary-box\\\"", cs);
        Assert.Contains("color:#212529", cs);
    }
}
