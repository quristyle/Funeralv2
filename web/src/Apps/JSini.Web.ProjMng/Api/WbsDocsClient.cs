using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>팀 공유 문서 — <c>projmng/wbs-board/docs</c>.</summary>
/// <remarks>
/// 본문은 서식 편집기가 만든 HTML 이라 <b>그대로 담고 그대로 돌려준다</b>.
/// 고친 사람은 서버가 게이트웨이 신원으로 채운다 — 보내지 않는다.
/// </remarks>
public sealed class WbsDocsClient(GatewayClient gateway)
{
    private const string Url = "projmng/wbs-board/docs";

    public Task<IReadOnlyList<WbsDocDto>> ListAsync(int prjRid, CancellationToken ct = default)
        => gateway.GetListAsync<WbsDocDto>($"{Url}?prjRid={prjRid}", ct);

    public Task CreateAsync(int prjRid, WbsDocDto doc, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}?prjRid={prjRid}", doc, ct);

    public Task UpdateAsync(int prjRid, WbsDocDto doc, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{doc.Id}?prjRid={prjRid}", doc, ct);

    public Task DeleteAsync(int prjRid, int id, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{id}?prjRid={prjRid}", ct);
}

/// <summary>공유 문서 한 쪽.</summary>
public sealed class WbsDocDto
{
    public int Id { get; set; }
    public string? Title { get; set; }

    /// <summary>본문 HTML.</summary>
    public string? Content { get; set; }

    /// <summary>목차의 차례. <b>0 이면 서버가 맨 뒤로 보낸다.</b></summary>
    public int SortOrder { get; set; }

    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}
