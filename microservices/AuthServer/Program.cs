﻿using AuthServer.Data;
using AuthServer.Endpoints;
using AuthServer.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AuthServer.Services;
using System.Text;
using Spectre.Console;
using System.Reflection;
using JSini.Shared.Infrastructure.Middleware;
using Serilog;
using JSini.Shared.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);



// 로컬 개별 설정을 위한 appsettings.Local.json 추가 (Git 제외)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ============================================================
// Serilog (로깅)
// ============================================================
// 다른 서비스(funeralv2Api·FileServer·HelpDeskServer)와 로그 형식을 맞춘다.
// 장애를 쫓을 때 서비스마다 로그 모양이 다르면 시간이 배로 든다.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();



// 1. 데이터베이스 구성 (PostgreSQL & EF Core)
var connectionString = builder.Configuration.GetConnectionString("jsinicore") 
                    ?? builder.Configuration["jsinicore"] 
                    ?? Environment.GetEnvironmentVariable("jsinicore");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "scom")));

// 2. JWT 인증 구성 (AuthServer 자체 엔드포인트 보호용)
// 키는 appsettings.Local.json (git 제외) 에만 있다. 없으면 기동에 실패한다 (결정 D1-B).
// 예전에는 여기에 평문 키가 폴백으로 박혀 있어, 설정이 비어도 조용히 그 값으로 돌았다.
var jwtKey = JSini.Shared.Infrastructure.JwtKeyGuard.Require(
    builder.Configuration, "JwtSettings:SecretKey", "AuthServer");
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = "funeralv2-auth",
            ValidAudience = "funeralv2-services",
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();


// ============================================================
// 3. 애플리케이션 비즈니스 서비스 등록 (의존성 주입)
// ============================================================
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITimezoneService, TimezoneService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IMenuFavoriteService, MenuFavoriteService>();
builder.Services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<ISystemMenuService, SystemMenuService>();
builder.Services.AddScoped<II18nResourceService, I18nResourceService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<ICommonCodeService, CommonCodeService>();
builder.Services.AddScoped<IBizSelectConfigService, BizSelectConfigService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<INoticeService, NoticeService>();
// 공지를 공개로 두면 첨부도 공개로 본다 (결정 D-S10). NoticeService 가 저장할 때마다 부른다.
builder.Services.AddScoped<IPublicFileSyncService, PublicFileSyncService>();

// 도움말 — F.A.Q(관리자가 쓰고 모두가 읽는다) · Q&A(누구나 묻고 관리자가 답한다)
builder.Services.AddScoped<IFaqService, FaqService>();
builder.Services.AddScoped<IQnaService, QnaService>();
// 자료실 — 관리자가 올리고 모두가 설명을 읽고 내려받는다(F.A.Q 와 같은 권한 방식).
builder.Services.AddScoped<IHelpArchiveService, HelpArchiveService>();
// 메뉴 기준 권한 현황(/auth/menu-role). 읽기만 한다 — 저장은 role-permission · role-scope 를 쓴다.
builder.Services.AddScoped<IMenuRoleService, MenuRoleService>();

// 접속 기록 — 계정 정보 화면의 '활동' 이 이 값을 읽는다
builder.Services.AddScoped<ILoginLogService, LoginLogService>();

// 화면 환경설정 — 계정에 붙여 두어 어느 PC 에서 로그인해도 따라오게 한다.
// 헤더 톱니의 드로어와 /setting/environment 가 같은 값을 읽고 쓴다.
builder.Services.AddScoped<IAccountPreferenceService, AccountPreferenceService>();

// 배포(릴리즈) — 대상은 설정(Release:Targets)에서 읽는다.
//
// 요청 한 건이 scom.release_runs 행 하나가 된다. 배포 장비의 래퍼가 그 run id 로
// 진행 상황을 되돌려 보고하고, 화면은 그 id 를 폴링한다.
// 배포가 끝나면 대상의 VersionUrl 을 읽어 실제로 반영됐는지 확인하므로 HttpClient 가 필요하다.
builder.Services.Configure<AuthServer.DTOs.ReleaseOptions>(
    builder.Configuration.GetSection("Release"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IReleaseService, ReleaseService>();

// 플레이어 릴리스 — GitHub 에 버전 태그를 만들어 릴리스 워크플로를 깨운다.
// 토큰은 appsettings.Local.json(git 제외)에만 둔다. 값이 없으면 화면이 안내만 띄우고
// 서버는 정상 기동한다 — 이 기능을 안 쓰는 환경에서도 떠야 하기 때문이다.
// 생일 축하 푸시 (D-G1a) — NotificationServer 의 기존 /notifications/push 를 부른다.
builder.Services.AddHttpClient<AuthServer.Services.BirthdayNotifyClient>();

builder.Services.Configure<AuthServer.DTOs.GitHubOptions>(
    builder.Configuration.GetSection("GitHub"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IPlayerReleaseService, PlayerReleaseService>();



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

// 요청 한 줄 로그. 다른 서비스와 같은 형식으로 남긴다.
app.UseSerilogRequestLogging();

// 헬스체크 엔드포인트. 프로세스가 요청을 처리할 수 있는 상태인지만 보고한다.
app.MapJsiniHealthChecks();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// 엔드포인트 등록
app.MapAuthEndpoints();


app.MapUserEndpoints();
app.MapMenuEndpoints();
app.MapMenuFavoriteEndpoints();
app.MapRoleScopeEndpoints();
app.MapTimezoneEndpoints();
app.MapSystemEndpoints();
app.MapCompanyEndpoints();
app.MapCommonCodeEndpoints();
app.MapRolePermissionEndpoints();
app.MapNoticeEndpoints();
app.MapFaqEndpoints();
app.MapQnaEndpoints();
app.MapHelpArchiveEndpoints();
app.MapMenuRoleEndpoints();
app.MapReleaseEndpoints();
app.MapPlayerReleaseEndpoints();
app.MapDeployStatusEndpoints();
// 생일 — 정본은 계정(scom.accounts)이고 여기서는 조회 · 축하 메시지만 낸다 (A안).
app.MapBirthdayEndpoints();


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
