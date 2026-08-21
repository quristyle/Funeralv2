using Microsoft.EntityFrameworkCore;
using FileServer.Data;
using Serilog;
using FluentValidation;
using System.Reflection;
using Microsoft.OpenApi.Models;
using FileServer.Services;
using FileServer.Endpoints;
using Spectre.Console;
using JSini.Shared.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Kestrel 요청 본문 크기 제한 해제 (예: 500MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024; // 500MB
});

// Multipart Form 제한 해제
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024; // 500MB
    options.ValueLengthLimit = 500 * 1024 * 1024;
});


// 로컬 개별 설정을 위한 appsettings.Local.json 추가 (Git 제외)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ============================================================
// 1. Serilog 구성 (로깅)
// ============================================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// ============================================================
// 2. 데이터베이스 구성 (PostgreSQL & EF Core)
// ============================================================
var connectionString = builder.Configuration.GetConnectionString("jsinifileconn") 
    ?? builder.Configuration["jsinifileconn"] 
    ?? Environment.GetEnvironmentVariable("jsinifileconn");
builder.Services.AddDbContext<FileDbContext>(options =>
    options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "scom")));

// ============================================================
// 3. 비즈니스 서비스 의존성 주입
// ============================================================
builder.Services.AddScoped<IFileService, FileService>();

// ============================================================
// 4. CORS 구성
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ============================================================
// 5. FluentValidation 구성
// ============================================================
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// ============================================================
// 6. Swagger/OpenAPI 구성 (API 문서화)
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Funeral V2 File API",
        Version = "v1",
        Description = "장례 관리 시스템 V2 파일 마이크로서비스 API"
    });
});

builder.Services.AddHttpContextAccessor();

// [헬스체크]
// 게이트웨이의 능동 헬스체크와 오케스트레이터(K8s/로드밸런서)의 liveness 프로빙 대상.
// 인증 없이 접근 가능해야 하므로 별도 정책을 걸지 않는다.
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

var app = builder.Build();
// 헬스체크 엔드포인트. 프로세스가 요청을 처리할 수 있는 상태인지만 보고한다.
app.MapHealthChecks("/health").AllowAnonymous();


// ============================================================
// 7. 데이터베이스 초기화 (테이블 자동 생성)
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<FileDbContext>();
        // 데이터베이스가 존재하고 마이그레이션이 적용되지 않은 경우 자동 적용함
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "데이터베이스 초기화(Migrate) 중 오류 발생");
    }
}

// ============================================================
// 8. HTTP 요청 파이프라인 구성 (Middleware)
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("AllowAll");

// ============================================================
// 9. API 엔드포인트 등록
// ============================================================
app.MapFileEndpoints();

string GetServerName()
{
    return Environment.GetEnvironmentVariable("SERVER_NAME")
        ?? Assembly.GetEntryAssembly()?.GetName().Name
        ?? typeof(Program).Namespace
        ?? "FILE_API";
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    var serverName = GetServerName();
    var env = app.Environment.EnvironmentName;
    var pid = Environment.ProcessId;

    // 🔥 서버명 기반 색상 자동 매핑 (파일 서비스는 파란색으로 지정)
    var color = Color.Blue;

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
        new FigletText(serverName)
            .Color(color)
            .Centered());

    // 🌐 URL + PORT
    var urlLines = app.Urls.Select(url =>
    {
        try
        {
            var uri = new Uri(url);
            return $"[blue]🌐 {Markup.Escape(url)}[/]  [grey](PORT: {uri.Port})[/]";
        }
        catch
        {
            return $"[blue]🌐 {Markup.Escape(url)}[/]";
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

try
{
    Log.Information("Starting FileServer web host");
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
