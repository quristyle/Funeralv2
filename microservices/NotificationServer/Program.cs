using System.Text;

using JSini.Shared.Infrastructure;
using JSini.Shared.Infrastructure.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NotificationServer.Data;
using NotificationServer.Endpoints;
using NotificationServer.Options;
using NotificationServer.Services;
using NotificationServer.Models;

using Serilog;
using JSini.Shared.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ============================================================
// NotificationServer — 푸시·이메일을 세 시스템이 공유한다 (결정 D8-A)
// ============================================================
//
// 예전에는 푸시·이메일이 **헬프데스크 안에만** 있었다. 그래서
//
//   * 포털도 장례식장도 알림을 보내려면 헬프데스크를 거쳐야 했다
//   * VAPID 키가 두 서비스에 중복으로 박혀 있었다 (그래서 D1 에서도 걸렸다)
//   * 대상 선택 로직(팀·회사·관리자)이 발송 코드와 얽혀 헬프데스크 밖에서 못 썼다
//
// 이 서비스는 **보내는 일만** 한다. 누구에게 보낼지는 부르는 쪽이 정하고
// 주인 키 목록을 넘긴다.

var builder = WebApplication.CreateBuilder(args);

// 로컬 개별 설정 (Git 제외). 다른 서비스들과 같은 자리에 같은 방식으로 둔다.
// **이 줄이 없으면 아래 키 검사가 뜻대로 동작하지 않는다** — 게이트웨이에서
// 같은 것을 빠뜨려 토큰이 전부 401 이 된 일이 있었다(D1-B).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 로그 형식을 다른 서비스와 맞춘다. 장애를 쫓을 때 모양이 다르면 시간이 배로 든다.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// ── DB ──────────────────────────────────────────────────────
//
// 포털 DB(funeralv2 / scom)를 쓴다. 서비스별 DB 가 정석이지만 결정 D2(DB 통합)가
// 열려 있어, 구독 표 하나 때문에 DB 를 늘리지 않았다. Data/AppDbContext.cs 참고.
var connectionString = builder.Configuration.GetConnectionString("jsinicore")
                    ?? builder.Configuration["jsinicore"]
                    ?? Environment.GetEnvironmentVariable("jsinicore");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString,
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", "scom")));

// ── 인증 ────────────────────────────────────────────────────
//
// 게이트웨이가 1차 검증하고 X-User-* 헤더를 붙여 준다. 그래도 토큰이 실려 오면
// 여기서도 검증한다(게이트웨이 우회 시 최소 방어선).
//
// 키는 appsettings.Local.json 에만 있다. 없으면 기동에 실패한다 (결정 D1-B).
var jwtKey = JwtKeyGuard.Require(builder.Configuration, "Jwt:Key", "NotificationServer");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "funeralv2-auth",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "funeralv2-services",
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── 설정과 서비스 ───────────────────────────────────────────
//
// VAPID 키가 **이제 한 곳에만** 있다. 이 서비스를 만든 이유 중 하나다.
builder.Services.Configure<VapidOptions>(builder.Configuration.GetSection("Vapid"));
builder.Services.Configure<EmailQueueOptions>(builder.Configuration.GetSection("EmailQueue"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<IPushSender, PushSender>();
builder.Services.AddScoped<IEmailQueueSender, EmailQueueSender>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
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

app.UseSerilogRequestLogging();

// 게이트웨이의 능동 헬스체크 대상. 인증을 걸지 않는다.
app.MapJsiniHealthChecks();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapNotificationEndpoints();

// 설정이 반쪽이면 기동할 때 한 번 말해 준다. 조용히 못 보내는 것이 가장 나쁘다.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var vapid = app.Services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<VapidOptions>>().Value;
    var email = app.Services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<EmailQueueOptions>>().Value;

    Log.Information("NotificationServer 시작. 푸시={Push} 이메일={Email}",
        vapid.IsConfigured ? "사용 가능" : "설정 없음 (Vapid:*)",
        email.IsConfigured ? "사용 가능" : "설정 없음 (EmailQueue:*)");

    if (!vapid.IsConfigured)
    {
        Log.Warning("VAPID 설정이 없어 푸시를 보낼 수 없습니다. " +
                    "Vapid:Subject·PublicKey·PrivateKey 를 appsettings.Local.json 에 넣으세요.");
    }
});

app.Run();
