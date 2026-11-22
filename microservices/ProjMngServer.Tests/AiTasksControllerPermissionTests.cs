using Xunit;

namespace ProjMngServer.Tests;

/// <summary>
/// AiTasksController.ListAsync 는 시스템관리자(SYSTEM_ADMINISTRATOR)가 아닌 포털 호출 시
/// 자신이 작성한 작업(onlyMine)만 조회하도록 강제해야 한다.
/// </summary>
public class AiTasksControllerPermissionTests
{
    private static string FindAiTasksControllerSource()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var target = Path.Combine(current, "microservices", "ProjMngServer", "Controllers", "AiTasksController.cs");
            if (File.Exists(target))
            {
                return File.ReadAllText(target);
            }

            var parent = Directory.GetParent(current);
            if (parent is null) break;
            current = parent.FullName;
        }

        throw new FileNotFoundException("AiTasksController.cs 파일을 찾을 수 없습니다.");
    }

    [Fact]
    public void ListAsync는_시스템관리자가_아닌_경우_자신이_작성한_작업만_조회한다()
    {
        var source = FindAiTasksControllerSource();

        Assert.Contains("SYSTEM_ADMINISTRATOR", source);
        Assert.Contains("isSystemAdmin", source);
        Assert.Contains("onlyMine", source);
        Assert.Contains("creId: onlyMine ? UserId : null", source);
    }

    [Fact]
    public void GetAsync는_시스템관리자가_아닌_경우_자신이_작성한_작업만_조회한다()
    {
        var source = FindAiTasksControllerSource();

        Assert.Contains("GetAsync(long taskKey)", source);
        Assert.Contains("service.GetAsync(taskKey, creId: onlyMine ? UserId : null)", source);
    }
}
