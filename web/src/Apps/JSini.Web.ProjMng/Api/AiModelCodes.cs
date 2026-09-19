using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>AI 실행기 선택 항목.</summary>
public sealed record AiModelOption(string Value, string Text);

/// <summary>
/// 프로젝트관리 공통코드의 <c>AI_MODEL</c> 묶음.
/// 화면에 실행기 이름을 하드코딩하지 않고 공통코드에서 읽는다.
/// </summary>
public sealed class AiModelCodes(DevCommonCodeClient codes)
{
    public const string GroupCode = "AI_MODEL";

    public async Task<IReadOnlyList<AiModelOption>> GetAsync(
        CancellationToken ct = default)
    {
        try
        {
            var rows = await codes.ChildrenAsync(GroupCode, ct);

            return [.. rows
                .Where(x => !string.IsNullOrWhiteSpace(x.CmCd))
                .Select(x => new AiModelOption(
                    x.CmCd!.Trim(),
                    string.IsNullOrWhiteSpace(x.CmNm) ? x.CmCd.Trim() : x.CmNm.Trim()))];
        }
        catch (ApiException)
        {
            // 선택 목록을 못 읽었다고 화면 회로를 끊지 않는다.
            return [];
        }
    }
}
