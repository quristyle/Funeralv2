using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JSini.Web.Http;

using JSini.Web.Components.Data;

namespace JSini.Web.Admin.Api;

/// <summary>RAG 문서함 조회·저장. 게이트웨이를 거쳐 AIAgentServer 로 간다.</summary>
public sealed class RagLibraryClient(GatewayClient gateway, HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── 카테고리 ────────────────────────────────────────────────

    public Task<IReadOnlyList<RagCategoryDto>> CategoriesAsync(CancellationToken ct = default)
        => CategoriesAsync(companyId: null, ct);

    public Task<IReadOnlyList<RagCategoryDto>> CategoriesAsync(
        string? companyId, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(companyId)
            ? "ai/rag/categories"
            : $"ai/rag/categories?companyId={Uri.EscapeDataString(companyId)}";
        return gateway.GetListAsync<RagCategoryDto>(url, ct);
    }

    public Task<object?> CreateCategoryAsync(
        string id, string name, string? description, string? companyId = null, CancellationToken ct = default)
        => gateway.PostAsync<object>("ai/rag/categories",
            new { id, name, description, companyId }, ct);

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

    /// <summary>분류를 옮긴다(부모·차례). 색인은 건드리지 않는다 — 색인은 카테고리 단위라 분류를 옮겨도 내용이 그대로다.</summary>
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

    /// <summary>문서를 다른 분류로 옮긴다. <paramref name="folderId"/> 가 <c>null</c> 이면 분류 미지정.</summary>
    public Task<object?> MoveDocumentAsync(
        long docId, long? folderId, CancellationToken ct = default)
        => gateway.PutAsync<object>($"ai/rag/documents/{docId}/folder", new { folderId }, ct);

    public Task DeleteDocumentAsync(long docId, CancellationToken ct = default)
        => gateway.DeleteAsync($"ai/rag/documents/{docId}", ct);

    /// <summary>내려받기 주소. 셸이 중계하는 길이다.</summary>
    public static string? DownloadUrl(RagDocumentDto doc)
        => doc.FileId is { } id ? FileDownload.UrlFor(id.ToString(), doc.FileName) : null;

    /// <summary>파일 하나를 올린다.</summary>
    public async Task<RagUploadedDto?> UploadOneAsync(
        string categoryId, long? folderId,
        Stream content, string fileName, string? contentType,
        Action<long>? onSent = null, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        await using var counted = new CountingStream(content, onSent);

        using var sc = new StreamContent(counted);
        sc.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(sc, "files", fileName);

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

        return env.Data?.Result is { Count: > 0 } rows ? rows[0] : null;
    }

    /// <summary>읽어 가는 바이트를 세어 알려 주는 껍데기.</summary>
    private sealed class CountingStream(Stream inner, Action<long>? onSent) : Stream
    {
        private long _total;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _total;
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken ct = default)
        {
            var n = await inner.ReadAsync(buffer, ct);
            if (n > 0)
            {
                _total += n;
                onSent?.Invoke(_total);
            }

            return n;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            if (n > 0)
            {
                _total += n;
                onSent?.Invoke(_total);
            }

            return n;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    // ── 색인 ────────────────────────────────────────────────────

    /// <summary>색인을 백그라운드에 맡긴다. 끝나기를 기다리지 않고 맡긴 뒤의 상태를 돌려준다.</summary>
    public Task<RagIndexStatusDto?> ReindexAsync(string categoryId, CancellationToken ct = default)
        => gateway.PostAsync<RagIndexStatusDto>(
            $"ai/rag/categories/{Uri.EscapeDataString(categoryId)}/reindex", new { }, ct);

    public Task<RagIndexStatusDto?> IndexStatusAsync(string categoryId, CancellationToken ct = default)
        => gateway.GetOneAsync<RagIndexStatusDto>(
            $"ai/rag/categories/{Uri.EscapeDataString(categoryId)}/reindex", ct);

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
