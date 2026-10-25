using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// RAG 카테고리 이름표. 프로젝트에 카테고리를 맬 때 고르개를 채우는 데만 쓴다.
/// </summary>
/// 카테고리 정본은 AIAgentServer(<c>/admin/ai/rag-documents</c> 가 다루는 그것)에 있다.
/// 여기서 만들거나 고치지 않는다 — 읽기만 한다. 포털관리 모듈에도 같은 길을 부르는
/// 클라이언트가 있지만 업무 모듈끼리는 참조하지 않는 것이 규칙이라, 읽기에 필요한
/// 만큼만 따로 둔다.
public sealed class RagCategoryClient(GatewayClient gateway)
{
    private const string Url = "ai/rag/categories";

    /// <summary>
    /// 고를 수 있는 카테고리 전부. <b>실패를 삼킨다</b> — 곁들이는 값이라
    /// AI 서버가 멎었다고 프로젝트 목록까지 안 열리면 안 된다.
    /// </summary>
    public async Task<IReadOnlyList<RagCategoryItem>> ListAsync(CancellationToken ct = default)
    {
        try
        {
            return await gateway.GetListAsync<RagCategoryItem>(Url, ct);
        }
        catch (ApiException)
        {
            return [];
        }
    }
}

/// <summary>카테고리 한 칸. 고르개에 쓸 열쇠와 이름만 받는다.</summary>
public sealed class RagCategoryItem
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>소속 회사 이름. 같은 이름의 카테고리가 회사별로 있을 때 가른다.</summary>
    public string? CompanyName { get; set; }

    /// <summary>지금 올라와 있는 문서 수. 빈 카테고리를 매는 실수를 줄인다.</summary>
    public int DocTotal { get; set; }

    /// <summary>고르개에 보일 글. 회사가 있으면 앞에 붙인다.</summary>
    public string Label => string.IsNullOrWhiteSpace(CompanyName)
        ? Name
        : $"{CompanyName} · {Name}";
}
