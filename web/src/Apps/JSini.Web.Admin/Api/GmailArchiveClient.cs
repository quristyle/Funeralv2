using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>Gmail 보관함 화면이 쓰는 길. 읽기·시작은 평범한 JSON 이라 게이트웨이 클라이언트를 쓰고,
/// mbox 올리기만 멀티파트라 맨 <see cref="HttpClient"/> 가 따로 필요하다(RagLibraryClient 와 같은 사정).</summary>
public sealed class GmailArchiveClient(GatewayClient gateway, HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    // ── 계정 ────────────────────────────────────────────────
    public Task<IReadOnlyList<GmailAccountDto>> AccountsAsync(CancellationToken ct = default) =>
        gateway.GetListAsync<GmailAccountDto>("ai/gmail/accounts", ct);

    public Task<GmailAccountDto?> SaveAccountAsync(GmailAccountSave body, CancellationToken ct = default) =>
        gateway.PostAsync<GmailAccountDto>("ai/gmail/accounts", body, ct);

    // ── 적재 ────────────────────────────────────────────────
    public Task<IReadOnlyList<GmailMboxFileDto>> FilesAsync(CancellationToken ct = default) =>
        gateway.GetListAsync<GmailMboxFileDto>("ai/gmail/import/files", ct);

    public Task<GmailImportStartedDto?> StartImportAsync(
        string accountId, string file, CancellationToken ct = default) =>
        gateway.PostAsync<GmailImportStartedDto>(
            "ai/gmail/import/takeout", new { accountId, file }, ct);

    public Task<GmailImportStatusDto?> ImportStatusAsync(CancellationToken ct = default) =>
        gateway.GetOneAsync<GmailImportStatusDto>("ai/gmail/import/status", ct);

    // ── RAG 문서 만들기 ─────────────────────────────────────

    /// <summary>오래된 메일을 묶어 RAG 문서로 올린다.
    /// <paramref name="folderId"/> 가 <c>null</c> 이면 카테고리 바로 아래에 둔다.</summary>
    public Task<GmailArchiveResultDto?> ArchiveAsync(
        string? categoryId, long? folderId, int maxMessages, int afterMonths,
        CancellationToken ct = default) =>
        gateway.PostAsync<GmailArchiveResultDto>(
            "ai/gmail/archive", new { categoryId, folderId, maxMessages, afterMonths }, ct);

    // ── 현황 ────────────────────────────────────────────────
    public Task<IReadOnlyList<GmailStatDto>> StatsAsync(CancellationToken ct = default) =>
        gateway.GetListAsync<GmailStatDto>("ai/gmail/stats", ct);

    // ── 증분 ────────────────────────────────────────────────
    public Task<GmailSyncResultDto?> SyncAsync(
        string accountId, int maxMessages, CancellationToken ct = default) =>
        gateway.PostAsync<GmailSyncResultDto>(
            "ai/gmail/sync", new { accountId, maxMessages }, ct);

    /// <summary>mbox 하나를 적재 폴더로 올린다. 큰 파일이라 <b>스트림 그대로</b> 보낸다 —
    /// 바이트 배열로 바꿔 담으면 브라우저 쪽 메모리가 먼저 터진다.
    /// <paramref name="onSent"/> 는 보낸 바이트를 알린다(<see cref="CountingStream"/>) —
    /// 이 구간은 DevExpress 가 「Uploaded」로 바뀐 뒤에도 한참 더 도는 자리다.</summary>
    public async Task<GmailMboxFileDto?> UploadAsync(
        Stream content, string fileName, Action<long>? onSent = null, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        await using var counted = new CountingStream(content, onSent);
        using var sc = new StreamContent(counted);
        sc.Headers.ContentType = new MediaTypeHeaderValue("application/mbox");
        form.Add(sc, "file", fileName);

        using var res = await http.PostAsync("ai/gmail/import/upload", form, ct);
        res.EnsureSuccessStatusCode();

        var env = await res.Content.ReadFromJsonAsync<UploadEnvelope>(JsonOptions, ct);
        if (env is null || !env.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(env?.Message) ? "파일을 올리지 못했습니다." : env.Message);
        }

        return env.Data?.Result?.FirstOrDefault();
    }

    private sealed class UploadEnvelope
    {
        public bool Success { get; set; }

        public string? Message { get; set; }

        public UploadPayload? Data { get; set; }
    }

    private sealed class UploadPayload
    {
        public List<GmailMboxFileDto>? Result { get; set; }
    }
}

public sealed class GmailAccountDto
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string OwnerLoginId { get; set; } = string.Empty;

    /// <summary><c>api</c>(Gmail API) 또는 <c>imap</c>.</summary>
    public string Kind { get; set; } = "api";

    public bool Enabled { get; set; } = true;

    public DateTime? LastSyncAt { get; set; }

    public string? LastError { get; set; }

    public string KindLabel => Kind == "imap" ? "IMAP (개인 Gmail)" : "Gmail API (Workspace)";
}

public sealed record GmailAccountSave(string Id, string Email, string? OwnerLoginId, string Kind, bool Enabled);

public sealed class GmailMboxFileDto
{
    public string Name { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public string SizeLabel => SizeBytes switch
    {
        >= 1L << 30 => $"{SizeBytes / (double)(1L << 30):F1} GB",
        >= 1L << 20 => $"{SizeBytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{SizeBytes / (double)(1L << 10):F0} KB",
        _ => $"{SizeBytes} B",
    };
}

public sealed class GmailImportStartedDto
{
    public bool Started { get; set; }

    public string? AccountId { get; set; }

    public string? File { get; set; }
}

public sealed class GmailImportStatusDto
{
    public bool Running { get; set; }

    public string? AccountId { get; set; }

    public string? Path { get; set; }

    public long Parsed { get; set; }

    /// <summary>실제로 표에 들어간 줄. <see cref="Parsed"/> 와의 차이가 겹쳐서 버린 수다.</summary>
    public long Inserted { get; set; }

    public long Duplicate { get; set; }

    public long Failed { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string? Error { get; set; }
}

public sealed class GmailArchiveResultDto
{
    public int Documents { get; set; }

    public int Messages { get; set; }

    /// <summary>못 한 까닭이나 「남은 것이 없다」. 실패가 아니라 알림이다.</summary>
    public string? Note { get; set; }
}

public sealed class GmailSyncResultDto
{
    public string? Kind { get; set; }

    public int Fetched { get; set; }

    public int Inserted { get; set; }

    public int Duplicate { get; set; }

    public string? Note { get; set; }
}

public sealed class GmailStatDto
{
    public string AccountId { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public int Count { get; set; }

    public DateTime? Oldest { get; set; }

    public DateTime? Newest { get; set; }

    public string SourceLabel => Source switch
    {
        "takeout" => "Takeout(mbox)",
        "api" => "Gmail API",
        "imap" => "IMAP",
        _ => Source,
    };
}
