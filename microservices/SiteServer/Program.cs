using System.Reflection;
using JSini.Shared.Infrastructure.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using SiteServer.Data;
using SiteServer.Endpoints;
using SiteServer.Services;
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
// 포털과 같은 funeralv2 인스턴스를 쓰고 스키마만 site 로 나눈다.
// **스키마는 이 코드가 만들지 않는다** — docs/sql/site_schema.sql 이 만든다.
// FileServer 만 Database.Migrate() 를 쓰는데 .gitignore 가 Migrations/ 를 제외하고 있어
// 그 방식은 다른 장비로 가지 않는다. 나머지 서비스와 같은 방식을 따른다.
var connectionString = builder.Configuration.GetConnectionString("jsinisiteconn")
    ?? builder.Configuration["jsinisiteconn"]
    ?? Environment.GetEnvironmentVariable("jsinisiteconn");

builder.Services.AddDbContext<SiteDbContext>(options => options.UseNpgsql(connectionString));

// ============================================================
// 3. 서비스
// ============================================================
builder.Services.AddScoped<ISiteService, SiteService>();

// ============================================================
// 4. CORS
// ============================================================
// 소개 사이트는 게이트웨이와 다른 오리진(www.jsini.co.kr)에서 온다.
// 허용할 오리진은 설정으로 받는다 — 코드에 도메인을 박으면 배포마다 고쳐야 한다.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5556"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("PublicSite", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
        // AllowCredentials 를 켜지 않는다. 공개 사이트는 쿠키를 쓸 일이 없고,
        // 켜면 오리진 검사가 조금 어긋나도 자격 증명이 함께 나간다.
    });
});

// ============================================================
// 5. Swagger
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JSINI Site API",
        Version = "v1",
        Description = "회사 소개 사이트(www.jsini.co.kr) 의 공개 조회 · 문의 접수 · 관리",
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health").AllowAnonymous();

// ============================================================
// 6. 파이프라인
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors("PublicSite");

// ============================================================
// 7. 엔드포인트
// ============================================================
app.MapSiteEndpoints();

string GetServerName() =>
    Environment.GetEnvironmentVariable("SERVER_NAME")
    ?? Assembly.GetEntryAssembly()?.GetName().Name
    ?? "SITE_API";

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
    Log.Information("Starting SiteServer web host");
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
