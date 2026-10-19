using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JSini.Shared.Infrastructure.HealthChecks;

/// <summary>
/// 모든 서비스가 같은 모양으로 <c>/health</c> 를 보고하게 하는 도우미. 딸린 것(LLM·DB·큐·저장소)을 항목별로 점검한다.
/// <c>Degraded</c> 는 프로세스는 살아 있지만 제 일을 못 하는 상태라 HTTP 200 을 유지한다 — 로드밸런서가 내리면 안 된다.
/// </summary>
public static class HealthCheckJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

/// <summary>의존 대상 점검에 붙이는 태그. 화면이 '딸린 것' 으로 따로 보여 주는 기준이다.</summary>
    public const string DependencyTag = "dependency";

/// <summary><c>/health</c> 를 JSON 으로 매핑한다. 인증은 걸지 않는다. Healthy·Degraded 는 200, Unhealthy 는 503.</summary>
    public static IEndpointConventionBuilder MapJsiniHealthChecks(
        this IEndpointRouteBuilder app, string pattern = "/health")
    {
        return app.MapHealthChecks(pattern, new HealthCheckOptions
        {
            ResponseWriter = WriteResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
        }).AllowAnonymous();
    }

/// <summary>점검 결과를 JSON 으로 쓴다. 본문을 읽는 쪽은 게이트웨이뿐이다.</summary>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = (int)e.Value.Duration.TotalMilliseconds,
                tags = e.Value.Tags,
                // 주소·모델명처럼 화면이 함께 보여 주면 좋은 값.
                // **비밀은 넣지 않는다** — 이 경로는 인증이 없다.
                data = e.Value.Data.Count > 0 ? e.Value.Data : null,
                error = e.Value.Exception?.Message,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
