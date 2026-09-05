using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using HelpDeskServer.Data;
using HelpDeskServer.Endpoints;
using RabbitMQ.Client.Exceptions;
using HelpDeskServer.Options;
using Microsoft.Extensions.Options;
using HelpDeskServer.Services;
using JSini.Shared.Infrastructure.Middleware;
using Serilog;
using Spectre.Console;
using JSini.Shared.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;


// 전역 시간대 설정 (KST)
Environment.SetEnvironmentVariable("TZ", "Asia/Seoul");

var builder = WebApplication.CreateBuilder(args);

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
// 접속 문자열은 Git 에 올리지 않는다. appsettings.Local.json 또는 환경변수로 주입한다.
// (Help_JSINI 는 이관 전 JinRestApi 가 쓰던 환경변수명이라 호환을 위해 남겨둔다)
var connectionString = builder.Configuration.GetConnectionString("helpdesk")
                       ?? builder.Configuration["helpdesk"]
                       ?? Environment.GetEnvironmentVariable("helpdesk")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("Help_JSINI");

if (string.IsNullOrWhiteSpace(connectionString)) {
  throw new InvalidOperationException(
      "HelpDesk DB 접속 문자열이 없습니다. appsettings.Local.json 의 ConnectionStrings:helpdesk 또는 환경변수 helpdesk 를 설정하세요.");
}

// 스키마 이름. 헬프데스크 전용 DB 로 옮기면서 스키마가 `jsini` 에서 `helpdesk` 로
// 바뀌었는데 EF 모델에는 박혀 있었다 — 모든 요청이 500 이었다.
// 기본값은 옛 이름 그대로라, 설정을 안 넣은 곳은 지금과 똑같이 동작한다.
AppDbContext.Schema = builder.Configuration["Database:Schema"] ?? "jsini";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString,
    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema)));

// ============================================================
// 3. RabbitMQ (없어도 서비스는 계속 동작한다)
// ============================================================
IConnection? connection = null;
try {
  var rabbitMqHostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";

  IConnectionFactory factory = new ConnectionFactory() {
    HostName = rabbitMqHostName
  };

  connection = await factory.CreateConnectionAsync();
}
catch (BrokerUnreachableException ex) {
  Console.WriteLine($"[ERROR] RabbitMQ connection failed: {ex.Message}. RabbitMQ 없이 서비스가 계속 동작합니다.");
}
catch (Exception ex) {
  Console.WriteLine($"[ERROR] RabbitMQ 초기화 중 알 수 없는 오류: {ex.Message}");
}

// 반드시 DI에 등록 (null일 수도 있음)
builder.Services.AddSingleton<IRabbitMqConnectionProvider>(
    new RabbitMqConnectionProvider(connection)
);

// IHttpContextAccessor를 등록하여 서비스 내에서 HttpContext에 접근할 수 있도록 합니다.
builder.Services.AddHttpContextAccessor();

// JSON 직렬화 시 순환 참조 문제를 해결하기 위한 설정
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => {
  options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
  options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// ============================================================
// 4. CORS 구성
// ============================================================
// 평시에는 ApiGateway 를 통해 호출되지만, 서비스 직접 호출(개발/디버깅)도 허용한다.
builder.Services.AddCors(options => {
  options.AddPolicy("AllowAll", policy => {
    policy.SetIsOriginAllowed(_ => true)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
  });
});

// ============================================================
// 5. Swagger/OpenAPI 구성 (API 문서화)
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
  c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo {
    Title = "HelpDesk API",
    Version = "v1",
    Description = "헬프데스크(요청/WBS/일정/공지) 마이크로서비스 API"
  });
});

// ============================================================
// 6. 인증/인가 구성
// ============================================================
// HelpDesk 는 자체 로그인(/api/users/login)으로 토큰을 발급하므로 자기 발급 토큰을 검증한다.
// 동시에 ApiGateway(AuthServer) 가 발급한 funeralv2 토큰도 수용해야 퍼널v2 메뉴에서 그대로 호출할 수 있다.
// 따라서 두 발급자/서명키를 모두 유효한 것으로 등록한다.
builder.Services.AddAuthorization();

// 키 둘은 appsettings.Local.json (git 제외) 에만 있다. 없으면 기동에 실패한다 (결정 D1-B).
//
//   Jwt:Key         헬프데스크 자체 로그인용 (LocalLogin 이 꺼져 있어 지금은 쓰이지 않는다)
//   GatewayJwt:Key  게이트웨이/AuthServer 가 발급한 funeralv2 토큰 검증용 — 이쪽이 실제로 쓰인다
//
// 예전에는 둘 다 평문 폴백이 있었고, GatewayJwt 가 비면 헬프데스크 자체 키로 조용히
// 대체됐다. 그러면 게이트웨이 토큰이 통째로 거부되는데 이유를 알기 어렵다.
var jwtKey = JSini.Shared.Infrastructure.JwtKeyGuard.Require(
    builder.Configuration, "Jwt:Key", "HelpDeskServer");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "helpdesk-api";
var gatewayJwtKey = JSini.Shared.Infrastructure.JwtKeyGuard.Require(
    builder.Configuration, "GatewayJwt:Key", "HelpDeskServer");
var gatewayJwtIssuer = builder.Configuration["GatewayJwt:Issuer"] ?? jwtIssuer;

