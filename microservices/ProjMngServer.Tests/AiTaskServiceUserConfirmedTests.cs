using Xunit;

namespace ProjMngServer.Tests;

/// <summary>이어서 지시(ContinueAsync)·수동 재시도(RetryAsync)·작업 요청(RequestAsync)은 user_confirmed 가 true 여도 false 로 리셋해야 한다.</summary>
public class AiTaskServiceUserConfirmedTests
{
    private static string FindAiTaskServiceSource()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var target = Path.Combine(current, "microservices", "ProjMngServer", "Services", "AiTaskService.cs");
            if (File.Exists(target))
            {
                return File.ReadAllText(target);
            }

            var parent = Directory.GetParent(current);
            if (parent is null) break;
            current = parent.FullName;
        }

        throw new FileNotFoundException("AiTaskService.cs 파일을 찾을 수 없습니다.");
    }

    [Fact]
    public void ContinueAsync는_user_confirmed를_false로_재설정한다()
    {
        var source = FindAiTaskServiceSource();

        var continueMethodIndex = source.IndexOf("public async Task<AiTaskEditResult> ContinueAsync(");
        Assert.True(continueMethodIndex > 0, "ContinueAsync 메서드가 존재해야 합니다.");

        var continueMethod = source.Substring(continueMethodIndex, 3000);
        Assert.Matches(@"user_confirmed\s*=\s*false", continueMethod);
    }

    [Fact]
    public void RetryAsync는_user_confirmed를_false로_재설정한다()
    {
        var source = FindAiTaskServiceSource();

        var retryMethodIndex = source.IndexOf("public async Task<AiTaskEditResult> RetryAsync(");
        Assert.True(retryMethodIndex > 0, "RetryAsync 메서드가 존재해야 합니다.");

        var retryMethod = source.Substring(retryMethodIndex, 3000);
        Assert.Matches(@"user_confirmed\s*=\s*false", retryMethod);
    }

    [Fact]
    public void RequestAsync는_user_confirmed를_false로_재설정한다()
    {
        var source = FindAiTaskServiceSource();

        var requestMethodIndex = source.IndexOf("public async Task<AiTaskEditResult> RequestAsync(");
        Assert.True(requestMethodIndex > 0, "RequestAsync 메서드가 존재해야 합니다.");

        var requestMethod = source.Substring(requestMethodIndex, 3000);
        Assert.Matches(@"user_confirmed\s*=\s*false", requestMethod);
    }
}
