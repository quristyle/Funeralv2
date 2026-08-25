using AuthServer.Data;
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
var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? "a-very-secret-key-that-is-long-enough-for-security";
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

// 도움말 — F.A.Q(관리자가 쓰고 모두가 읽는다) · Q&A(누구나 묻고 관리자가 답한다)
builder.Services.AddScoped<IFaqService, FaqService>();
builder.Services.AddScoped<IQnaService, QnaService>();

// 접속 기록 — 계정 정보 화면의 '활동' 이 이 값을 읽는다
builder.Services.AddScoped<ILoginLogService, LoginLogService>();

// 배포(릴리즈) — 대상은 설정(Release:Targets)에서 읽는다.
builder.Services.Configure<AuthServer.DTOs.ReleaseOptions>(
    builder.Configuration.GetSection("Release"));
builder.Services.AddScoped<IReleaseService, ReleaseService>();



// [헬스체크]
// 게이트웨이의 능동 헬스체크와 오케스트레이터(K8s/로드밸런서)의 liveness 프로빙 대상.
// 인증 없이 접근 가능해야 하므로 별도 정책을 걸지 않는다.
builder.Services.AddHealthChecks();

var app = builder.Build();

// 요청 한 줄 로그. 다른 서비스와 같은 형식으로 남긴다.
app.UseSerilogRequestLogging();

// 헬스체크 엔드포인트. 프로세스가 요청을 처리할 수 있는 상태인지만 보고한다.
app.MapHealthChecks("/health").AllowAnonymous();


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
app.MapReleaseEndpoints();


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
