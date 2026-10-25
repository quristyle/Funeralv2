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

// NotificationServer — 푸시·이메일 발송 공용. 받는 사람은 부르는 쪽이 정하고 주인 키 목록을 넘긴다(대상 판정은 여기 없다).

var builder = WebApplication.CreateBuilder(args);

// 로컬 설정(Git 제외). 빠지면 아래 키 검사가 뜻대로 돌지 않는다.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 로그 형식은 다른 서비스와 맞춘다(모양이 다르면 장애를 쫓는 시간이 배로 든다).
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// ── DB ──────────────────────────────────────────────────────
// 포털 DB(funeralv2 / scom). 구독 표 하나 때문에 DB 를 늘리지 않았다 — Data/AppDbContext.cs 참고.
var connectionString = builder.Configuration.GetConnectionString("jsinicore")
                    ?? builder.Configuration["jsinicore"]
                    ?? Environment.GetEnvironmentVariable("jsinicore");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString,
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", "scom")));

// ── 인증 ────────────────────────────────────────────────────
// 게이트웨이가 1차 검증하고 여기서도 검증한다. 키가 없으면 기동에 실패한다.
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
// VAPID 키는 이 서비스 한 곳에만 있다.
builder.Services.Configure<VapidOptions>(builder.Configuration.GetSection("Vapid"));
// 푸시 수명·겹침 설정(없으면 6시간·high). 근거는 PushDeliveryOptions 에 있다.
builder.Services.Configure<PushDeliveryOptions>(
    builder.Configuration.GetSection(PushDeliveryOptions.SectionName));
builder.Services.Configure<EmailQueueOptions>(builder.Configuration.GetSection("EmailQueue"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();

// 알림 정책. 발송마다 읽으므로 짧게 들고 있고 저장할 때 바로 버린다.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<INotificationPolicyService, NotificationPolicyService>();

// 관리 화면 권한은 서버가 스스로 본다(게이트웨이는 로그인만 본다).
builder.Services.AddSingleton<MenuViewAccess>();
// 지나온 자리 기록(설정 저장과 분리: LocationTrackService 참고).
builder.Services.AddScoped<ILocationTrackService, LocationTrackService>();
// 알림 아이콘 열쇠 — 로그인하지 않은 기기에서도 얼굴이 뜨게 한다.
builder.Services.AddSingleton<IAvatarIconTokenFactory, AvatarIconTokenFactory>();
builder.Services.AddScoped<IAvatarIconResolver, AvatarIconResolver>();

// 로그인 아이디 → 대표 메일. 메일을 내는 자리가 둘이라 한 곳에 둔다.
builder.Services.AddScoped<IAccountEmailResolver, AccountEmailResolver>();
builder.Services.AddScoped<IPushSender, PushSender>();

// 푸시 HttpClient 는 하나를 물려 쓴다(발송마다 맺으면 소켓이 TIME_WAIT 로 쌓인다). 제한 30초는 기기당 전체 왕복 한도다(docs/push-delivery.md).
builder.Services.AddHttpClient(PushSender.HttpClientName, c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
})
.SetHandlerLifetime(TimeSpan.FromMinutes(10));

// 쪽지의 받는 사람을 아이디·이메일로 푼다(scom 을 읽기만 한다).
builder.Services.AddScoped<INoteRecipientResolver, NoteRecipientResolver>();
builder.Services.AddScoped<IEmailQueueSender, EmailQueueSender>();

// 쪽지 전환 메일: 앱 푸시가 안 닿은 쪽지를 메일로 돌린다(NoteFallbackMail:Enabled=false 로 끈다).
builder.Services.Configure<NoteFallbackMailOptions>(
    builder.Configuration.GetSection("NoteFallbackMail"));
builder.Services.AddHostedService<NoteFallbackMailer>();

// 배포 알림 — 배포 파이프라인이 반영 끝을 알려 오면 슈퍼관리자에게 푸시한다. 토큰이 없으면 엔드포인트가 닫힌다.
builder.Services.Configure<DeployNotifyOptions>(
    builder.Configuration.GetSection(DeployNotifyOptions.SectionName));

// 카카오 알림톡 (D-G1b · 기본 꺼짐). 자격증명·수신 번호가 정해지기 전에는 0건만 돌려준다.
builder.Services.Configure<KakaoOptions>(builder.Configuration.GetSection(KakaoOptions.SectionName));
builder.Services.AddHttpClient<IKakaoAlimtalkSender, BizppurioAlimtalkSender>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();


// ── 딸린 것: DB ────────────────────────────────────────────
// 살아 있는 것과 제 일을 하는 것은 다르다(DB 가 끊기면 Unhealthy 503, 일부만 막히면 Degraded).
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
app.MapNoteEndpoints();       // 쪽지 — 사람에게서 사람에게로 가는 짧은 글
app.MapLocationEndpoints();   // 위치를 허용한 계정들 — 좌표가 이 서비스에 있다
app.MapEmailEndpoints();
app.MapMailEndpoints();       // 포털 「메일 보내기」 — 사람이 써서 보내고, 보낸 글이 남는다
app.MapInternalQueryEndpoints(); // 업무 비서(AI) 내부 조회 — 게이트웨이를 거치지 않는다
app.MapWeatherEventEndpoints(); // 기상 이벤트 발송 (D-G1a) — LifeEnvServer 가 부른다
app.MapDeployEventEndpoints();  // 배포 반영 알림 — GitHub Actions 의 deploy 잡이 부른다
app.MapNotificationPolicyEndpoints(); // 알림관리 — 어떤 이벤트를 어느 역할이 어느 길로 받나

// 설정이 반쪽이면 기동할 때 한 번 알린다(조용히 못 보내는 것이 가장 나쁘다).
app.Lifetime.ApplicationStarted.Register(() =>
{
    var vapid = app.Services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<VapidOptions>>().Value;
    var email = app.Services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<EmailQueueOptions>>().Value;

    var deploy = app.Services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<DeployNotifyOptions>>().Value;

    // SMTP 직발송(/emails/send). 메일이 나가는 길은 이쪽이다.
    var smtp = app.Services.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<EmailSettings>>().Value;

    Log.Information(
        "NotificationServer 시작. 푸시={Push} 메일(SMTP)={Smtp} 메일(큐)={Email} 배포알림={Deploy}",
        vapid.IsConfigured ? "사용 가능" : "설정 없음 (Vapid:*)",
        smtp.IsConfigured ? $"사용 가능 ({smtp.Host}:{smtp.Port})" : "설정 없음 (EmailSettings:*)",
        email.IsConfigured ? "사용 가능" : "설정 없음 (EmailQueue:*)",
        deploy.IsConfigured ? "사용 가능" : "설정 없음 (DeployNotify:Token)");

    if (!vapid.IsConfigured)
    {
        Log.Warning("VAPID 설정이 없어 푸시를 보낼 수 없습니다. " +
                    "Vapid:Subject·PublicKey·PrivateKey 를 appsettings.Local.json 에 넣으세요.");
    }

    // 설정이 비면 비밀번호 찾기 메일이 조용히 사라진다. 기동 로그의 이 줄이 유일한 예고다.
    if (!smtp.IsConfigured)
    {
        Log.Warning(
            "SMTP 설정이 없어 메일이 한 통도 나가지 못합니다 ({Missing}). " +
            "비밀번호 찾기 · 소개 사이트 문의 접수 · AI 작업 알림 · 관리자 메일 보내기가 모두 멈춥니다. " +
            "appsettings.Local.json 에 넣으세요 (docs/email-smtp.md).",
            smtp.MissingKeys());
    }
});

app.Run();
