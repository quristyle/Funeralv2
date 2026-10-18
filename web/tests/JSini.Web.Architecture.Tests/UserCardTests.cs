using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 얼굴(<c>UserAvatar</c>)을 누르면 뜨는 사람 카드 — 레이아웃의 <c>UserCardHost</c> 한 벌이 그린다.
/// </summary>
public sealed class UserCardTests
{
    [Fact]
    public void 카드는_레이아웃에_한_벌만_두고_휴대폰_여부를_받는다()
    {
        var layout = Read("Shared", "JSini.Web.Components", "Layout", "MainLayout.razor");

        Assert.Single(Regex.Matches(layout, "<UserCardHost "));
        Assert.Contains("<UserCardHost IsPhone=\"@_isPhone\" />", layout);

        // 화면이 카드를 직접 적으면 격자 칸 안에 갇혀 말풍선이 잘린다.
        foreach (var file in Directory.EnumerateFiles(Root(), "*.razor", SearchOption.AllDirectories))
        {
            if (file.EndsWith("MainLayout.razor", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.DoesNotContain("<UserCardHost", File.ReadAllText(file));
        }
    }

    [Fact]
    public void 로그인_아이디가_있는_얼굴만_눌린다()
    {
        var cs = Read("Shared", "JSini.Web.Components", "Data", "UserAvatar.razor.cs");

        Assert.Contains("[Parameter] public bool Card { get; set; } = true;", cs);
        Assert.Contains("Card && !string.IsNullOrWhiteSpace(LoginId)", cs);
        Assert.Contains("Cards.Open(LoginId!, _anchorId, Who, PhotoUrl)", cs);

        // 줄을 누르는 목록 안에서 카드가 함께 열리지 않도록 누름을 거기서 멈춘다.
        var razor = Read("Shared", "JSini.Web.Components", "Data", "UserAvatar.razor");
        Assert.Contains("@onclick:stopPropagation=\"@Clickable\"", razor);
    }

    [Theory]
    [InlineData("Apps/JSini.Web.ProjMng/Components/Pages/AiTaskList.razor")]
    [InlineData("Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor")]
    [InlineData("Apps/JSini.Web.Admin/Components/Pages/UserRoleMap.razor")]
    public void 단추_안의_얼굴은_카드를_끈다(string path)
    {
        var razor = Read(path.Split('/'));

        foreach (Match m in Regex.Matches(razor, "<UserAvatar [^>]*>"))
        {
            Assert.Contains("Card=\"false\"", m.Value);
        }
    }

    [Fact]
    public void 휴대폰은_가운데_창에_닫기_단추_책상은_말풍선이고_바깥을_누르면_닫힌다()
    {
        var host = Read("Shared", "JSini.Web.Components", "Data", "UserCardHost.razor");

        Assert.Contains("jsini-ucard--center", host);
        Assert.Contains("jsini-ucard--bubble", host);
        Assert.Contains("class=\"jsini-ucard__close\"", host);
        Assert.Matches(new Regex("jsini-ucard-scrim[^>]*\\s+@onclick=\"@Cards.Close\"", RegexOptions.Singleline), host);

        foreach (var label in new[] { "쪽지쓰기", "이메일쓰기", "전화하기" })
        {
            Assert.Contains(label, host);
        }

        var css = Read("Shared", "JSini.Web.Components", "wwwroot", "app.css");
        Assert.Contains(".jsini-ucard--bubble::before", css);
        Assert.Contains(".jsini-icon-telephone", css);
    }

    [Fact]
    public void 메일_보내기_화면은_받는_사람을_주소로_받는다()
    {
        var cs = Read("Apps", "JSini.Web.Admin", "Components", "Pages", "MailSendPage.razor.cs");

        Assert.Contains("[SupplyParameterFromQuery(Name = \"to\")]", cs);

        var host = Read("Shared", "JSini.Web.Components", "Data", "UserCardHost.razor.cs");
        Assert.Contains("private const string MailSendPath = \"/admin/mail/send\";", host);
        Assert.Contains(
            "@page \"/admin/mail/send\"",
            Read("Apps", "JSini.Web.Admin", "Components", "Pages", "MailSendPage.razor"));
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "web", "src");
    }
}
