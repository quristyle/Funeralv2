using System.Reflection;
using LifeEnvServer.Data;
using LifeEnvServer.Endpoints;
using LifeEnvServer.Services;
using JSini.Shared.Infrastructure.HealthChecks;
using JSini.Shared.Infrastructure.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;
using Spectre.Console;

var builder = WebApplication.CreateBuilder(args);

// 로컬 개별 설정 (Git 제외)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ============================================================
// 1. 로깅
// ============================================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// ============================================================
// 2. 데이터베이스
// ============================================================
// DB 는 ghub(서비스 전용), 스키마도 ghub 다.
// **스키마는 이 코드가 만들지 않는다** — docs/sql/ghub_schema.sql 이 만든다.
var connectionString = builder.Configuration.GetConnectionString("jsinilifeenvconn")
    ?? builder.Configuration["jsinilifeenvconn"]
    ?? Environment.GetEnvironmentVariable("jsinilifeenvconn");

builder.Services.AddDbContext<LifeEnvDbContext>(options => options.UseNpgsql(connectionString));

// ============================================================
// 3. 서비스
// ============================================================
// 기상청 공공데이터 API 클라이언트. 인증키는 Weather:ServiceKey (Local 설정).
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<WeatherApiService>(client =>
{
    // 공공데이터포털이 느릴 때 30분 수집 사이클이 통째로 밀리지 않게 상한을 둔다
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<IWeatherMonitoringService, WeatherMonitoringService>();
// 기상 이벤트를 NotificationServer 로 넘긴다 (D-G1a). 주소는 Notify:BaseUrl (기본 :5460).
builder.Services.AddHttpClient<WeatherNotifyClient>();

// 30분 주기 수집(실황 · 특보 · 중기 · 초단기 · 단기). 키가 없으면 로그만 남기고 쉰다.
builder.Services.AddHostedService<WeatherCollectionService>();

// ============================================================
// 4. Swagger
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JSINI LifeEnv API",
        Version = "v1",
        Description = "생활과환경 — 기상(기상청 연동). GHUB(SK가스 지허브)에서 이식. "
                      + "생일은 포털(AuthServer /birthday/*)로 옮겨졌다.",
    });
});

builder.Services.AddHttpContextAccessor();

// EF 네비게이션이 서로를 물고 있어(통보문 ↔ 문장) 순환 참조가 생긴다.
// 원본(GHUB)과 같게 순환을 끊고, enum 은 이름 문자열로 내보낸다.
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler =
        System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// ── 딸린 것: DB ────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDependencyCheck("database", async (sp, ct) =>
    {
        var db = sp.GetRequiredService<LifeEnvDbContext>();
        var canConnect = await db.Database.CanConnectAsync(ct);
        return canConnect
            ? HealthCheckResult.Healthy("DB 에 연결됩니다.")
            : HealthCheckResult.Unhealthy("DB 에 연결할 수 없습니다.");
    });

var app = builder.Build();

app.MapJsiniHealthChecks();

// ============================================================
// 5. 파이프라인
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();
app.UseSerilogRequestLogging();

// ============================================================
// 6. 엔드포인트
// ============================================================
// 게이트웨이가 /api/ghub 접두사를 떼고 넘겨 주므로 여기서는 루트 기준이다.
// 인증은 게이트웨이가 끝냈고(X-User-* 헤더), 외부에서 직접 못 붙게
// Kestrel 이 루프백(127.0.0.1)에만 묶여 있다 — appsettings.json 참조.
app.MapWeatherEndpoints();
app.MapWeatherStandardEndpoints();
app.MapWeatherResponseEndpoints();
app.MapWeatherEventEndpoints();

string GetServerName() =>
    Environment.GetEnvironmentVariable("SERVER_NAME")
    ?? Assembly.GetEntryAssembly()?.GetName().Name
    ?? "LIFEENV_API";

app.Lifetime.ApplicationStarted.Register(() =>
{
    var serverName = GetServerName();
    var env = app.Environment.EnvironmentName;
    var color = Color.Grey70;

    var envColor = env switch
    {
        "Development" => Color.Green,
        "Staging" => Color.Yellow,
        "Production" => Color.Red,
        _ => Color.Grey,
    };

    AnsiConsole.Write(new FigletText(serverName).Color(color).Centered());

    var urlLines = app.Urls.Select(url =>
    {
        try
        {
            var uri = new Uri(url);
            return $"[grey]🌐 {Markup.Escape(url)}[/]  [grey](PORT: {uri.Port})[/]";
        }
        catch
        {
            return $"[grey]🌐 {Markup.Escape(url)}[/]";
        }
    });

    AnsiConsole.Write(new Panel(
            $"[bold {color}]{serverName} SERVICE STARTED[/]\n" +
            $"[yellow]PID:[/] {Environment.ProcessId}\n" +
            $"[bold {envColor}]ENV:[/] {env}\n\n" +
            string.Join("\n", urlLines))
        .Border(BoxBorder.Double)
        .BorderColor(color)
        .Padding(1, 1));

    AnsiConsole.Write(new Rule($"[bold {color}]READY[/]").RuleStyle(color.ToString()).Centered());
});

try
{
    Log.Information("Starting LifeEnvServer web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
