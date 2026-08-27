using AIAgentServer.Endpoints;
using AIAgentServer.Services;
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

// [AI 공급자 목록]
// 설정(AI:Providers)을 한 번 읽어 두고 계속 쓴다. 요청마다 다시 읽을 이유가 없다.
// 싱글턴이라 `appsettings` 를 고치면 **재기동해야 반영된다** — 이 저장소의 다른
// 설정들과 같은 규칙이다(CLAUDE.md: appsettings 변경만으로는 재기동하지 않는다).
builder.Services.AddSingleton<AiProviderRegistry>();

// [무료 모델 확인]
// OpenRouter 는 같은 API 로 유료 모델도 부를 수 있다. 사용자가 고른 모델 이름을
// 그대로 믿으면 과금 대상 호출이 되므로, 카탈로그를 받아 실제 가격이 0 인지 본다.
// 목록을 캐시하려면 인스턴스가 살아 있어야 해서 싱글턴이다.
builder.Services.AddSingleton<FreeModelGuard>();

// [AI 호출용 HttpClient]
//
// 대기 시간을 두 가지로 나눠 잡는다. 하나로 두면 둘 중 하나가 반드시 망가진다.
//
//   · 접속(ConnectTimeout) — **짧게.** 장비가 꺼져 있을 때 빨리 포기하는 값이다.
//     예전에는 이 설정이 없어 운영체제 재시도에 맡겨졌고, 로컬 LLM 이 꺼져 있으면
//     21초를 기다린 뒤 실패했다. 연결이 맺어진 뒤에는 적용되지 않으므로
//     짧게 잡아도 생성이 끊기지 않는다.
//
//   · 응답(공급자별 TimeoutSeconds) — **넉넉하게.** 로컬 LLM 은 모델을 메모리에
//     올리는 데만 수십 초가 걸린다. 요청마다 LLMService 가 직접 건다.
//
// 그래서 HttpClient 자체의 Timeout 은 끈다(무한). 공급자별로 달라야 하는데
// HttpClient 는 하나뿐이라 여기서 정할 수가 없다.
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

// [헬스체크]
// 게이트웨이의 능동 헬스체크와 오케스트레이터(K8s/로드밸런서)의 liveness 프로빙 대상.
// 인증 없이 접근 가능해야 하므로 별도 정책을 걸지 않는다.
//
// **이 서비스는 LLM 장비 점검을 함께 보고한다.** 프로세스가 멀쩡해도 LLM 이 꺼져 있으면
// 아무 일도 못 하는데, 예전에는 그 사실이 상태 화면까지 올라오지 않아 초록으로 보였다.
builder.Services.AddHealthChecks()
    .AddCheck<LlmHealthCheck>(
        LlmHealthCheck.Name,
        tags: new[] { HealthCheckJson.DependencyTag });

var app = builder.Build();

// 요청 한 줄 로그. 다른 서비스와 같은 형식으로 남긴다.
app.UseSerilogRequestLogging();

// 헬스체크 엔드포인트. 프로세스 상태와 **딸린 것(LLM)** 을 항목별로 보고한다.
app.MapJsiniHealthChecks();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.MapAIEndpoints();

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
