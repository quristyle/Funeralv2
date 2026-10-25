using System.Net.Http.Headers;
using System.Text.Json;

using AIAgentServer.Data;

using Microsoft.EntityFrameworkCore;

using MimeKit;

namespace AIAgentServer.Services.Gmail;

/// <summary>Workspace 계정의 메일을 Gmail REST API 로 받아 온다.
///
/// <para><b>SDK 를 안 쓴다.</b> 쓰는 것이 토큰 새로 받기 · 목록 · 본문 · 변경이력 넷뿐이라
/// <c>Google.Apis.Gmail.v1</c> 을 들이는 값이 안 나온다. 그 꾸러미는 자체 인증·재시도 계층을 들고 오는데
/// 그만큼 우리가 못 보는 동작이 는다.</para>
///
/// <para><b>본문은 <c>format=raw</c> 로 받는다.</b> 파싱된 JSON(<c>format=full</c>)을 받으면 길마다
/// 다른 모양을 각각 풀어야 하지만, raw 는 그냥 MIME 이라 <see cref="MailNormalizer"/> 하나로 Takeout·
/// IMAP 과 똑같이 다룬다 — 겹침 열쇠(Message-ID)도 같은 자리에서 나온다.</para>
///
/// <para><b>동의 화면은 「내부(Internal)」여야 한다.</b> <c>gmail.readonly</c> 는 제한된 스코프라,
/// 외부 앱이면 테스트 상태에서 <b>리프레시 토큰이 7일 만에 죽고</b> 게시하려면 구글 보안 심사를 받아야
/// 한다. 내부 앱은 둘 다 면제다.</para></summary>
public sealed class GmailApiCollector(
    HttpClient http,
    AiUsageDbContext db,
    GmailWriter writer,
    IConfiguration config,
    ILogger<GmailApiCollector> logger)
{
    private const string Api = "https://gmail.googleapis.com/gmail/v1/users/me";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";

    /// <summary>한 번에 표에 넣는 줄 수.</summary>
    private const int BatchSize = 100;

    /// <summary>첫 수집에서 가져올 최대 통수. Takeout 이 과거를 맡으므로 여기서 전부 끌어올 까닭이 없다 —
    /// 할당량(사용자당 초당 250단위)을 메일 하나당 5단위씩 먹는다.</summary>
    private const int FirstRunCap = 2000;

    public sealed record Result(int Fetched, int Inserted, string? HistoryId, string? Note);

    public async Task<Result> SyncAsync(GmailAccountRow account, int max, CancellationToken ct)
    {
        var clientId = config["Gmail:OAuth:ClientId"];
        var clientSecret = config["Gmail:OAuth:ClientSecret"];
        var refreshToken = config[$"Gmail:Credentials:{account.Id}:RefreshToken"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return new Result(0, 0, null, "Gmail:OAuth 의 ClientId·ClientSecret 이 없습니다.");

        if (string.IsNullOrWhiteSpace(refreshToken))
            return new Result(0, 0, null, $"Gmail:Credentials:{account.Id}:RefreshToken 이 없습니다.");

        var token = await AccessTokenAsync(clientId, clientSecret, refreshToken, ct);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 이어받을 표식이 있으면 변경이력만, 없으면 목록부터 — 첫 수집과 그 뒤가 길이 다르다.
        var (ids, historyId, note) = string.IsNullOrWhiteSpace(account.LastHistoryId)
            ? await FirstRunIdsAsync(Math.Min(max, FirstRunCap), ct)
            : await ChangedIdsAsync(account.LastHistoryId!, max, ct);

        if (note is not null) return new Result(0, 0, null, note);

        var fetched = 0;
        var inserted = 0;
        var batch = new List<GmailMessageRow>(BatchSize);

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();

            var row = await FetchAsync(account.Id, id, ct);
            if (row is null) continue;

            batch.Add(row);
            fetched++;

            if (batch.Count >= BatchSize)
            {
                inserted += await writer.InsertAsync(batch, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0) inserted += await writer.InsertAsync(batch, ct);

        await StampAsync(account.Id, historyId, ct);

        logger.LogInformation(
            "Gmail API 수집: 계정 {Account} · 받음 {Fetched} · 넣음 {Inserted} · 이력 {History}",
            account.Id, fetched, inserted, historyId);

        return new Result(fetched, inserted, historyId, null);
    }

    private async Task<string> AccessTokenAsync(
        string clientId, string clientSecret, string refreshToken, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        });

        using var res = await http.PostAsync(TokenUrl, form, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            // 내용에 리프레시 토큰이 안 담기도록 구글이 준 글만 그대로 올린다.
            throw new InvalidOperationException($"토큰을 새로 받지 못했습니다({(int)res.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("토큰 응답에 access_token 이 없습니다.");
    }

    /// <summary>첫 수집 — 최근 것부터 목록을 훑는다. 함께 돌려주는 이력 번호가 다음 증분의 출발점이다.</summary>
    private async Task<(List<string> Ids, string? HistoryId, string? Note)> FirstRunIdsAsync(
        int max, CancellationToken ct)
    {
        // **이력 번호를 먼저 잡는다.** 목록을 다 훑은 뒤에 잡으면 그 사이 들어온 메일이
        // 어느 쪽에도 안 걸려 영영 빠진다.
        var historyId = await ProfileHistoryIdAsync(ct);

        var ids = new List<string>();
        string? pageToken = null;

        do
        {
            var url = $"{Api}/messages?maxResults=500&q=-in:spam+-in:trash"
                      + (pageToken is null ? string.Empty : $"&pageToken={pageToken}");

            using var doc = await GetJsonAsync(url, ct);
            if (doc.RootElement.TryGetProperty("messages", out var arr))
            {
                foreach (var m in arr.EnumerateArray())
                {
                    if (m.TryGetProperty("id", out var id) && id.GetString() is { } s) ids.Add(s);
                    if (ids.Count >= max) return (ids, historyId, null);
                }
            }

            pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var np) ? np.GetString() : null;
        }
        while (pageToken is not null);

        return (ids, historyId, null);
    }

    /// <summary>증분 — 지난 번 이력 번호 뒤에 더해진 메일만.</summary>
    private async Task<(List<string> Ids, string? HistoryId, string? Note)> ChangedIdsAsync(
        string startHistoryId, int max, CancellationToken ct)
    {
        var ids = new List<string>();
        string? pageToken = null;
        string? newest = startHistoryId;

        do
        {
            var url = $"{Api}/history?startHistoryId={startHistoryId}&historyTypes=messageAdded"
                      + (pageToken is null ? string.Empty : $"&pageToken={pageToken}");

            JsonDocument doc;
            try
            {
                doc = await GetJsonAsync(url, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // 이력 번호가 너무 오래되면 구글이 버린다. 그때는 표식을 지우고 다음 번에
                // 첫 수집으로 다시 시작한다 — 겹침은 Message-ID 가 지우므로 손해가 없다.
                return ([], null, "이력 번호가 만료됐습니다. 표식을 지웠으니 다시 부르면 처음부터 받습니다.");
            }

            using (doc)
            {
                if (doc.RootElement.TryGetProperty("historyId", out var h)) newest = h.GetString();

                if (doc.RootElement.TryGetProperty("history", out var hist))
                {
                    foreach (var entry in hist.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("messagesAdded", out var added)) continue;
                        foreach (var a in added.EnumerateArray())
                        {
                            if (a.TryGetProperty("message", out var m)
                                && m.TryGetProperty("id", out var id)
                                && id.GetString() is { } s)
                            {
                                ids.Add(s);
                            }

                            if (ids.Count >= max) return (ids, newest, null);
                        }
                    }
                }

                pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var np) ? np.GetString() : null;
            }
        }
        while (pageToken is not null);

        return (ids, newest, null);
    }

    private async Task<string?> ProfileHistoryIdAsync(CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"{Api}/profile", ct);
        return doc.RootElement.TryGetProperty("historyId", out var h) ? h.GetString() : null;
    }

    /// <summary>메일 한 통을 MIME 그대로 받아 표 줄로 바꾼다.</summary>
    private async Task<GmailMessageRow?> FetchAsync(string accountId, string messageId, CancellationToken ct)
    {
        try
        {
            using var doc = await GetJsonAsync($"{Api}/messages/{messageId}?format=raw", ct);
            var root = doc.RootElement;

            var raw = root.TryGetProperty("raw", out var r) ? r.GetString() : null;
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var labels = new List<string>();
            if (root.TryGetProperty("labelIds", out var ls))
            {
                labels.AddRange(ls.EnumerateArray().Select(x => x.GetString()).OfType<string>());
            }

            var threadId = root.TryGetProperty("threadId", out var t) ? t.GetString() : null;

            using var stream = new MemoryStream(Base64Url(raw!));
            var message = await MimeMessage.LoadAsync(stream, ct);
            return MailNormalizer.FromMime(message, accountId, "api", messageId, threadId, labels);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 한 통이 깨져도 나머지를 포기하지 않는다.
            logger.LogWarning(ex, "메일 {Id} 를 받지 못했습니다 (건너뜁니다)", messageId);
            return null;
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var res = await http.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Gmail 의 raw 는 <b>base64url</b> 이다 — 표준 base64 로 읽으면 <c>-</c>·<c>_</c> 에서 터진다.</summary>
    private static byte[] Base64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }

    private Task StampAsync(string accountId, string? historyId, CancellationToken ct) =>
        db.GmailAccounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.LastHistoryId, historyId)
                .SetProperty(a => a.LastSyncAt, DateTime.UtcNow)
                .SetProperty(a => a.LastError, (string?)null), ct);
}
