using JSini.Web.Components.Data;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 앱알림 아이콘에 쓰는 <b>얼굴</b>의 주소 약속.
///
/// [왜 시험으로 못 박나]
///
/// 이 약속은 <b>서비스 둘에 나뉘어 적혀 있다.</b> 주소를 만드는 쪽은 알림
/// 서비스(<c>NotificationServer/Services/AvatarIconResolver.cs</c>)이고, 그 주소를
/// 받아 그림을 내주는 쪽은 셸(<c>FileDownload</c>)이다. 둘은 서로를 참조하지
/// 않으므로 한쪽에서 경로 글자를 고쳐도 <b>빌드가 통과한다</b>.
///
/// 어긋났을 때의 증상이 고약하다 — 알림은 멀쩡히 뜨고 아이콘만 조용히 빈다.
/// 아무도 오류를 보지 못하고, 브라우저 개발자 도구를 열어 서비스워커가 받아 간
/// 주소를 들여다봐야 알 수 있다.
/// </summary>
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
    /// <b>열쇠의 주인 이름 앞머리를 세 서비스가 같은 글자로 안다.</b>
    /// </summary>
    /// <remarks>
    /// 만드는 쪽(알림 서비스) · 쓸 자리를 묶는 쪽(게이트웨이) · 어느 파일이냐를
    /// 보는 쪽(FileServer)이다. 셋은 서로를 참조하지 않으므로 한쪽만 고쳐도
    /// <b>빌드가 통과하고</b>, 어긋나면 아이콘이 조용히 그림자로 돌아간다 —
    /// 게이트웨이 쪽이 어긋나면 그 열쇠가 <b>파일 읽기 밖에서도 통하는</b>
    /// 방향으로 틀린다.
    /// </remarks>
    [Fact]
    public void 열쇠_이름을_세_서비스가_같이_안다()
    {
        const string prefix = "\"push-icon:\"";

        foreach (var (service, file) in new[]
        {
            ("NotificationServer", Path.Combine("microservices", "NotificationServer", "Services", "AvatarIconToken.cs")),
            ("ApiGateway", Path.Combine("ApiGateway", "Program.cs")),
            ("FileServer", Path.Combine("microservices", "FileServer", "Endpoints", "PublicFileAccessFilter.cs")),
        })
        {
            var source = RazorSource.Read(Path.Combine(RepoRoot(), file));
            Assert.True(source.Contains(prefix), $"{service} 가 {prefix} 를 모른다 ({file}).");
        }
    }

    /// <summary>
    /// 게이트웨이가 그 열쇠를 <b>파일 읽기 경로에서만</b> 신원으로 받는다.
    /// </summary>
    /// <remarks>
    /// 이 검사가 없으면 「열쇠를 만들었다」까지만 지켜진다. 쓸 자리를 묶는 줄이
    /// 사라져도 아이콘은 잘 뜨므로 <b>아무도 알아채지 못한다</b> — 그때 그 열쇠는
    /// 한 시간짜리 로그인이 된다.
    /// </remarks>
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
