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

// 스키마 기본값은 AppDbContext.Schema 한 곳에만 둔다 — 여기서 덮으면 기본값이 두 곳이 된다.
AppDbContext.Schema = builder.Configuration["Database:Schema"] ?? AppDbContext.Schema;

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
    Description = "헬프데스크(요청/공지/리포트) 마이크로서비스 API"
  });
});

// ============================================================
// 6. 인증/인가 구성
// ============================================================
// 헬프데스크 자체 발급 토큰과 게이트웨이(funeralv2) 토큰, 두 발급자·서명키를 모두 유효하게 등록한다.
builder.Services.AddAuthorization();

// 키 둘은 appsettings.Local.json (git 제외) 에만 있다. 없으면 기동에 실패한다 (결정 D1-B).
//
//   Jwt:Key         헬프데스크 자체 로그인용 (LocalLogin 이 꺼져 있어 지금은 쓰이지 않는다)
//   GatewayJwt:Key  게이트웨이/AuthServer 가 발급한 funeralv2 토큰 검증용 — 이쪽이 실제로 쓰인다
//
// 평문 폴백은 두지 않는다 — GatewayJwt 가 비어 자체 키로 대체되면 게이트웨이 토큰이 이유 없이 거부된다.
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
builder.Services.AddScoped<IAdminService, AdminService>();

// funeralv2 계정 단일화: AuthServer 계정을 헬프데스크 계정으로 해석한다.
builder.Services.Configure<AccountLinkOptions>(builder.Configuration.GetSection(AccountLinkOptions.SectionName));
// 어떤 포털 역할을 헬프데스크 담당자로 대우할지.
builder.Services.Configure<HelpdeskIdentityOptions>(builder.Configuration.GetSection(HelpdeskIdentityOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IFuneralAccountLinkService, FuneralAccountLinkService>();
builder.Services.AddScoped<IRequesterProvisioner, RequesterProvisioner>();
// 접수자(담당자)도 같은 사정이다 — 새 DB 라 `admin` 이 0명이어서
// 「접수」를 누른 사람을 가리킬 줄이 없다.
builder.Services.AddScoped<IAssigneeProvisioner, AssigneeProvisioner>();

// 회사는 포털이 단독으로 관리한다. 헬프데스크에는 회사 표가 없고, 이름이
// 필요하면 이 통로로 AuthServer 에 묻는다(5분 캐시).
builder.Services.AddSingleton<IPortalCompanyDirectory, PortalCompanyDirectory>();

// 사람 이름을 푸는 통로. 회사 쪽과 같은 폭(싱글턴)이다 — 둘 다 부르는 사람과
// 무관한 조회이고 캐시를 회로 너머로 공유해야 값이 있다.
builder.Services.AddSingleton<IPortalAccountDirectory, PortalAccountDirectory>();

// 요청글의 주인·접수자를 **포털 로그인 아이디**로 푸는 자리. 알림이 나가는
// 자리마다 제각기 풀면 한쪽이 비었을 때 조용히 0 명이 된다.
builder.Services.AddScoped<IRequestOwnerResolver, RequestOwnerResolver>();

// 알림 서비스로 한 통 건네는 통로. 실패해도 업무를 넘어뜨리지 않는다.
builder.Services.AddScoped<INotifyGateway, NotifyGateway>();

// 「내 요청글에 댓글이 달렸다」를 글 주인에게 알린다(앱 푸시 · 이메일).
// DB 를 읽으므로 Scoped 다.
builder.Services.AddScoped<ICommentNotifier, CommentNotifier>();

// 「내 요청글이 접수·완료·종료 되었다」를 당사자에게 알린다(앱 푸시 · 이메일).
builder.Services.AddScoped<IRequestStatusNotifier, RequestStatusNotifier>();

// 현황판(/api/dashboard/overview)이 쓰는 집계. DB 를 읽으므로 Scoped 다.
builder.Services.AddScoped<DashboardOverviewService>();

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

// 게이트웨이의 능동 헬스체크와 오케스트레이터(K8s/로드밸런서)의 liveness 프로빙 대상.
// 인증 없이 접근 가능해야 하므로 별도 정책을 걸지 않는다.
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();


// DB 가 끊기면 Unhealthy(503), 일부 기능만 막히면 Degraded. 프로세스가 살아 있는 것과 제 일을 하는 것은 다르다. 상태 화면은 30초 캐시로 읽는다.
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

// 고객사·조직 관리·메뉴·권한 엔드포인트는 두지 않는다. 회사·조직·권한은 포털이 관리하고, 헬프데스크의 옛 표는 DB 에 그대로 남아 있다.
app.MapCustomerEndpoints();
app.MapRequestEndpoints();
app.MapCommentEndpoints();
app.MapAttachmentEndpoints();
app.MapDashboardEndpoints();
app.MapNoticeEndpoints();
app.MapFileUploadEndpoints();
// 프로젝트·일정·WBS 엔드포인트는 두지 않는다. 남은 GET /api/wbs 는 「도구 > 다이어그램」 화면이 항목을 고르는 데 쓴다.
app.MapWbsEndpoints();
app.MapWbsDiagramEndpoints();
app.MapAuthLinkEndpoints();

// Push Endpoints
app.MapPushEndpoints();
app.MapUserPropertyEndpoints();
app.MapUserEndpoints();
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


