using AIAgentServer.Data;
using AIAgentServer.Endpoints;
using AIAgentServer.Services;
using Microsoft.EntityFrameworkCore;
using JSini.Shared.Infrastructure.HealthChecks;
using JSini.Shared.Infrastructure.Middleware;

using System.Reflection;
using Serilog;
using Spectre.Console;

var builder = WebApplication.CreateBuilder(args);

// 로컬 개별 설정을 위한 appsettings.Local.json 추가 (Git 제외)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ============================================================
// Serilog (로깅)
// ============================================================
// 다른 서비스와 로그 형식을 맞춘다. 장애를 쫓을 때 모양이 제각각이면 시간이 배로 든다.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// 헬스체크가 IHttpClientFactory 로 쓴다.
builder.Services.AddHttpClient();

// ── 사람별 AI 사용량 ────────────────────────────────────────
//
// **LLM 서버는 사람을 모른다.** 토큰 수는 응답에 실려 오지만 포털의 모든
// 요청이 API 키 하나로 들어가므로 그쪽에서는 전부 같은 손님이다. 사람을 아는
// 자리가 여기뿐이라(게이트웨이가 붙여 준 X-User-Id) 여기서 줍어 적는다.
//
// 연결 문자열이 없는 개발 장비에서는 **적는 일만 건너뛴다** — AI 기능이
// 통째로 멎으면 본말이 뒤집힌다(AiUsageLog 머리말).
var usageConnection = builder.Configuration.GetConnectionString("jsinicore")
                   ?? builder.Configuration["jsinicore"]
                   ?? Environment.GetEnvironmentVariable("jsinicore");

builder.Services.AddDbContext<AiUsageDbContext>(options => options.UseNpgsql(usageConnection));

// 적는 쪽이 지금 요청의 X-User-Id 를 읽는다.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AiUsageLog>();

// 「이 화면을 볼 수 있는 사람인가」를 **사이드바와 같은 표**에 묻는다.
// 역할 이름을 코드에 적으면 메뉴 권한을 넓혔을 때 조용히 어긋난다
// (MenuAccess 머리말 참고).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<MenuAccess>();

// AI쳇 대화 보관. 새로고침해도 남고 주제별로 나뉜다 — 여태는 회로가
// 사는 동안만 있었다(AiChatStore 머리말).
builder.Services.AddSingleton<AiChatStore>();

// 설정(AI:Providers)을 한 번 읽어 두고 계속 쓴다. 요청마다 다시 읽을 이유가 없다.
// 싱글턴이라 `appsettings` 를 고치면 **재기동해야 반영된다** — 이 저장소의 다른
// 설정들과 같은 규칙이다(CLAUDE.md: appsettings 변경만으로는 재기동하지 않는다).
builder.Services.AddSingleton<AiProviderRegistry>();

// OpenRouter 는 같은 API 로 유료 모델도 부를 수 있다. 사용자가 고른 모델 이름을
// 그대로 믿으면 과금 대상 호출이 되므로, 카탈로그를 받아 실제 가격이 0 인지 본다.
// 목록을 캐시하려면 인스턴스가 살아 있어야 해서 싱글턴이다.
builder.Services.AddSingleton<FreeModelGuard>();

//
// Anthropic Messages API 는 OpenAI 호환이 아니라 공식 SDK 로 부른다. 그 안에
// HttpClient 가 들어 있어서 요청마다 새로 만들면 소켓이 쌓인다 — 싱글턴으로 둔다.
builder.Services.AddSingleton<AnthropicTransport>();

//
// 무료 공급자가 전부 막혔을 때 한 줄 추천만 호스트의 AI 작업 실행기에 맡긴다.
// 부를 때마다 브로커 연결을 새로 여는 것이라 상태가 없지만, 설정을 한 번만 읽으려고 싱글턴이다.
builder.Services.AddSingleton<CliRelay>();

//
// 대기 시간을 두 가지로 나눠 잡는다. 하나로 두면 둘 중 하나가 반드시 망가진다.
//
//   · 접속(ConnectTimeout) — **짧게.** 장비가 꺼져 있을 때 빨리 포기하는 값이다.
//     이 설정이 없으면 운영체제 재시도에 맡겨져, 로컬 LLM 이 꺼져 있을 때
//     21초를 기다린 뒤 실패했다. 연결이 맺어진 뒤에는 적용되지 않으므로
//     짧게 잡아도 생성이 끊기지 않는다.
//
//   · 응답(공급자별 TimeoutSeconds) — **넉넉하게.** 로컬 LLM 은 모델을 메모리에
//     올리는 데만 수십 초가 걸린다. 요청마다 LLMService 가 직접 건다.
//
// 그래서 HttpClient 자체의 Timeout 은 끈다(무한). 공급자별로 달라야 하는데
// HttpClient 는 하나뿐이라 여기서 정할 수가 없다.
// RAG — 문서를 토막 내 벡터로 찾는다. 아직 대화에 끼워 넣지는 않는다(검색만).
builder.Services.AddHttpClient<AIAgentServer.Services.Rag.EmbeddingClient>();
builder.Services.AddSingleton<AIAgentServer.Services.Rag.RagStore>();
builder.Services.AddScoped<AIAgentServer.Services.Rag.RagIndexer>();
// RAG 문서의 실물을 맡는 곳. **올리기가 큰 파일이라 기본 100초로는 모자란다**
// — 색인할 때도 문서마다 한 번씩 받아 온다.
builder.Services.AddHttpClient<AIAgentServer.Services.Rag.FileServerClient>(c =>
    c.Timeout = TimeSpan.FromMinutes(10));

