using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>팀 공유 문서(projmng/wbs-board/docs). 본문 HTML 은 그대로 담고 그대로 돌려준다. 고친 사람은 서버가 채우고 이름은 <see cref="WbsBoardNames"/> 가 붙인다.</summary>
public sealed class WbsDocsClient(GatewayClient gateway, WbsBoardNames names)
{
    private const string Url = "projmng/wbs-board/docs";

    public async Task<IReadOnlyList<WbsDocDto>> ListAsync(int prjRid, CancellationToken ct = default)
        => await NameAsync(await gateway.GetListAsync<WbsDocDto>($"{Url}?prjRid={prjRid}", ct), ct);

    public async Task<IReadOnlyList<WbsDocDto>> TrashAsync(int prjRid, CancellationToken ct = default)
        => await NameAsync(await gateway.GetListAsync<WbsDocDto>($"{Url}/trash?prjRid={prjRid}", ct), ct);

    /// <summary>새 쪽의 번호를 준다 — 만든 쪽을 제목으로 되찾으면 같은 제목이 둘일 때 엉뚱한 쪽이 열린다.</summary>
    public Task<int> CreateAsync(int prjRid, WbsDocDto doc, CancellationToken ct = default)
        => gateway.PostAsync<int>($"{Url}?prjRid={prjRid}", doc, ct);

    public Task UpdateAsync(int prjRid, WbsDocDto doc, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{doc.Id}?prjRid={prjRid}", doc, ct);

    /// <summary>목차 차례만 바꾼다. 고친 때가 안 올라가 「변경됨」이 켜지지 않는다.</summary>
    public Task ReorderAsync(int prjRid, IEnumerable<int> ids, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/order?prjRid={prjRid}", ids.ToList(), ct);

    /// <summary>휴지통으로 보낸다.</summary>
    public Task DeleteAsync(int prjRid, int id, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{id}?prjRid={prjRid}", ct);

    /// <summary>휴지통에서 영영 지운다. 붙인 문서도 함께 사라진다.</summary>
    public Task PurgeAsync(int prjRid, int id, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{id}?prjRid={prjRid}&purge=true", ct);

    public Task RestoreAsync(int prjRid, int id, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}/{id}/restore?prjRid={prjRid}", null, ct);

    /// <summary>프로젝트의 붙인 문서 전부. 화면이 쪽별로 가른다.</summary>
    public async Task<IReadOnlyList<WbsDocFileDto>> FilesAsync(int prjRid, CancellationToken ct = default)
    {
        var rows = await gateway.GetListAsync<WbsDocFileDto>($"{Url}/files?prjRid={prjRid}", ct);
        return await names.FillAsync(rows, (f, nameOf) => f.UploadedBy = nameOf(f.UploadedBy), ct);
    }

    public Task<WbsDocFileDto?> UploadAsync(int prjRid, int docId, string name, string html, CancellationToken ct = default)
        => gateway.PostAsync<WbsDocFileDto>($"{Url}/{docId}/files?prjRid={prjRid}", new { name, html }, ct);

    public Task<WbsDocFileDto?> LinkAsync(int prjRid, int docId, string url, string? name, CancellationToken ct = default)
        => gateway.PostAsync<WbsDocFileDto>($"{Url}/{docId}/links?prjRid={prjRid}", new { url, name }, ct);

    public Task RemoveFileAsync(int prjRid, int docId, int fileId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{docId}/files/{fileId}?prjRid={prjRid}", ct);

    public Task ReorderFilesAsync(int prjRid, int docId, IEnumerable<int> ids, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{docId}/files/order?prjRid={prjRid}", ids.ToList(), ct);

    private Task<IReadOnlyList<WbsDocDto>> NameAsync(IReadOnlyList<WbsDocDto> rows, CancellationToken ct)
        => names.FillAsync(rows, (d, nameOf) =>
        {
            d.UpdatedBy = nameOf(d.UpdatedBy);
            d.CreatedBy = nameOf(d.CreatedBy);
            d.DeletedBy = nameOf(d.DeletedBy);
        }, ct);
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

    /// <summary>한국 시각 「yyyy-MM-dd HH:mm」. 안 본 변경을 가르는 열쇠로도 쓴다.</summary>
    public string? UpdatedAt { get; set; }

    /// <summary>처음 쓴 때·사람. 칸을 넣기 전에 만든 쪽은 비어 있다.</summary>
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }

    /// <summary>휴지통 목록에서만 채워진다.</summary>
    public string? DeletedBy { get; set; }
    public string? DeletedAt { get; set; }
}

/// <summary>쪽에 붙인 문서 한 장 — 올린 HTML 이거나 걸어 둔 주소.</summary>
public sealed class WbsDocFileDto
{
    public int Id { get; set; }
    public int DocId { get; set; }
    public string? Name { get; set; }

    /// <summary>걸어 둔 주소. 비어 있으면 올린 HTML 이다.</summary>
    public string? Url { get; set; }

    public long Bytes { get; set; }
    public int SortOrder { get; set; }
    public string? UploadedBy { get; set; }
    public string? UploadedAt { get; set; }

    public bool IsLink => !string.IsNullOrEmpty(Url);
}
