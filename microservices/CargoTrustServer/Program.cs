using System.Reflection;
using CargoTrustServer.Admin;
using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Companies;
using CargoTrustServer.Data;
using CargoTrustServer.Disputes;
using CargoTrustServer.Endpoints;
using CargoTrustServer.Payments;
using CargoTrustServer.Reports;
using CargoTrustServer.Reviews;
using CargoTrustServer.Statistics;
using CargoTrustServer.Transactions;
using CargoTrustServer.Users;
using JSini.Shared.Infrastructure.Filters;
using JSini.Shared.Infrastructure.HealthChecks;
using JSini.Shared.Infrastructure.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
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
// DB 는 cargotrust(서비스 전용), 스키마도 cargotrust 다.
// **스키마는 이 코드가 만들지 않는다** — deploy/sql/cargotrust-schema-2026-09-24.sql 이 만든다.
var connectionString = builder.Configuration.GetConnectionString("cargotrust")
    ?? builder.Configuration["cargotrust"]
    ?? Environment.GetEnvironmentVariable("cargotrust");

builder.Services.AddDbContext<CargoTrustDbContext>(options => options.UseNpgsql(connectionString));

// ============================================================
// 3. 서비스
// ============================================================
builder.Services.Configure<CargoTrustOptions>(builder.Configuration.GetSection(CargoTrustOptions.Section));

// 요청마다 한 사람 — CargoUserFilter 가 채우고 핸들러가 받는다.
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<CargoUserService>();
builder.Services.AddScoped<AuditService>();
// 통계 계산은 여기 한 곳 — 사용자·관리자 화면이 같은 식을 쓴다.
builder.Services.AddScoped<CompanyStatsService>();

// ============================================================
// 4. Swagger
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JSINI CargoTrust API",
        Version = "v1",
        Description = "JSini 운송관리 — 화물 거래처 신뢰정보. 계약: docs/cargotrust/05-api-design.md",
    });
});

builder.Services.AddHttpContextAccessor();

// enum 은 이름 문자열(대문자 코드값 그대로)로 내보낸다. 날짜(DateOnly)는 기본이 "yyyy-MM-dd" 다.
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
        var db = sp.GetRequiredService<CargoTrustDbContext>();
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
// 게이트웨이가 /api/cargotrust 접두사를 떼고 넘겨 주므로 여기서는 루트 기준이다.
// 인증은 게이트웨이가 끝냈고(X-User-* 헤더), 외부에서 직접 못 붙게
// Kestrel 이 루프백(127.0.0.1)에만 묶여 있다 — appsettings.json 참조.
//
// 모든 업무 경로가 한 그룹 아래에 선다. 신원(401) · 사용자 줄 · 차단(403)을 그룹 필터
// 하나가 맡아서, 새 경로를 더해도 그 검사를 빠뜨릴 수 없다.
var api = app.MapGroup("/")
    .AddEndpointFilter<CargoUserFilter>()
    .AddApiResponseWrapper();

api.MapMeEndpoints();
api.MapCompanyEndpoints();
api.MapTransactionEndpoints();
api.MapPaymentEndpoints();
api.MapReviewEndpoints();
api.MapReportEndpoints();
api.MapDisputeEndpoints();
api.MapAdminEndpoints();

string GetServerName() =>
    Environment.GetEnvironmentVariable("SERVER_NAME")
    ?? Assembly.GetEntryAssembly()?.GetName().Name
    ?? "CARGOTRUST_API";

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
    Log.Information("Starting CargoTrustServer web host");
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
