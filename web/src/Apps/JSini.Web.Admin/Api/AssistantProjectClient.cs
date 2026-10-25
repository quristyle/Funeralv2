using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>
/// 업무 비서가 「어느 프로젝트의 자료를 볼까」를 채울 때 쓰는, <b>내가 참여한</b> 프로젝트 목록.
/// </summary>
/// 누구인지는 보내지 않는다 — 게이트웨이가 실은 신원(X-User-Id)으로 서버가 가린다.
/// 프로젝트관리 모듈에도 프로젝트를 읽는 클라이언트가 있지만 업무 모듈끼리는 참조하지
/// 않는 것이 규칙이라, 이 화면에 필요한 만큼만 따로 둔다.
public sealed class AssistantProjectClient(GatewayClient gateway)
{
    private const string Url = "projmng/projects/mine";

    /// <summary>
    /// 내 프로젝트 전부. <b>실패를 삼킨다</b> — 프로젝트를 못 읽어도 대화는 되어야 한다
    /// (고르개만 비고 사내 자료 전체에서 찾는다).
    /// </summary>
    public async Task<IReadOnlyList<AssistantProject>> MineAsync(CancellationToken ct = default)
    {
        try
        {
            return await gateway.GetListAsync<AssistantProject>(Url, ct);
        }
        catch (ApiException)
        {
            return [];
        }
    }
}

/// <summary>고를 수 있는 프로젝트 한 줄. 서버의 <c>ProjMngServer.Models.Project</c> 에서 이 화면이 쓰는 칸만 받는다.</summary>
public sealed class AssistantProject
{
    public int PrjRid { get; set; }

    public string? PrjName { get; set; }

    /// <summary>별칭. 다른 화면의 프로젝트 드롭다운에 나오는 이름이라 여기서도 그것을 먼저 쓴다.</summary>
    public string? PrjNick { get; set; }

    /// <summary>이 프로젝트에 매인 RAG 카테고리 열쇠(포털관리 › 프로젝트 목록에서 맨다).</summary>
    public List<string> RagCateIds { get; set; } = [];

    /// <summary>고르개에 보일 이름.</summary>
    public string Label => string.IsNullOrWhiteSpace(PrjNick)
        ? (string.IsNullOrWhiteSpace(PrjName) ? $"#{PrjRid}" : PrjName)
        : PrjNick;
}
