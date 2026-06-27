using Microsoft.EntityFrameworkCore;
using funeralv2Api.Data;
using Serilog;
using FluentValidation;
using System.Reflection;
using Microsoft.OpenApi.Models;

using funeralv2Api.Services;
using funeralv2Api.Endpoints;
using Spectre.Console;
using Funeralv2.Shared.Infrastructure.Middleware;

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
var connectionString = builder.Configuration["funeralv2"] ?? Environment.GetEnvironmentVariable("funeralv2");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "smfr")));

builder.Services.AddScoped<IDemoService, DemoService>();
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IFloorService, FloorService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IDeviceAttributeService, DeviceAttributeService>();
builder.Services.AddScoped<IDeviceConfigService, DeviceConfigService>();
builder.Services.AddScoped<IMediaSourceService, MediaSourceService>();


// ============================================================
// 5. CORS 구성 (Cross-Origin Resource Sharing)
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
// 6. FluentValidation 구성 (유효성 검사)
// ============================================================
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// ============================================================
// 7. Swagger/OpenAPI 구성 (API 문서화)
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Funeral V2 API",
        Version = "v1",
        Description = "장례 관리 시스템 V2 API"
    });
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

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
// 9. API 엔드포인트 등록 (분리된 파일에서 로드)
// ============================================================
app.MapExampleEndpoints();
app.MapBuildingEndpoints();
app.MapFloorEndpoints();
app.MapRoomEndpoints();
app.MapDeviceEndpoints();
app.MapDeviceAttributeEndpoints();
app.MapDeviceConfigEndpoints();
app.MapMediaSourceEndpoints();







string GetServerName()
{
    return Environment.GetEnvironmentVariable("SERVER_NAME")
        ?? Assembly.GetEntryAssembly()?.GetName().Name
        ?? typeof(Program).Namespace
        ?? "API";
}

app.Lifetime.ApplicationStarted.Register(() =>
{var serverName = GetServerName();
    var env = app.Environment.EnvironmentName;
    var pid = Environment.ProcessId;

    // 🔥 서버명 기반 색상 자동 매핑
    var color = serverName.ToUpper() switch
    {
        "GATEWAY" => Color.Cyan,
        "FUNERALV2" => Color.Green,
        "PAYMENT" => Color.Yellow,
        "AUTH" => Color.Magenta,
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
    Log.Information("Starting web host");
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
