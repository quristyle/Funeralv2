using System.Text.Json;

using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>공통코드 한 건.</summary>
/// <param name="Code">코드값. 그리드 셀에 실제로 저장되는 값이다.</param>
/// <param name="Name">사람이 읽는 이름. 드롭다운과 셀에 보이는 값이다.</param>
/// <param name="Others">나머지 컬럼 전부(db_type · db_nick · db_schema 등). 부가 정보를 프로시저 파라미터로 되돌려 보낼 때 쓴다.</param>
public sealed record CommonCodeItem(
    string Code,
    string Name,
    IReadOnlyDictionary<string, string> Others);

/// <summary>
/// 공통코드를 읽고 캐시한다. 포털의 범용 셀렉트를 거치지 않고 제 통로(projmng/proj-codes)를 직접 부른다.
/// 사람마다 따로 담는다 — 프로젝트 목록은 그 사람이 참여한 프로젝트로 좁혀지기 때문이다(PerUserAsync).
/// </summary>
public sealed class CommonCodes(GatewayClient gateway, ReferenceData data)
{
    private const string Url = "projmng/proj-codes";

    /// <summary>참조자료 통 안에서의 묶음 이름. 코드를 고치는 화면이 이 이름으로 버린다.</summary>
    public const string Group = "projmng.common-code";

    /// <summary>
    /// 코드 목록을 읽는다. 같은 <paramref name="codeId"/> 는 한 번만 읽는다.
    /// </summary>
    /// <param name="codeId">코드 종류 (<c>CODE_TYPE</c> · <c>projdb</c> …)</param>
    /// <param name="key">종류 안에서 다시 거를 값. 안 쓰는 코드가 대부분이다.</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<IReadOnlyList<CommonCodeItem>> GetAsync(
        string codeId,
        string key = "",
        CancellationToken cancellationToken = default)
    {
        var cached = await data.PerUserAsync(Group, $"{codeId} {key}",
            () => LoadAsync(codeId, key, cancellationToken));

        return cached ?? [];
    }

    /// <summary>실제로 읽는다. null 을 돌려주면 통에 담기지 않아 다음 조회에서 다시 읽는다.</summary>
    private async Task<IReadOnlyList<CommonCodeItem>?> LoadAsync(
        string codeId,
        string key,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Dictionary<string, JsonElement>> rows;

        try
        {
            var query = string.IsNullOrEmpty(key)
                ? string.Empty
                : $"?etc0={Uri.EscapeDataString(key)}";

            rows = await gateway.GetListAsync<Dictionary<string, JsonElement>>(
                $"{Url}/{Uri.EscapeDataString(codeId)}{query}", cancellationToken);
        }
        catch (ApiException)
        {
            // 드롭다운 하나가 못 읽었다고 회로를 끊지 않는다 — 여기서 예외가 나가면 탭 전체가 멎는다.
            return null;
        }

        var items = new List<CommonCodeItem>(rows.Count);

        foreach (var row in rows)
        {
            // 칸 이름은 서버가 정한다 — 화면이 Others["db_nick"] 처럼 그대로 집으므로 바꾸지 않는다.
            var others = row.ToDictionary(
                pair => pair.Key,
                pair => Text(pair.Value),
                StringComparer.Ordinal);

            items.Add(new CommonCodeItem(
                others.GetValueOrDefault("code", string.Empty),
                others.GetValueOrDefault("name", string.Empty),
                others));
        }

        return items;
    }

    /// <summary>값 하나를 글자로. 문자열은 따옴표를 벗긴다 — 벗기지 않으면 따옴표째 프로시저에 실린다.</summary>
    private static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => value.ToString(),
    };

    /// <summary>캐시를 비운다. 코드를 편집하는 화면이 저장 뒤에 부른다. 통이 하나라 부르면 모두에게 반영된다.</summary>
    public void Clear() => data.Invalidate(Group);
}
