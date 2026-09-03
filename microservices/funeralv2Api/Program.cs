using Microsoft.EntityFrameworkCore;
using funeralv2Api.Data;
using Serilog;
using FluentValidation;
using System.Reflection;
using Microsoft.OpenApi.Models;

using funeralv2Api.Services;
using funeralv2Api.Endpoints;
using Spectre.Console;
using JSini.Shared.Infrastructure.Middleware;
using JSini.Shared.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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


var connectionString = builder.Configuration.GetConnectionString("funeralv2") 
                    ?? builder.Configuration["funeralv2"] 
                    ?? Environment.GetEnvironmentVariable("funeralv2");


Console.WriteLine($"aaaaaaaaaaaaaaaaaaaaaaaaaaa funeralv2api connectionString: {connectionString}");



builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "smfr")));

builder.Services.AddScoped<IDemoService, DemoService>();
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IFloorService, FloorService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IDeviceAttributeService, DeviceAttributeService>();
builder.Services.AddScoped<IDeviceConfigService, DeviceConfigService>();
builder.Services.AddScoped<IDeviceRibbonService, DeviceRibbonService>();
builder.Services.AddScoped<IDeviceTextOverlayService, DeviceTextOverlayService>();
builder.Services.AddScoped<IMediaSourceService, MediaSourceService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IDeceasedService, DeceasedService>();

// 옛 장례식장 시스템에서 이식한 화면들 (40번 문서)
builder.Services.AddScoped<IInfoService, InfoService>();
builder.Services.AddScoped<IStatService, StatService>();
builder.Services.AddScoped<IStatusService, StatusService>();
builder.Services.AddScoped<ISettingService, SettingService>();
builder.Services.AddScoped<IBuildingMusicService, BuildingMusicService>();
builder.Services.AddScoped<IDeviceHubSender, DeviceHubSender>();

// 장비 상태 자동 정리 백그라운드 서비스 등록
// last_seen_at 기준으로 응답 없는 ONLINE 장비를 주기적으로 OFFLINE 처리합니다.
builder.Services.AddHostedService<DeviceStatusCleanupService>();

builder.Services.AddSignalR();


// ============================================================
// 5. CORS 구성 (Cross-Origin Resource Sharing)
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
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

// [헬스체크]
// 게이트웨이의 능동 헬스체크와 오케스트레이터(K8s/로드밸런서)의 liveness 프로빙 대상.
// 인증 없이 접근 가능해야 하므로 별도 정책을 걸지 않는다.
builder.Services.AddHealthChecks();


// ── 딸린 것: DB ────────────────────────────────────────────
//
// 프로세스가 살아 있는 것과 서비스가 **제 일을 하는 것**은 다르다.
// DB 가 끊기면 이 서비스는 사실상 아무것도 못 하므로 Unhealthy(503) 로 본다
// (LLM 처럼 일부 기능만 막히는 경우는 Degraded 를 쓴다).
//
// 상태 화면이 '연결 대상' 줄로 보여 준다. 30초 캐시 · 3초 타임아웃은 도우미가 맡는다.
builder.Services.AddHealthChecks()
    .AddDependencyCheck("database", async (sp, ct) =>
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var canConnect = await db.Database.CanConnectAsync(ct);
        return canConnect
            ? HealthCheckResult.Healthy("DB 에 연결됩니다.")
            : HealthCheckResult.Unhealthy("DB 에 연결할 수 없습니다.");
    });

var app = builder.Build();
// 헬스체크 엔드포인트. 프로세스가 요청을 처리할 수 있는 상태인지만 보고한다.
app.MapJsiniHealthChecks();


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
app.MapPlayerUpdateEndpoints(); // 원격 업그레이드 지시 · 버전 조회 (D-P3 · D-P4)
app.MapDeviceAttributeEndpoints();
app.MapDeviceConfigEndpoints();
app.MapDeviceRibbonEndpoints();
app.MapDeviceTextOverlayEndpoints();
app.MapMediaSourceEndpoints();
app.MapDeceasedEndpoints();
app.MapInfoEndpoints();
app.MapStatEndpoints();
app.MapStatusEndpoints();
app.MapSettingEndpoints();
app.MapBuildingMusicEndpoints();

app.MapHub<funeralv2Api.Hubs.DeviceHub>("/hubs/device");







string GetServerName()
{
    return Environment.GetEnvironmentVariable("SERVER_NAME")
        ?? Assembly.GetEntryAssembly()?.GetName().Name
        ?? typeof(Program).Namespace
        ?? "API";
}

app.Lifetime.ApplicationStarted.Register(async () =>
{
    // ============================================================
    // 앱 재기동 시 잔류 ONLINE 장비 일괄 OFFLINE 초기화
    // 서버/장비가 갑작스럽게 종료된 경우 DB에 ONLINE이 잔류할 수 있으므로,
    // 재기동 직후 모든 ONLINE 장비를 OFFLINE으로 초기화합니다.
    // 장비들은 이후 SignalR을 통해 재접속 시 다시 ONLINE으로 갱신됩니다.
    // ============================================================
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<funeralv2Api.Data.AppDbContext>();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var onlineDevices = await db.Devices
            .Where(d => d.Status == "ONLINE" && !d.IsDeleted)
            .ToListAsync();

        if (onlineDevices.Count > 0)
        {
            foreach (var device in onlineDevices)
            {
                device.Status = "OFFLINE";
                device.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            startupLogger.LogWarning(
                "[Startup] 앱 재기동 감지: 잔류 ONLINE 장비 {Count}개를 OFFLINE으로 초기화함. " +
                "(장비 재접속 시 자동으로 ONLINE 전환됩니다)",
                onlineDevices.Count);
        }
        else
        {
            startupLogger.LogInformation("[Startup] 잔류 ONLINE 장비 없음. 초기화 불필요.");
        }
    }
    catch (Exception ex)
    {
        // 초기화 실패 시 서버 기동을 막지 않고 경고 로그만 남김
        var fallbackLogger = app.Services.GetRequiredService<ILogger<Program>>();
        fallbackLogger.LogError(ex, "[Startup] ONLINE 장비 초기화 중 오류 발생. 서버 기동은 계속 진행됩니다.");
    }

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

// (설정 반영을 위한 재기동 트리거)

