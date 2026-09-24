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
  // 옛 ProjMngWasm 봉투를 표준 봉투(ApiResponse)로 갈아입힌다 (결정 D-A1, 2026-09-04).
  // 서비스·프로시저 규약은 그대로다 — 직렬화 경계에서만 바뀐다.
  options.Filters.Add<ApiEnvelopeResultFilter>();
});

builder.Services.AddScoped<DevService>();
builder.Services.AddScoped<ProjService>();

// 프로시저를 걷어내며 생기는 업무 서비스들. 범용 통로(ProjService)와 달리
// **자기 표 하나만** 안다 — 무엇을 부를 수 있는지가 경로에 드러난다.
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ProjectUserService>();
builder.Services.AddScoped<DevCommonCodeService>();
builder.Services.AddScoped<SourceInfoService>();
builder.Services.AddScoped<ProjectDbService>();
builder.Services.AddScoped<HomeTodoService>();
builder.Services.AddScoped<ProjCodeService>();
builder.Services.AddScoped<WbsService>();
builder.Services.AddScoped<DbLogicService>();
builder.Services.AddScoped<ActivityInfoService>();
builder.Services.AddScoped<ProjectPropService>();

// WBS 대시보드 — 사내망에서 따로 돌던 물건을 옮겨 온 것이다
// (deploy/sql/projmng-wbs-2026-09-23.sql 이 표를 만든다).
// `WbsService` 와 이름이 비슷하지만 **다른 표**다 — 그쪽은 프로젝트별 공정표
// (`dev_wbs`), 이쪽은 화면 단위 원장(`wbs_work`)이다.
builder.Services.AddScoped<WbsBoardService>();
builder.Services.AddScoped<WbsProgressService>();
builder.Services.AddScoped<WbsDelayService>();
builder.Services.AddScoped<WbsBoardTaskService>();
builder.Services.AddScoped<WbsBoardUserService>();
builder.Services.AddScoped<WbsDocsService>();

// EAI 인터페이스 카탈로그. 첨부는 표가 아니라 파일 이름에 메타를 담는다.
builder.Services.AddScoped<InterfaceService>();
builder.Services.AddScoped<InterfaceFileService>();


// Git(GitHub). 저장소 목록이 비면 아무것도 부르지 않고 빈 결과를 준다.
// **토큰은 없어도 된다** — 공개 저장소는 그대로 읽히고 한도만 낮다.
builder.Services.AddScoped<GitHubClient>();
builder.Services.AddScoped<GitService>();
builder.Services.AddScoped<GitMonitorService>();

// AI 작업 지시 — docs/ai-task-runner.md.
// 작업 서비스가 대상 서비스를 받는다 — push 를 켤 수 있는 대상인지 되묻기 때문이다.
// 그 값 하나가 운영 배포를 일으키므로 화면 말고 여기서도 본다.
builder.Services.AddScoped<AiTargetService>();

// 대상의 git 상태. **서버가 git 을 부르는 서비스가 아니다** — 컨테이너 안에서
// 호스트 경로를 볼 수 없어서, 실행기가 들여다보고 적어 둔 것을 받아 두었다가
// 화면에 내주는 자리다(설계 11.7).
builder.Services.AddScoped<AiTargetStatusService>();
// 지시에 함께 올린 그림·파일. **바이트가 DB 에 있다** — 이 컨테이너에는
// 붙은 디스크가 없어서, 파일 시스템에 담으면 다음 배포에 사라진다
// (deploy/sql/projmng-ai-task-file-2026-09-25.sql 머리말).
builder.Services.AddScoped<AiTaskFileService>();

builder.Services.AddScoped<AiTaskService>();
builder.Services.AddScoped<AiRunService>();

// AI 작업 현황. **읽기뿐이다** — 집계만 한다.
builder.Services.AddScoped<AiDashboardService>();

// 실행기가 올려 주는 AI CLI 한도(`/usage`). 서버는 CLI 를 부르지 않는다.
builder.Services.AddScoped<AiUsageService>();

// 끝난 작업을 메일로 알린다. **못 보내도 작업 상태를 바꾸지 않는다**(설계 8-2).
// 알림 서비스를 부르므로 HttpClient 공장이 필요하다.
builder.Services.AddHttpClient();
builder.Services.AddScoped<AiTaskNotifier>();

// 올라온 「AI 작업 요청」을 관리자에게 앱 푸시로 알린다.
// **Singleton 이다** — 저장 응답을 기다리게 하지 않으려고 뒤로 띄워 보내는데,
// 그 일이 응답보다 오래 살아야 하므로 요청 수명(Scoped)에 매달면 안 된다.
// 무는 것은 설정·HttpClient 공장·로거뿐이라 함께 살아도 안전하다.
builder.Services.AddSingleton<AiRequestAlerter>();

// 결과 요약을 AI 에게 다시 쓰게 한다(AIAgentServer 를 부른다).
// **실패하면 null 을 주고 끝난다** — 메일은 옛 방식으로 그대로 나간다.
builder.Services.AddScoped<AiResultSummarizer>();

// 그 요약을 `ai_task_run.summary_text` 에 적는 일의 주인. **알림에서 떼어 냈다** —
// 메일도 앱푸시도 끈 건에도 요약은 있어야 하고, 알림 안에 두면 그 수명이
// 알림 관문에 매달린다. 완료 처리가 부르고, 알림은 적힌 것을 읽는다.
builder.Services.AddScoped<AiRunSummaryWriter>();

// 끝난 실행을 보고 제목을 다시 짓는다. **제목 칸을 비우고 저장한 건만** 손댄다
// (`ai_task.title_auto`) — 사람이 적은 제목은 읽지도 않는다.
builder.Services.AddScoped<AiTaskTitler>();

// 큐는 **종(bell)** 이다. 메시지에 taskKey 하나만 싣고 실제 상태 변경은
// DB 의 원자적 UPDATE 가 한다 — 그래서 메시지를 잃어도 손실이 아니라 지연이다.
builder.Services.AddSingleton<AiTaskQueue>();

// 그 종을 **뒤에서** 울린다. 요청 처리 안에서 울리면 AMQP 연결 한 번이
// 화면의 「보내는 중」에 그대로 붙는다 — 큐에 넣는 일이 사람을 기다리게 할
// 이유가 없다.
builder.Services.AddHostedService<AiTaskBell>();

// 멈춘 것을 찾아내는 감시자. **이 기능에서 가장 나쁜 실패는 조용히 멈춰
// 있는 것**이라 프로세스가 사는 동안 계속 돈다.
builder.Services.AddHostedService<AiStaleSweeper>();

// 비어 있는 「처리 요약」을 뒤늦게 채운다. 끝나는 순간의 한 번은 결과 메일이
// 기다리는 자리라 오래 못 붙잡는데, 하필 그때 AI 가 붐비면 그 건의 요약은
// **영영** 없었다. 그 한 번을 빠른 길로 두고, 놓친 것은 여기가 줍는다.
builder.Services.AddHostedService<AiSummaryCatchUp>();

// Dapper 가 DateOnly 를 파라미터로 다루게 한다. **여기 한 곳에서만 등록한다** —
// 서비스마다 부르면 빠뜨리는 서비스가 생기고, 그 서비스의 저장만 죽는다.
DapperDateOnlyHandlers.Register();

// 연결 가능한 프로젝트 DB 목록 캐시

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
  c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo {
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