builder.Services.AddScoped<AIAgentServer.Services.Rag.RagLibrary>();
builder.Services.AddSingleton<AIAgentServer.Services.Rag.RagBackgroundIndexer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AIAgentServer.Services.Rag.RagBackgroundIndexer>());
builder.Services.AddScoped<AIAgentServer.Services.Rag.RagAugmenter>();

// MCP — 바깥 도구(웹 검색 등)를 모델이 쓰게 한다. 도구 목록은 코드가 아니라
// MCP 서버가 정한다. 붙지 못해도 서비스는 그냥 뜬다(도구 없이 답한다).
builder.Services.AddSingleton<AIAgentServer.Services.Mcp.McpToolGateway>();
builder.Services.AddHttpClient<AIAgentServer.Services.Mcp.WebChatAgent>();
builder.Services.AddScoped<AIAgentServer.Services.Mcp.WebAugmenter>();

// 업무 비서 — 포털 안의 자료(요청 글 · 일정 · 할 일 · 메일)를 도구로 읽는다. 내부 전용 길이라
// X-Internal-Token 으로만 자격을 증명하고(PortalToolRunner 가 호출마다 싣는다), 그 설정값이
// 비어 있으면 PortalToolSet 이 문서 검색 하나만 돌려준다 — 토큰 없는 개발 장비에서도 죽지 않는다.
builder.Services.AddScoped<AIAgentServer.Services.Portal.UserDirectory>();

builder.Services.AddHttpClient<AIAgentServer.Services.Portal.HelpDeskGateway>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(cfg["HelpDesk:Endpoint"] is { Length: > 0 } e ? e : "http://localhost:5400");
});
builder.Services.AddHttpClient<AIAgentServer.Services.Portal.ProjMngGateway>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(cfg["ProjMng:Endpoint"] is { Length: > 0 } e ? e : "http://localhost:5450");
});
builder.Services.AddHttpClient<AIAgentServer.Services.Portal.MailGateway>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(cfg["Notification:Endpoint"] is { Length: > 0 } e ? e : "http://localhost:5460");
});

builder.Services.AddScoped<AIAgentServer.Services.Portal.PortalToolRunner>();

// 업무 비서의 도구 호출 루프. 모델 호출은 jsini 공급자로 직접 가므로 ILLMService 와 같은
// 접속 시간 설정을 쓴다 — 장비가 꺼져 있을 때 빨리 포기해야 생성이 끊기지 않는다.
builder.Services.AddHttpClient<AIAgentServer.Services.Portal.AssistantAgent>()
    .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
    {
        ConnectTimeout = sp.GetRequiredService<AiProviderRegistry>().ConnectTimeout,
    })
    .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);

// LLM 장비 하드웨어 상태. 추론과 같은 주소·같은 키를 쓰므로 설정이 따로 없다.
builder.Services.AddHttpClient<LlmHostMetricsClient>();

// 말소리를 글로 (STT). 브라우저가 가진 받아쓰기를 안 쓰는 까닭은 SpeechClient
// 머리말에 있다 — 아이폰에 없고, 말소리가 바깥으로 나간다.
builder.Services.AddHttpClient<SpeechClient>();

// RAG·웹 검색이 실제로 도는지 눌러 보는 자리. **싱글턴이라야 한다** —
// 재어 둔 값(2분)을 들고 있어야 상태 화면의 10초 조회가 임베딩 서버의
// CPU 를 그때마다 물지 않는다.
// Gmail 보관함. 적재는 한 번에 하나만 돌므로 진행 상태는 싱글턴 하나에 담는다
// (메모리에만 — 다시 띄우면 지워지고, 적재는 다시 돌리면 된다).
builder.Services.AddSingleton<AIAgentServer.Services.Gmail.GmailImportStatus>();
builder.Services.AddScoped<AIAgentServer.Services.Gmail.GmailWriter>();
builder.Services.AddScoped<AIAgentServer.Services.Gmail.TakeoutImporter>();
builder.Services.AddScoped<AIAgentServer.Services.Gmail.GmailSearch>();
builder.Services.AddScoped<AIAgentServer.Services.Gmail.GmailRagArchiver>();
builder.Services.AddScoped<AIAgentServer.Services.Gmail.ImapCollector>();

