using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;

namespace AIAgentServer.Services.Portal;

/// <summary>포털에서 보낸 메일을 읽는다. NotificationServer 의 <c>/internal/mails</c> 를 부른다.
/// 그쪽 매개변수는 <c>DateTime?</c> 이지만 날짜만 적힌 글자("yyyy-MM-dd")를 받으면 <b>그 서버가 직접</b>
/// 한국 달력 하루 경계로 바꾼다 — 여기서 UTC 로 미리 바꿔 보내면 그 변환이 한 번 더 걸려 날짜가 어긋난다.</summary>
public sealed class MailGateway(HttpClient http, IConfiguration config)
{
    /// <summary>메일 한 줄. <c>Kind</c> 는 "보낸 메일"(손으로 쓴 메일) 또는 "발송 기록"(알림 직발송)이다.</summary>
    public sealed record MailRow(string Id, string? Subject, string? To, DateTime SentAt, string Kind);

    private sealed record Envelope<T>(int Total, List<T> Items);

    public async Task<(int Total, IReadOnlyList<MailRow> Items)> SearchMailsAsync(
        string? userId, string? from, string? to, string? keyword, int take, CancellationToken ct)
    {
        var query = new Dictionary<string, string?> { ["take"] = take.ToString() };
        if (userId is not null) query["userId"] = userId;
        if (from is not null) query["from"] = from;
        if (to is not null) query["to"] = to;
        if (keyword is not null) query["keyword"] = keyword;

        var url = QueryHelpers.AddQueryString("/internal/mails", query);
        var env = await GetAsync<Envelope<MailRow>>(url, ct);
        return env is null ? (0, []) : (env.Total, env.Items);
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("X-Internal-Token", config["Assistant:InternalToken"]);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    }
}
