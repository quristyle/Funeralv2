using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;

namespace AIAgentServer.Services.Portal;

/// <summary>헬프데스크의 요청 글·일정을 읽는다. HelpDeskServer 의 <c>/internal/*</c> 를 부른다 — 게이트웨이를
/// 거치지 않는 서버 간 호출이라 <see cref="InternalHeader"/> 머리글로만 자격을 증명한다.</summary>
public sealed class HelpDeskGateway(HttpClient http, IConfiguration config)
{
    /// <summary>요청 글 목록의 한 줄.</summary>
    public sealed record RequestRow(
        int Id, string Title, string Status, DateTime RequestedAt, DateTime? AcceptedAt, DateTime? CompletedAt,
        string? AuthorName, string? AssigneeName, string? CompanyName, string Match);

    /// <summary>요청 글 상세 — 본문과 댓글.</summary>
    public sealed record RequestDetail(
        int Id, string Title, string Description, string Status, DateTime RequestedAt, DateTime? AcceptedAt,
        DateTime? CompletedAt, string? AuthorName, string? AssigneeName, string? CompanyName,
        List<RequestComment> Comments);

    public sealed record RequestComment(
        int Id, string AuthorType, string? AuthorName, string CommentText, bool IsDeleted,
        DateTime CreatedAt, int? ParentCommentId);

    /// <summary>헬프데스크 일정 한 줄. <c>Owner</c> 는 이미 서버 쪽에서 사람 이름으로 풀려 있다 —
    /// 프로젝트관리 쪽(<see cref="ProjMngGateway.ScheduleRow"/>)의 원문 그대로인 값과 다르다.</summary>
    public sealed record ScheduleRow(string Source, string Title, DateTime StartDate, DateTime EndDate, bool Done, string? Owner);

    private sealed record Envelope<T>(int Total, List<T> Items);

    /// <summary>요청 글 목록. <paramref name="from"/>·<paramref name="to"/> 는 이미 UTC 로 바꾼 경계여야 한다
    /// (<see cref="PortalDay"/>) — 이 서버는 날짜를 다시 안 바꾸고 그대로 견준다.</summary>
    public async Task<(int Total, IReadOnlyList<RequestRow> Items)> SearchRequestsAsync(
        string? userId, DateTime? from, DateTime? to, string? status, string? keyword, int take, CancellationToken ct)
    {
        var query = BaseQuery(take);
        if (userId is not null) query["userId"] = userId;
        if (from is not null) query["from"] = from.Value.ToString("o");
        if (to is not null) query["to"] = to.Value.ToString("o");
        if (status is not null) query["status"] = status;
        if (keyword is not null) query["keyword"] = keyword;

        var url = QueryHelpers.AddQueryString("/internal/requests", query);
        var env = await GetAsync<Envelope<RequestRow>>(url, ct);
        return env is null ? (0, []) : (env.Total, env.Items);
    }

    /// <summary>요청 글 하나의 본문과 댓글. 없으면 <c>null</c>.</summary>
    public Task<RequestDetail?> GetRequestAsync(int id, CancellationToken ct)
        => GetAsync<RequestDetail>($"/internal/requests/{id}", ct);

    /// <summary>헬프데스크 일정 목록. 날짜 경계는 <see cref="SearchRequestsAsync"/> 와 같은 규칙(UTC 로 미리 바꿔서).</summary>
    public async Task<(int Total, IReadOnlyList<ScheduleRow> Items)> SearchSchedulesAsync(
        string? userId, DateTime? from, DateTime? to, string? keyword, int take, CancellationToken ct)
    {
        var query = BaseQuery(take);
        if (userId is not null) query["userId"] = userId;
        if (from is not null) query["from"] = from.Value.ToString("o");
        if (to is not null) query["to"] = to.Value.ToString("o");
        if (keyword is not null) query["keyword"] = keyword;

        var url = QueryHelpers.AddQueryString("/internal/schedules", query);
        var env = await GetAsync<Envelope<ScheduleRow>>(url, ct);
        return env is null ? (0, []) : (env.Total, env.Items);
    }

    private static Dictionary<string, string?> BaseQuery(int take) => new() { ["take"] = take.ToString() };

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("X-Internal-Token", config["Assistant:InternalToken"]);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    }
}
