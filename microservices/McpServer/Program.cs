using McpServer.Tools;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddHttpClient<WebSearchService>();
builder.Services.AddHttpClient<WebFetchService>();

// MCP 서버. 도구는 [McpServerToolType] 이 붙은 클래스를 훑어 찾는다 —
// 도구를 더할 때 여기를 고칠 일이 없다.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// MCP 규격 엔드포인트. 클라이언트(AIAgentServer)가 여기에 붙는다.
app.MapMcp("/mcp");

// 사람이 눈으로 확인하는 자리. MCP 클라이언트 없이도 살아 있는지 볼 수 있어야 한다.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/tools", (WebSearchService _, IConfiguration config) => Results.Ok(new
{
    provider = config["Search:Provider"] ?? "duckduckgo",
    tools = new[] { "web_search", "web_fetch" },
}));

app.Run();