// Gmail REST 는 느리게 답할 때가 있다(본문 한 통씩). 기본 100초로는 모자란다.
builder.Services.AddHttpClient<AIAgentServer.Services.Gmail.GmailApiCollector>(
    c => c.Timeout = TimeSpan.FromMinutes(5));

builder.Services.AddSingleton<AiFeatureProbe>();

builder.Services.AddHttpClient<ILLMService, LLMService>()
    .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
    {
        ConnectTimeout = sp.GetRequiredService<AiProviderRegistry>().ConnectTimeout,
    })
    .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);

// CORS 설정 (프론트엔드 직접 호출 허용을 위해)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 게이트웨이의 능동 헬스체크와 오케스트레이터(K8s/로드밸런서)의 liveness 프로빙 대상.
// 인증 없이 접근 가능해야 하므로 별도 정책을 걸지 않는다.
//
// **이 서비스는 LLM 장비 점검을 함께 보고한다.** 프로세스가 멀쩡해도 LLM 이 꺼져 있으면
// 아무 일도 못 하는데, 그 사실이 상태 화면까지 올라와야 한다.
builder.Services.AddHealthChecks()
    .AddCheck<LlmHealthCheck>(
        LlmHealthCheck.Name,
        tags: new[] { HealthCheckJson.DependencyTag });

var app = builder.Build();

// 색인은 기동 때 한 번 읽는다. 없으면 그냥 비어 있는 채로 뜬다 —
// 색인이 없다고 AI 서비스가 안 뜰 까닭은 없다.
_ = app.Services.GetRequiredService<AIAgentServer.Services.Rag.RagStore>()
    .LoadAllAsync(CancellationToken.None);

// 요청 한 줄 로그. 다른 서비스와 같은 형식으로 남긴다.
app.UseSerilogRequestLogging();

// 헬스체크 엔드포인트. 프로세스 상태와 **딸린 것(LLM)** 을 항목별로 보고한다.
app.MapJsiniHealthChecks();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}

app.UseGlobalExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.MapAIEndpoints();
app.MapRagLibraryEndpoints();
app.MapAssistantEndpoints();

// 사람별 AI 사용량(포털관리 「AI 사용량」 화면). **관리자만 본다** —
// 게이트웨이의 ai-route 가 이 묶음을 익명으로 열어 두므로 그쪽에서 한 번 더 본다.
app.MapAiUsageEndpoints();

// 내 AI쳇 대화. **권한을 따지지 않는다** — 쪽지처럼 누구나 제 것을 다루는
// 일이고, 모든 조회가 주인을 조건에 넣으므로 남의 대화는 잡히지 않는다.
app.MapAiChatSessionEndpoints();

// Gmail 보관함을 채우고 들여다보는 길. 적재는 서버 디스크의 mbox 를 읽으므로
// 설정(Gmail:ImportRoot)에 적힌 한 칸 안의 파일 이름만 받는다.
app.MapGmailEndpoints();

string GetServerName()
{
    return Environment.GetEnvironmentVariable("SERVER_NAME")
        ?? Assembly.GetEntryAssembly()?.GetName().Name
        ?? typeof(Program).Namespace
        ?? "API";
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    var serverName = GetServerName();
    var env = app.Environment.EnvironmentName;
    var pid = Environment.ProcessId;

    // 🔥 서버명 기반 색상 자동 매핑
    var color = serverName.ToUpper() switch
    {
        "GATEWAY" => Color.Cyan,
        "FUNERALV2" => Color.Green,
        "PAYMENT" => Color.Yellow,
        "AUTH" => Color.Magenta,
        "AI_AGENT" => Color.Aqua,
        _ => Color.White
    };

    // 🔥 환경 색상
    var envColor = env switch
    {
        "Development" => Color.Green,
        "Staging" => Color.Yellow,
        "Production" => Color.Red,
        _ => Color.Grey
    };

    // 🚀 Figlet 배너
    AnsiConsole.Write(
        new FigletText(serverName + "")
            .Color(color)
            .Centered());

    // 🌐 URL + PORT
    var urlLines = app.Urls.Select(url =>
    {
        try
        {
            var uri = new Uri(url);
            return $"[blue]🌐 {url}[/]  [grey](PORT: {uri.Port})[/]";
        }
        catch
        {
            return $"[blue]🌐 {url}[/]";
        }
    });

    // 📦 패널 내용
    var panelContent =
        $"[bold {color}]{serverName} SERVICE STARTED[/]\n" +
        $"[yellow]PID:[/] {pid}\n" +
        $"[bold {envColor}]ENV:[/] {env}\n\n" +
        string.Join("\n", urlLines);

    var panel = new Panel(panelContent)
        .Border(BoxBorder.Double)
        .BorderColor(color)
        .Padding(1, 1);

    AnsiConsole.Write(panel);

    // 하단 구분선
    AnsiConsole.Write(
        new Rule($"[bold {color}]READY[/]")
            .RuleStyle(color.ToString())
            .Centered());
});

app.Run();
