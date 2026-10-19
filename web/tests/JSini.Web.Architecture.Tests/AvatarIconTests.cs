using JSini.Web.Components.Data;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>앱알림 아이콘에 쓰는 얼굴의 주소 약속. 주소를 만드는 알림 서비스와 받아 내주는 셸은 서로를 참조하지 않으므로,
/// 한쪽 글자가 틀려도 빌드는 통과하고 아이콘만 조용히 빈다. 그래서 글자로 못 박는다.</summary>
public class AvatarIconTests
{
    /// <summary>
    /// 사람 형상 그림자가 <b>실제로 거기 있어야 한다.</b>
    /// 없으면 사진 없는 사람의 알림이 전부 아이콘 없이 뜬다.
    /// </summary>
    [Fact]
    public void 그림자_아이콘이_셸에_들어_있다()
    {
        var path = Path.Combine(
            WebRoot(), "src", "Shell", "JSini.Web.Shell", "wwwroot",
            FileDownload.FallbackAvatarPath.TrimStart('/'));

        Assert.True(File.Exists(path), $"{FileDownload.FallbackAvatarPath} 가 셸 wwwroot 에 없다.");
    }

    /// <summary>
    /// 알림 서비스가 만들어 보내는 주소와 셸이 여는 경로가 같아야 한다.
    /// </summary>
    [Fact]
    public void 알림_서비스가_같은_경로를_만든다()
    {
        var source = RazorSource.Read(Path.Combine(
            RepoRoot(), "microservices", "NotificationServer", "Services", "AvatarIconResolver.cs"));

        // 사진이 있는 사람 — /files/avatar/{파일아이디}?t={열쇠}
        Assert.Contains($"\"/files/avatar/{{0}}?{FileDownload.AvatarTokenKey}={{1}}\"", source);
        Assert.Equal(
            "/files/avatar/11111111-1111-1111-1111-111111111111?t=abc",
            FileDownload.AvatarUrlFor("11111111-1111-1111-1111-111111111111", "abc"));

        // 열쇠를 못 만들었을 때의 주소도 셸이 그대로 받는다.
        Assert.Contains("\"/files/avatar/{0}\"", source);
        Assert.Equal(
            "/files/avatar/11111111-1111-1111-1111-111111111111",
            FileDownload.AvatarUrlFor("11111111-1111-1111-1111-111111111111"));

        // 사진이 없는 사람 — 셸이 들고 있는 그림자
        Assert.Contains($"\"{FileDownload.FallbackAvatarPath}\"", source);
    }

    /// <summary>
    /// 새 기기 구독 알림은 구독자의 얼굴을 아이콘으로 쓴다.
    /// </summary>
    [Fact]
    public void 새_기기_구독_알림은_구독자의_프로필_사진을_쓴다()
    {
        var source = RazorSource.Read(Path.Combine(
            RepoRoot(), "microservices", "NotificationServer", "Endpoints", "NotificationEndpoints.cs"));
        var title = source.IndexOf("Title = \"새로운 알림 구독\"", StringComparison.Ordinal);

        Assert.True(title >= 0, "새 기기 구독 알림의 메시지 정의를 찾지 못했다.");
        var category = source.IndexOf("Category = PushCategories.Subscription", title, StringComparison.Ordinal);
        Assert.True(category > title, "새 기기 구독 알림의 구분을 찾지 못했다.");
        Assert.Contains("IconOwnerKey = user.UserId", source[title..category]);
    }

    /// <summary>열쇠의 주인 이름 앞머리를 세 서비스가 같은 글자로 안다 — 만드는 쪽, 묶는 쪽(게이트웨이), 보는 쪽(FileServer). 어긋나면 아이콘이 조용히 그림자로 돌아간다.</summary>
    [Fact]
    public void 열쇠_이름을_서비스들이_같이_안다()
    {
        const string prefix = "\"push-icon:\"";

        foreach (var (service, file) in new[]
        {
            ("NotificationServer", Path.Combine("microservices", "NotificationServer", "Services", "AvatarIconToken.cs")),
            ("AuthServer", Path.Combine("microservices", "AuthServer", "Services", "MailPhotoToken.cs")),
            ("ApiGateway", Path.Combine("ApiGateway", "Program.cs")),
            ("FileServer", Path.Combine("microservices", "FileServer", "Endpoints", "PublicFileAccessFilter.cs")),
        })
        {
            var source = RazorSource.Read(Path.Combine(RepoRoot(), file));
            Assert.True(source.Contains(prefix), $"{service} 가 {prefix} 를 모른다 ({file}).");
        }
    }

    /// <summary>게이트웨이가 그 열쇠를 파일 읽기 경로에서만 신원으로 받는다. 이 검사가 없으면 쓸 자리를 묶는 줄이 사라져도 아이콘은 잘 떠서 아무도 모르고, 열쇠가 한 시간짜리 로그인이 된다.</summary>
    [Fact]
    public void 열쇠는_파일_읽기_경로에서만_통한다()
    {
        var source = RazorSource.Read(Path.Combine(RepoRoot(), "ApiGateway", "Program.cs"));

        Assert.Contains("OnTokenValidated", source);
        Assert.Contains("iconTokenSubjectPrefix", source);
        Assert.Contains("ctx.Fail(", source);
    }

    /// <summary><c>web/</c> 폴더.</summary>
    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>저장소 뿌리. <c>microservices/</c> 가 있는 곳이다.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(WebRoot());

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "microservices")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
