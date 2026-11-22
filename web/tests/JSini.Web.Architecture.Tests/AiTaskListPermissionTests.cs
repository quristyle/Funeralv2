using JSini.Web.Components.Layout;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// /projmng/ai/tasks 화면에서 작업 목록은 일반 사용자의 경우 자신이 작성한 작업만 보이고,
/// 시스템관리자(SYSTEM_ADMINISTRATOR)는 모든 사용자의 작성 작업을 볼 수 있어야 한다.
/// </summary>
public sealed class AiTaskListPermissionTests
{
    [Fact]
    public void CurrentUser_IsSystemAdmin_시스템관리자_판정()
    {
        var user = new CurrentUser(null!, null!);

        // 초기 상태 (빈 역할)
        Assert.False(user.IsSystemAdmin);

        // 일반 관리자/프로젝트관리자만 있는 경우 -> 시스템관리자 아님
        user.Apply(new CurrentUser.UserInfoWire
        {
            Username = "user1",
            Roles = ["ADMINISTRATOR", "PROJMNG_ADMIN"]
        });
        Assert.False(user.IsSystemAdmin);
        Assert.True(user.IsAdmin);

        // 시스템관리자 역할이 있는 경우 -> 시스템관리자 참
        user.Apply(new CurrentUser.UserInfoWire
        {
            Username = "sysadmin",
            Roles = ["SYSTEM_ADMINISTRATOR"]
        });
        Assert.True(user.IsSystemAdmin);
        Assert.True(user.IsAdmin);

        // 대소문자 무관 검사
        user.Apply(new CurrentUser.UserInfoWire
        {
            Username = "sysadmin2",
            Roles = ["system_administrator"]
        });
        Assert.True(user.IsSystemAdmin);
    }

    [Fact]
    public void AiTaskList_SearchAsync는_시스템관리자가_아니면_자신의_작업만_조회한다()
    {
        var source = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Pages", "AiTaskList.razor.cs"));

        // CurrentUser 주입 여부 확인
        Assert.Contains("[Inject] private CurrentUser CurrentUser", source);

        // IsSystemAdmin 프로퍼티 확인
        Assert.Contains("private bool IsSystemAdmin => CurrentUser.IsSystemAdmin;", source);

        // SearchAsync 에서 mine: !IsSystemAdmin 호출 여부 확인
        Assert.Contains("mine: !IsSystemAdmin", source);
    }

    [Fact]
    public void AiTaskViewPage_LoadAsync는_시스템관리자가_아니면_자신의_작업만_조회한다()
    {
        var source = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Pages", "AiTaskViewPage.razor.cs"));

        // CurrentUser 주입 여부 확인
        Assert.Contains("[Inject] private CurrentUser CurrentUser", source);

        // IsSystemAdmin 프로퍼티 확인
        Assert.Contains("private bool IsSystemAdmin => CurrentUser.IsSystemAdmin;", source);

        // LoadAsync 에서 본인 확인 로직 확인
        Assert.Contains("!IsSystemAdmin && !string.Equals(item.CreId, CurrentUser.Username", source);
    }

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
}
