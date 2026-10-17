using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JSini.Web.Http;

using JSini.Web.Components.Data;

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

    /// <summary>
    /// 문서를 다른 분류로 옮긴다. <paramref name="folderId"/> 가 <c>null</c> 이면 분류 미지정.
    /// </summary>
    /// <remarks>
    /// <b>색인은 건드리지 않는다</b> — 분류 옮기기와 같은 까닭이다. 토막에도
    /// 디스크의 자리에도 분류가 적혀 있지 않다.
    /// </remarks>
    public Task<object?> MoveDocumentAsync(
        long docId, long? folderId, CancellationToken ct = default)
        => gateway.PutAsync<object>($"ai/rag/documents/{docId}/folder", new { folderId }, ct);

    public Task DeleteDocumentAsync(long docId, CancellationToken ct = default)
        => gateway.DeleteAsync($"ai/rag/documents/{docId}", ct);

    /// <summary>
    /// 내려받기 주소. <b>셸이 중계하는 길이다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 전에는 <c>ai/rag/documents/{id}/download</c> 를 그대로 돌려주었다. 그것은
    /// <b>게이트웨이 기준 경로</b>(이 클래스의 <c>HttpClient</c> 가 쓰는 값)인데
    /// 화면이 그 글자를 <c>&lt;a href&gt;</c> 에 걸어서, 브라우저가 **지금 쪽
    /// 주소 기준**으로 풀었다 —
    /// <c>/admin/ai/rag-documents</c> + <c>ai/rag/…</c> →
    /// <c>/admin/ai/ai/rag/documents/4/download</c>. 앞에 슬래시를 붙여도 안 된다.
    /// 포털(:5557)에는 <c>/api</c> 가 없고 브라우저에는 게이트웨이 토큰도 없다(BFF).
    /// </para>
    /// <para>
    /// 그래서 파일 서버의 아이디로 <see cref="FileDownload.UrlFor"/> 를 부른다 —
    /// 공지 첨부·자료실과 같은 길이고, 권한 판정도 파일 서버가 한다.
    /// </para>
    /// </remarks>
    public static string? DownloadUrl(RagDocumentDto doc)
        => doc.FileId is { } id ? FileDownload.UrlFor(id.ToString(), doc.FileName) : null;

    /// <summary>
    /// 파일 <b>하나</b>를 올린다.
    /// </summary>
    /// <param name="categoryId">넣을 카테고리.</param>
    /// <param name="folderId">넣을 분류. null 이면 분류 미지정.</param>
    /// <param name="content">파일 내용. 브라우저에서 건너오는 스트림이다.</param>
    /// <param name="fileName">원본 파일 이름.</param>
    /// <param name="contentType">MIME 형식. 모르면 application/octet-stream.</param>
    /// <param name="onSent">
    /// 보낸 바이트가 늘 때마다 부른다. <b>부르는 쪽이 간격을 둔다</b> —
    /// 버퍼마다 화면을 다시 그리면 회로가 넘친다.
    /// </param>
    /// <param name="ct">취소 토큰.</param>
    /// <remarks>
    /// <para>
    /// 여러 개를 한 묶음으로 보내지 않는다. 그러면 <b>어디까지 갔는지 알 수
    /// 없어</b> 큰 파일을 올릴 때 화면이 "올리는 중" 에서 멎은 것처럼 보인다.
    /// 한 건씩 보내면 파일마다 진행을 보여 줄 수 있고, 하나가 실패해도 나머지는
    /// 올라간다.
    /// </para>
    /// <para>
    /// Blazor 서버에서 <c>IBrowserFile</c> 의 스트림을 읽는 것이 곧 브라우저에서
    /// 서버로 건너오는 과정이다. 그래서 여기서 세는 바이트가 <b>사람이 기다리는
    /// 바로 그 전송</b>이다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 읽어 가는 바이트를 세어 알려 주는 껍데기.
    /// </summary>
    /// <remarks>
    /// <c>StreamContent</c> 가 이 스트림을 읽어 내보내므로, 여기서 세는 것이
    /// 곧 보낸 양이다. <b>읽기 전용</b>이고 길이도 모른다 — 브라우저 스트림이
    /// 찾아가기(seek)를 못 하기 때문이다.
    /// </remarks>
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
