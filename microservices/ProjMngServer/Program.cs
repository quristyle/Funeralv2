using System.Reflection;
using System.Text;
using JSini.Shared.Infrastructure.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProjMngServer;
using ProjMngServer.Filters;
using ProjMngServer.Services;

// 전역 시간대 설정 (KST) — 다른 MSA 와 동일하게 맞춘다.
Environment.SetEnvironmentVariable("TZ", "Asia/Seoul");

var builder = WebApplication.CreateBuilder(args);

// DB 접속 문자열 등 장비별 설정. Git 에 올리지 않는다.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ============================================================
// 1. MVC / 컨트롤러
// ============================================================
// 이식 전 구조를 그대로 살린다. 화면이 호출하는 경로는 /api/Proj, /api/Dev, /api/Sys, /api/Media 넷이다.
builder.Services.AddControllers(options => {
  // 요청 본문의 SSUserId 를 게이트웨이 신원(X-User-Id)으로 갈아 끼운다.
  options.Filters.Add<UserIdentityActionFilter>();
});

builder.Services.AddScoped<DevService>();
builder.Services.AddScoped<ProjService>();
builder.Services.AddScoped<SysService>();

// 연결 가능한 프로젝트 DB 목록 캐시
builder.Services.AddSingleton<AppData>();

builder.Services.AddHttpContextAccessor();

// ============================================================
// 2. CORS
// ============================================================
// 평시에는 ApiGateway 를 통해 들어오지만, 개발 중 직접 호출도 허용한다.
builder.Services.AddCors(options => {
  options.AddPolicy("AllowAll", policy => {
    policy.SetIsOriginAllowed(_ => true)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
  });
});

// ============================================================
// 3. Swagger
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
  c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo {
    Title = "ProjMng API",
    Version = "v1",
    Description = "프로젝트관리(구 ProjMngServer) 마이크로서비스 API. "
                + "저장 프로시저를 이름으로 호출하는 범용 데이터 통로다."
  });
});

// ============================================================
// 4. 인증/인가
// ============================================================
// 실제 인가 판단은 게이트웨이가 한다. 이 서비스는 루프백에만 바인딩되어 있어
// 게이트웨이를 지나지 않은 요청은 같은 장비에서만 들어올 수 있다.
// 그래도 토큰이 실려 오면 검증은 해 둔다(게이트웨이 우회 시 최소 방어선).
// 키는 appsettings.Local.json (git 제외) 에만 있다 (결정 D1-B).
//
// **아래 `if (!IsNullOrWhiteSpace)` 를 그대로 둔 이유**: 이 서비스는 키가 없으면
// JWT 검증을 아예 등록하지 않는 구조였다. 그래서 키가 빠지면 조용히 검증이 사라진다.
// 자리표시자·옛 평문 키·너무 짧은 값은 여기서 걸러 기동을 막고, "설정에 아예 없는"
// 경우만 예전처럼 건너뛴다 — 이 서비스가 게이트웨이 뒤에만 있다는 전제를 바꾸지 않으려는 것이다.
var jwtKey = string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"])
    ? null
    : JSini.Shared.Infrastructure.JwtKeyGuard.Require(
        builder.Configuration, "Jwt:Key", "ProjMngServer");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "funeralv2-auth";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "funeralv2-services";

builder.Services.AddAuthorization();

if (!string.IsNullOrWhiteSpace(jwtKey)) {
  builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
      options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
      };
    });
}

builder.Services.AddHealthChecks();

var app = builder.Build();

// 헬스체크. 게이트웨이의 능동 헬스체크 대상이라 인증을 걸지 않는다.
app.MapHealthChecks("/health").AllowAnonymous();

if (app.Environment.IsDevelopment()) {
  app.UseSwagger();
  app.UseSwaggerUI();
}

// ApiGateway 뒤에서 HTTP 로만 수신하므로 HTTPS 리다이렉트는 걸지 않는다.
app.UseGlobalExceptionHandler();
app.UseCors("AllowAll");

if (!string.IsNullOrWhiteSpace(jwtKey)) {
  app.UseAuthentication();
}
app.UseAuthorization();

// 임의 SQL 실행 경로 보호. 인가 직후에 둔다.
app.UseRawSqlGuard();

app.MapControllers();

string GetServerName() {
  return Environment.GetEnvironmentVariable("SERVER_NAME")
      ?? Assembly.GetEntryAssembly()?.GetName().Name
      ?? "PROJMNG";
}

app.Lifetime.ApplicationStarted.Register(() => {
  Console.WriteLine($"[{GetServerName()}] ProjMng API 시작 — env={app.Environment.EnvironmentName}, pid={Environment.ProcessId}");

  if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("jsini"))) {
    Console.WriteLine(
      "[PROJMNG] 경고: ConnectionStrings:jsini 가 없습니다. 저장 프로시저 호출이 모두 실패합니다."
    + Environment.NewLine
    + "          microservices/ProjMngServer/appsettings.Local.json 에 접속 문자열을 넣으세요.");
  }
});

app.Run();
