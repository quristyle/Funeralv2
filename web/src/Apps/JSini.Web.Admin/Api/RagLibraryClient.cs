using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>
/// RAG 문서함 조회·저장. 게이트웨이를 거쳐 AIAgentServer 로 간다.
/// </summary>
/// <remarks>
/// <b>올리기만 따로 <see cref="HttpClient"/> 를 쓴다.</b> <c>GatewayClient</c> 에는
/// 멀티파트가 없다 — 공지 첨부(<see cref="NoticeUploadClient"/>)와 같은 사정이고,
/// 같은 BaseAddress·같은 토큰 처리로 등록해 인증이 갈라지지 않게 한다.
/// </remarks>
public sealed class RagLibraryClient(GatewayClient gateway, HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── 카테고리 ────────────────────────────────────────────────

    public Task<IReadOnlyList<RagCategoryDto>> CategoriesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<RagCategoryDto>("ai/rag/categories", ct);

    public Task<object?> CreateCategoryAsync(
        string id, string name, string? description, CancellationToken ct = default)
        => gateway.PostAsync<object>("ai/rag/categories",
            new { id, name, description }, ct);

    public Task DeleteCategoryAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"ai/rag/categories/{Uri.EscapeDataString(id)}", ct);

    // ── 분류 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<RagFolderDto>> FoldersAsync(string categoryId, CancellationToken ct = default)
        => gateway.GetListAsync<RagFolderDto>(
            $"ai/rag/categories/{Uri.EscapeDataString(categoryId)}/folders", ct);

    public Task<object?> CreateFolderAsync(
        string categoryId, long? parentId, string name, CancellationToken ct = default)
        => gateway.PostAsync<object>(
            $"ai/rag/categories/{Uri.EscapeDataString(categoryId)}/folders",
            new { parentId, name }, ct);

    public Task<object?> RenameFolderAsync(long folderId, string name, CancellationToken ct = default)
        => gateway.PutAsync<object>($"ai/rag/folders/{folderId}", new { name }, ct);

    /// <summary>
    /// 분류를 옮긴다(부모·차례). <b>색인은 건드리지 않는다</b> —
    /// 색인은 카테고리 단위라 분류를 옮겨도 내용이 그대로다.
    /// </summary>
    public Task<object?> MoveFolderAsync(
        long folderId, long? parentId, long? beforeId, CancellationToken ct = default)
        => gateway.PutAsync<object>($"ai/rag/folders/{folderId}/move",
            new { parentId, beforeId }, ct);

    public Task DeleteFolderAsync(long folderId, CancellationToken ct = default)
        => gateway.DeleteAsync($"ai/rag/folders/{folderId}", ct);

    // ── 문서 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<RagDocumentDto>> DocumentsAsync(
        string categoryId, long? folderId, CancellationToken ct = default)
        => gateway.GetListAsync<RagDocumentDto>(
            $"ai/rag/categories/{Uri.EscapeDataString(categoryId)}/documents"
            + (folderId is { } f ? $"?folderId={f}" : string.Empty), ct);

    public Task DeleteDocumentAsync(long docId, CancellationToken ct = default)
        => gateway.DeleteAsync($"ai/rag/documents/{docId}", ct);

    public string DownloadUrl(long docId) => $"ai/rag/documents/{docId}/download";

    /// <summary>
    /// 파일 여러 개를 한 번에 올린다. <b>한 건씩 고르게 하면</b> 폴더째 올리려는
    /// 사람이 같은 창을 수십 번 연다.
    /// </summary>
    public async Task<IReadOnlyList<RagUploadedDto>> UploadAsync(
        string categoryId, long? folderId,
        IReadOnlyList<(Stream Content, string FileName, string? ContentType)> files,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var holders = new List<StreamContent>();

        foreach (var (content, name, type) in files)
        {
            var sc = new StreamContent(content);
            sc.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(type) ? "application/octet-stream" : type);
            form.Add(sc, "files", name);
            holders.Add(sc);
        }

        try
        {
            var url = $"ai/rag/categories/{Uri.EscapeDataString(categoryId)}/documents"
                      + (folderId is { } f ? $"?folderId={f}" : string.Empty);

            using var res = await http.PostAsync(url, form, ct);
            res.EnsureSuccessStatusCode();

            var env = await res.Content.ReadFromJsonAsync<Envelope<RagUploadedDto>>(JsonOptions, ct);
            if (env is null || !env.Success)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(env?.Message) ? "파일을 올리지 못했습니다." : env.Message);
            }

            return env.Data?.Result ?? [];
        }
        finally
        {
            foreach (var h in holders) h.Dispose();
        }
    }

    // ── 색인 ────────────────────────────────────────────────────

    public Task<RagReindexDto?> ReindexAsync(string categoryId, CancellationToken ct = default)
        => gateway.PostAsync<RagReindexDto>(
            $"ai/rag/categories/{Uri.EscapeDataString(categoryId)}/reindex", new { }, ct);

    private sealed class Envelope<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Payload<T>? Data { get; set; }
    }

    private sealed class Payload<T>
    {
        public List<T>? Result { get; set; }
    }
}
