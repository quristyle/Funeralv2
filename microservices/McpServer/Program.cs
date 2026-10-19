using JSini.Shared.Infrastructure.HealthChecks;
using McpServer.Tools;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 장비마다 다른 설정(검색 뒷단·열쇠). 운영에서는 컨테이너에 읽기 전용으로
// 물려 준다 — 이 줄이 없으면 **그 파일이 조용히 안 읽힌다**(compose 의 mcp 볼륨).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddHttpClient<WebSearchService>();
builder.Services.AddHttpClient<WebFetchService>();

// MCP 서버. 도구는 [McpServerToolType] 이 붙은 클래스를 훑어 찾는다 —
// 도구를 더할 때 여기를 고칠 일이 없다.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// ── /health ────────────────────────────────────────────────
// 다른 서비스와 같은 모양으로 보고한다. 딸린 것은 검색 뒷단 하나다 — 나가는 길이 막히면 프로세스는 멀쩡해도 도구를 못 쓰므로 따로 본다.
// 캐시 10분은 화면이 10초마다 묻는데 그 길이 바깥 요청이라서다.
builder.Services.AddHealthChecks()
    .AddDependencyCheck("search", async (sp, ct) =>
    {
        var search = sp.GetRequiredService<WebSearchService>();
        var probe = await search.ProbeAsync(ct);

        // 주소와 뒷단 이름은 화면이 함께 보여 준다. **열쇠는 담지 않는다** —
        // 이 경로에는 인증이 없다.
        var data = new Dictionary<string, object>
        {
            ["provider"] = probe.Provider,
            ["endpoint"] = probe.Endpoint,
        };

        // 검색이 막혀도 Unhealthy 가 아니다 — 본문 가져오기는 그대로 되므로, 503 을 주면 멀쩡한 서비스를 내리게 된다.
        return probe.Ok
            ? HealthCheckResult.Healthy(probe.Reason ?? "검색 뒷단에 닿습니다.", data)
            : HealthCheckResult.Degraded($"검색 뒷단에 닿지 못했습니다. {probe.Reason}".TrimEnd(), data: data);
    }, timeoutSeconds: 5, cacheSeconds: 600);

var app = builder.Build();

// MCP 규격 엔드포인트. 클라이언트(AIAgentServer)가 여기에 붙는다.
app.MapMcp("/mcp");

// 사람이 눈으로 확인하는 자리. MCP 클라이언트 없이도 살아 있는지 볼 수 있어야 한다.
app.MapJsiniHealthChecks();

app.MapGet("/tools", (WebSearchService _, IConfiguration config) => Results.Ok(new
{
    provider = config["Search:Provider"] ?? "duckduckgo",
    tools = new[] { "web_search", "web_fetch" },
}));

app.Run();
