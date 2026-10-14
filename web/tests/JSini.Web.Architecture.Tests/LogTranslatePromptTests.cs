using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 로그 번역 시 대괄호([]) 및 밑줄(_) 단어, 모든 영문자 번역 규칙 검증.
/// </summary>
public sealed class LogTranslatePromptTests
{
    private static readonly Regex CommonBracketTagsRegex = new(
        @"\[([A-Za-z_]+)\]", RegexOptions.Compiled);

    [Fact]
    public void 번역_프롬프트에_대괄호_밑줄_모든영문자_번역_규칙이_포함되어_있다()
    {
        var root = WorkspaceRoot();
        var llmServicePath = Path.Combine(root, "microservices", "AIAgentServer", "Services", "LLMService.cs");

        Assert.True(File.Exists(llmServicePath), $"LLMService.cs 파일이 존재해야 합니다: {llmServicePath}");

        var code = File.ReadAllText(llmServicePath);

        // 과거 식별자/오류코드를 원문 그대로 두게 하여 [] 나 _ 단어 번역을 막았던 구 규칙이 없어야 함
        Assert.DoesNotContain("파일 경로·명령·식별자·URL·숫자·오류 코드는 원문 그대로 둡니다", code);

        // 대괄호 [] 로 감싸인 단어 번역 지침
        Assert.Contains("대괄호 [] 로 감싸여진 단어", code);

        // 밑줄 _ 로 연결된 단어 분할 번역 지침
        Assert.Contains("단어가 _ (밑줄)로 연결되어 있는 단어", code);

        // 가능한 모든 영문자 번역 지침
        Assert.Contains("모든 영문자", code);
    }

    [Theory]
    [InlineData("[INF] Service started", "[정보] Service started")]
    [InlineData("[INFO] Starting task", "[정보] Starting task")]
    [InlineData("[WARN] High memory", "[경고] High memory")]
    [InlineData("[WARNING] Disk space low", "[경고] Disk space low")]
    [InlineData("[ERR] Fatal failure", "[오류] Fatal failure")]
    [InlineData("[ERROR] Unknown error", "[오류] Unknown error")]
    [InlineData("[DEBUG] Cache hit", "[디버그] Cache hit")]
    [InlineData("[TRACE] Call stack", "[추적] Call stack")]
    [InlineData("[FATAL] Crash", "[치명적_오류] Crash")]
    [InlineData("[SUCCESS] Task done", "[성공] Task done")]
    [InlineData("[FAIL] Task aborted", "[실패] Task aborted")]
    [InlineData("[정보] 이미 번역된 태그", "[정보] 이미 번역된 태그")]
    public void 공통_로그레벨_대괄호_태그_후처리_치환이_정상_동작한다(string input, string expected)
    {
        var actual = CommonBracketTagsRegex.Replace(input, m =>
        {
            var word = m.Groups[1].Value;
            var upper = word.ToUpperInvariant();
            var mapped = upper switch
            {
                "INF" or "INFO" or "INFORMATION" => "정보",
                "WARN" or "WARNING" => "경고",
                "ERR" or "ERROR" => "오류",
                "CRIT" or "CRITICAL" or "FATAL" => "치명적_오류",
                "DEBUG" or "DBG" => "디버그",
                "TRACE" or "TRC" => "추적",
                "SUCCESS" or "SUCCEEDED" or "OK" => "성공",
                "FAIL" or "FAILED" or "FAILURE" => "실패",
                _ => null
            };

            return mapped != null ? $"[{mapped}]" : m.Value;
        });

        Assert.Equal(expected, actual);
    }

    private static string WorkspaceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "microservices")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
