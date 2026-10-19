using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>팀 공유 문서(projmng/wbs-board/docs). 본문 HTML 은 그대로 담고 그대로 돌려준다. 고친 사람은 서버가 채운다.</summary>
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