builder.Services.AddAuthentication(options => {
  options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
  options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => {
  options.TokenValidationParameters = new TokenValidationParameters {
    ValidateIssuer = true,
    ValidateAudience = false,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuers = new[] { jwtIssuer, gatewayJwtIssuer },
    IssuerSigningKeys = new[] {
      new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
      new SymmetricSecurityKey(Encoding.UTF8.GetBytes(gatewayJwtKey))
    }
  };
});

// ============================================================
// 7. 비즈니스 서비스 의존성 주입
// ============================================================
// VAPID 옵션 + 푸시 관련 DI
builder.Services.Configure<VapidOptions>(builder.Configuration.GetSection("Vapid"));
builder.Services.AddScoped<IPushSubscriptionStore, DbPushSubscriptionStore>();
builder.Services.AddSingleton<IWebPushService>(sp => new WebPushService(
    sp.GetRequiredService<IOptions<VapidOptions>>(),
    sp.GetRequiredService<IServiceScopeFactory>(),
    sp.GetRequiredService<ILogger<WebPushService>>()
));
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// funeralv2 계정 단일화: AuthServer 계정을 헬프데스크 계정으로 해석한다.
builder.Services.Configure<AccountLinkOptions>(builder.Configuration.GetSection(AccountLinkOptions.SectionName));
// 어떤 포털 역할을 헬프데스크 담당자로 대우할지.
builder.Services.Configure<HelpdeskIdentityOptions>(builder.Configuration.GetSection(HelpdeskIdentityOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IFuneralAccountLinkService, FuneralAccountLinkService>();

// ============================================================
// 8. 백그라운드 워커
// ============================================================
// 외부 API 를 주기적으로 찌르므로 개발 환경에서는 설정으로 끌 수 있게 한다.
if (builder.Configuration.GetValue("Workers:HealthCheckEnabled", true)) {
  // 1분마다 외부 API 상태를 확인하는 백그라운드 서비스
  builder.Services.AddHostedService<HealthCheckWorker>();
}

if (builder.Configuration.GetValue("Workers:AutoCheckEnabled", true)) {
  // 주기적으로 자동 처리 작업을 수행하는 백그라운드 서비스
  builder.Services.AddHostedService<AutoCheckWorker>();
}

// [헬스체크]
// 게이트웨이의 능동 헬스체크와 오케스트레이터(K8s/로드밸런서)의 liveness 프로빙 대상.
// 인증 없이 접근 가능해야 하므로 별도 정책을 걸지 않는다.
builder.Services.AddHttpClient();
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

if (app.Environment.IsDevelopment()) {
  app.UseSwagger();
  app.UseSwaggerUI();
}

// ApiGateway 뒤에서 HTTP 로만 수신하므로 HTTPS 리다이렉트는 걸지 않는다.
app.UseGlobalExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors("AllowAll");
app.UseAuthentication();
// 인증 직후 · 인가 직전에 끼워 넣는다. funeralv2 토큰으로 들어온 요청에
// 헬프데스크 내부 클레임(uid/login_type/company_id)을 채워 기존 엔드포인트가 그대로 동작하게 한다.
app.UseFuneralIdentityMapping();
app.UseAuthorization();

// ============================================================
// 9. API 엔드포인트 등록
// ============================================================
app.MapRegistEndpoints();

app.MapCompanyEndpoints();
app.MapCustomerEndpoints();
app.MapAdminEndpoints();
app.MapTeamEndpoints();
// 헬프데스크 자체 메뉴·역할·권한 엔드포인트(/api/menus, /api/roles)는 제거했다 (결정 Q4).
// 메뉴와 권한은 JSini 관리 포털이 일원 관리한다 (scom.system_menus / scom.roles / scom.role_menus).
// jsini.menu · approle · menurole · rolemenupermission 테이블은 그대로 두었다(DB 는 건드리지 않는다).
app.MapRequestEndpoints();
app.MapCommentEndpoints();
app.MapAttachmentEndpoints();
app.MapDashboardEndpoints();
app.MapNoticeEndpoints();
app.MapFileUploadEndpoints();
app.MapWbsEndpoints();
app.MapWbsDiagramEndpoints();
app.MapProjectEndpoints();
app.MapWbsLinkEndpoints();
app.MapContactEndpoints();
app.MapAuthLinkEndpoints();

// Push Endpoints
app.MapPushEndpoints();
app.MapUserPropertyEndpoints();
app.MapUserEndpoints();
app.MapChecklistEndpoints();
app.MapScheduleEndpoints();
app.MapUtilEndpoints();

string GetServerName() {
  return Environment.GetEnvironmentVariable("SERVER_NAME")
      ?? Assembly.GetEntryAssembly()?.GetName().Name
      ?? typeof(Program).Namespace
      ?? "HELPDESK";
}

app.Lifetime.ApplicationStarted.Register(() => {
  var serverName = GetServerName();
  var env = app.Environment.EnvironmentName;
  var pid = Environment.ProcessId;

  // 🔥 헬프데스크는 보라색으로 지정
  var color = Color.Purple;

  // 🔥 환경 색상
  var envColor = env switch {
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
  var urlLines = app.Urls.Select(url => {
    try {
      var uri = new Uri(url);
      return $"[blue]🌐 {Markup.Escape(url)}[/]  [grey](PORT: {uri.Port})[/]";
    }
    catch {
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

try {
  Log.Information("Starting HelpDeskServer web host");
  app.Run();
}
catch (Exception ex) {
  Log.Fatal(ex, "Host terminated unexpectedly");
}
finally {
  Log.CloseAndFlush();
}

// (설정 반영을 위한 재기동 트리거)


