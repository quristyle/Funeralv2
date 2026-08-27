using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JSini.Shared.Infrastructure.HealthChecks;

/// <summary>
/// 모든 서비스가 같은 모양으로 <c>/health</c> 를 보고하게 하는 도우미.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 필요한가.</b> 예전에는 <c>/health</c> 가 "이 프로세스가 응답한다" 만 알려 주었고
/// 게이트웨이는 <b>HTTP 상태 코드만</b> 보고 UP/DOWN 을 정했다. 그래서 AIAgentServer 는
/// LLM 장비가 꺼져 있어도 초록으로 보였다 — 프로세스는 멀쩡하니까.
/// 서비스가 <b>제 일을 할 수 있는지</b>는 딸린 것(LLM · DB · 큐 · 저장소)에 달려 있는데
/// 그 사실이 화면까지 올라오지 않았다.
/// </para>
///
/// <para>
/// 그래서 각 서비스가 자기 의존 대상을 스스로 점검해 <b>항목별로</b> 보고한다.
/// 게이트웨이는 판정하지 않고 이 결과를 그대로 올려 준다 —
/// LLM 주소·모델명·접속 문자열을 게이트웨이가 알아야 할 이유가 없다.
/// </para>
///
/// <para>
/// <b>상태 세 가지의 뜻.</b>
/// </para>
/// <list type="bullet">
///   <item><c>Healthy</c> — 서비스와 딸린 것 모두 정상.</item>
///   <item>
///     <c>Degraded</c> — <b>프로세스는 살아 있지만 제 일을 못 한다.</b>
///     LLM 이 꺼진 AIAgentServer 가 여기다. HTTP 는 200 을 유지한다 —
///     서비스가 죽은 것이 아니므로 로드밸런서가 내려서는 안 된다.
///     대신 본문에 이유가 담기고, 화면이 '주의' 로 보여 준다.
///   </item>
///   <item><c>Unhealthy</c> — 서비스 자체가 요청을 처리할 수 없다. 503 을 준다.</item>
/// </list>
/// </remarks>
public static class HealthCheckJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// 의존 대상 점검에 붙이는 태그.
    /// </summary>
    /// <remarks>
    /// 이 태그가 붙은 항목만 화면이 '딸린 것' 으로 따로 보여 준다.
    /// 서비스 내부 점검(예: 자기 메모리)과 구분하기 위한 것이다.
    /// </remarks>
    public const string DependencyTag = "dependency";

    /// <summary>
    /// <c>/health</c> 를 JSON 으로 매핑한다. 인증은 걸지 않는다.
    /// </summary>
    /// <remarks>
    /// 오케스트레이터(K8s·로드밸런서)와 게이트웨이가 모두 이 경로를 찌른다.
    /// 상태 코드 규칙은 기본값을 그대로 쓴다 — Healthy·Degraded 는 200, Unhealthy 는 503.
    /// </remarks>
    public static IEndpointConventionBuilder MapJsiniHealthChecks(
        this IEndpointRouteBuilder app, string pattern = "/health")
    {
        return app.MapHealthChecks(pattern, new HealthCheckOptions
        {
            ResponseWriter = WriteResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                // 딸린 것이 죽었다고 서비스를 내리지는 않는다.
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
        }).AllowAnonymous();
    }

    /// <summary>
    /// 점검 결과를 JSON 으로 쓴다.
    /// </summary>
    /// <remarks>
    /// 예전 형식(본문이 <c>Healthy</c> 라는 글자 하나)을 읽던 곳이 있어도 깨지지 않는다 —
    /// 그쪽은 상태 코드만 보기 때문이다. 새 형식은 본문을 읽는 쪽(게이트웨이)만 쓴다.
    /// </remarks>
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
                // 왜 이 상태인지. 화면이 그대로 보여 주므로 사람이 읽을 문장으로 쓴다.
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
