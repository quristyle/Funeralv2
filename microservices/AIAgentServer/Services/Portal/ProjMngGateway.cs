using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;

namespace AIAgentServer.Services.Portal;

/// <summary>프로젝트관리의 일정(WBS)·할 일을 읽는다. ProjMngServer 의 <c>/internal/*</c> 를 부른다.
/// 날짜 매개변수는 <c>DateOnly</c> 로 그대로 받는 서버라, 한국 날짜 글자("yyyy-MM-dd")를
/// UTC 로 바꾸지 않고 그대로 넘긴다 — <see cref="HelpDeskGateway"/> 와 다른 점이다.</summary>
public sealed class ProjMngGateway(HttpClient http, IConfiguration config)
{
    /// <summary>프로젝트관리 일정 한 줄. <c>Owner</c> 는 담당자 칸(<c>dev_user</c>)의 <b>원문 그대로</b>다 —
    /// 로그인 아이디일 수도, 사람이 손으로 적은 이름일 수도 있다. 헬프데스크 쪽처럼 이름으로 풀려 있지 않다.</summary>
    public sealed record ScheduleRow(
        string Source, string? Title, DateOnly? StartDate, DateOnly? EndDate, bool Done,
        string? Owner, string State, string? Role, string? Note);

    /// <summary>할 일 한 줄.</summary>
    public sealed record TodoRow(string? Title, DateOnly? TargetDay, bool Done, string? Owner, string? Comments);

    private sealed record Envelope<T>(int Total, List<T> Items);

    /// <summary>프로젝트관리 일정 목록. <paramref name="userId"/>·<paramref name="name"/> 아무거나 맞으면
    /// 걸린다 — 담당자 칸이 손으로 적은 글이라 아이디만으로는 거의 안 걸린다(WbsPersonMatch).</summary>
    public async Task<(int Total, IReadOnlyList<ScheduleRow> Items)> SearchSchedulesAsync(
        string? userId, string? name, string? from, string? to, string? keyword, int take, CancellationToken ct)
    {
        var query = BaseQuery(take);
        if (userId is not null) query["userId"] = userId;
        if (name is not null) query["name"] = name;
        if (from is not null) query["from"] = from;
        if (to is not null) query["to"] = to;
        if (keyword is not null) query["keyword"] = keyword;

        var url = QueryHelpers.AddQueryString("/internal/schedules", query);
        var env = await GetAsync<Envelope<ScheduleRow>>(url, ct);
        return env is null ? (0, []) : (env.Total, env.Items);
    }

    /// <summary>할 일 목록. 사람은 <c>target_user</c> 하나뿐이라 아이디로만 찾는다.</summary>
    public async Task<(int Total, IReadOnlyList<TodoRow> Items)> SearchTodosAsync(
        string? userId, string? from, string? to, bool? done, int take, CancellationToken ct)
    {
        var query = BaseQuery(take);
        if (userId is not null) query["userId"] = userId;
        if (from is not null) query["from"] = from;
        if (to is not null) query["to"] = to;
        if (done is not null) query["done"] = done.Value ? "true" : "false";

        var url = QueryHelpers.AddQueryString("/internal/todos", query);
        var env = await GetAsync<Envelope<TodoRow>>(url, ct);
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
