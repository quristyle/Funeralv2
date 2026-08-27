using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;
using Spectre.Console;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 로컬 개별 설정 (Git 제외). 다른 서비스들과 같은 자리에 같은 방식으로 둔다.
//
// **이 줄이 없으면 아래 D1-B 의 키 검사가 뜻대로 동작하지 않는다.** 키를
// appsettings.Local.json 에만 두어도 게이트웨이는 그 파일을 읽지 못해
// 저장소에 남아 있는 예전 값으로 검증하게 된다. 그러면 AuthServer 가 Local 키로
// 서명한 토큰이 전부 401 이 되고(실제로 그 상태를 겪었다), 더 나쁘게는
// "잘 알려진 키를 못 쓰게 한다" 는 목적 자체가 조용히 깨진다.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Kestrel 요청 본문 크기 제한 해제 (예: 500MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024; // 500MB
});

// Multipart Form 제한 해제 (대용량 비디오 업로드 대응)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024; // 500MB
    options.ValueLengthLimit = 500 * 1024 * 1024;
});


// 1. JWT 검증 설정 (1차 검증)
// ── 서명 키 (결정 D1-B) ─────────────────────────────────────
//
// 예전에는 이 자리에 키가 평문으로 박혀 있었고, 설정이 비어 있으면 **조용히 그 값을
// 썼다.** 저장소를 볼 수 있는 사람이면 누구나 관리자 토큰을 만들 수 있었다는 뜻이다.
//
// 이제 키는 appsettings.Local.json (git 제외) 에만 있고, 없으면 기동에 실패한다.
// 조용히 잘 알려진 키로 도는 것보다 뜨지 않는 편이 낫다.
var jwtKey = JwtKeyGuard.Require(builder.Configuration, "Jwt:Key", "ApiGateway");
var key = Encoding.ASCII.GetBytes(jwtKey);

// ── 파일 읽기 경로에서만 쿠키를 신원으로 받는다 ──────────────────
//
// 화면이 사진을 `<img src="/api/file/thumbnail/{id}">` 로 그리는데 브라우저는 그런 태그에
// `Authorization` 헤더를 붙여 주지 않는다. 그래서 로그인한 사람이 포털에서 사진을 보는
// 요청도 이 아래로는 익명으로 내려갔고, 파일 읽기 라우트를 익명으로 열어 둘 수밖에 없었다.
// 결국 파일 아이디만 알면 누구나 남의 첨부를 받을 수 있는 상태였다.
//
// 로그인할 때 같은 토큰을 `jsini_file_at` 쿠키로도 심는다(AuthServer/Endpoints/AuthEndpoints.cs).
// 브라우저는 그 쿠키를 스스로 보내므로, 여기서 받아 주면 `<img>` 요청도 신원이 붙는다.
//
// **읽기 경로에서만 받는다.** 업로드·삭제·공개여부 변경에까지 쿠키를 받아 주면
// 남의 사이트가 우리 주소로 요청을 걸어 파일을 지울 수 있다(CSRF).
// 쿠키 자체도 `Path=/api/file` · `SameSite=Lax` 로 심어 두었지만, 그것에만 의지하지 않는다.
//
// 쿠키 이름을 바꾸려면 AuthServer 쪽도 함께 바꿔야 한다.
const string fileCookieName = "jsini_file_at";
string[] fileCookiePaths =
[
    "/api/file/download",
    "/api/file/thumbnail",
    "/api/file/medium",
    "/api/file/large",
    "/api/file/resize"
];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                // 헤더로 온 토큰이 우선이다. 없을 때만 쿠키를 본다.
                if (!string.IsNullOrEmpty(ctx.Token))
                {
                    return Task.CompletedTask;
                }

                var path = ctx.Request.Path.Value ?? string.Empty;
                var readable = fileCookiePaths.Any(p =>
                    path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                if (readable && ctx.Request.Cookies.TryGetValue(fileCookieName, out var fromCookie)
                    && !string.IsNullOrEmpty(fromCookie))
                {
                    ctx.Token = fromCookie;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5555")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 2. YARP 설정 및 보안 헤더 제어
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformContext =>
    {
        // 모든 요청에 대해 실행될 트랜스폼 추가
        transformContext.AddRequestTransform(async requestContext =>
        {
            // [보안] 외부에서 보낸 X-User-* 헤더를 무조건 제거하여 위조 방지
            requestContext.ProxyRequest.Headers.Remove("X-User-Id");
            requestContext.ProxyRequest.Headers.Remove("X-User-Role");
            requestContext.ProxyRequest.Headers.Remove("X-User-Roles");
            requestContext.ProxyRequest.Headers.Remove("X-User-Company-Id");
            requestContext.ProxyRequest.Headers.Remove("X-User-Name");
            requestContext.ProxyRequest.Headers.Remove("X-User-Email");
            requestContext.ProxyRequest.Headers.Remove("X-User-Msa-Source");

            var user = requestContext.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                // 게이트웨이가 검증한 JWT 클레임에서 정보 추출
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
                var companyId = user.FindFirst("CompanyId")?.Value;
                var userName = user.FindFirst("RealName")?.Value ?? user.FindFirst(ClaimTypes.Name)?.Value;
                var email = user.FindFirst(ClaimTypes.Email)?.Value;
                // 이 계정이 어느 MSA 레코드에서 왔는지 (`<서비스>:<테이블>:<원본키>`).
                // 이관으로 만들어진 계정은 아이디에 접두어가 붙어 있어(`jskim` → `pm_jskim`)
                // 로그인 아이디만으로는 각 서비스가 자기 사용자를 찾을 수 없다.
                var msaSource = user.FindFirst("MsaSource")?.Value;

                // 검증된 정보를 바탕으로 내부 전용 헤더 재생성
                if (!string.IsNullOrEmpty(userId))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Id", userId);
                }

                // X-User-Role 은 단수라 역할이 여럿인 계정을 표현하지 못한다.
                // 기존 서비스가 읽고 있으므로 첫 역할을 그대로 두고, 전체는 X-User-Roles 로 함께 보낸다.
                if (roles.Length > 0)
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Role", roles[0]);
                    requestContext.ProxyRequest.Headers.Add("X-User-Roles", string.Join(',', roles));
                }
                else
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Role", "User");
                }

                if (!string.IsNullOrEmpty(companyId))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Company-Id", companyId);
                }

                // 이름은 한글이라 그대로 실으면 HTTP 헤더(Latin-1)에서 깨진다. URL 인코딩해서 보낸다.
                // 받는 쪽은 Uri.UnescapeDataString 으로 되돌린다.
                if (!string.IsNullOrEmpty(userName))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Name", Uri.EscapeDataString(userName));
                }
                if (!string.IsNullOrEmpty(email))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Email", email);
                }
                if (!string.IsNullOrEmpty(msaSource))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Msa-Source", msaSource);
                }
            }
            await Task.CompletedTask;
        });
    });

// [헬스체크]
// 게이트웨이의 능동 헬스체크와 오케스트레이터(K8s/로드밸런서)의 liveness 프로빙 대상.
// 인증 없이 접근 가능해야 하므로 별도 정책을 걸지 않는다.
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

// ============================================================
// 레이트 리미팅 — 로그인·비밀번호 관련 경로
// ============================================================
//
// 로그인은 아이디·비밀번호만 맞으면 통과하므로 무차별 대입에 노출된다.
// 비밀번호 초기화 경로는 더 위험하다 — 성공하면 피해자가 로그인하지 못하게 된다.
//
// 두 경로에만 IP 단위 창(window) 제한을 건다.
// 사람이 쓰기에는 넉넉하고(1분에 10회), 자동화 공격에는 의미 있게 느린 수준이다.
// 어느 경로에 적용할지는 appsettings 의 라우트에서 RateLimiterPolicy 로 지정한다.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth-attempts", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // 프록시 뒤에 있으면 X-Forwarded-For 가 실제 클라이언트다.
            partitionKey: httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 소개 사이트의 문의 접수처럼 **로그인하지 않은 사람이 쓰는** 경로에 건다.
    // 로그인보다 더 조인다(분당 3회) — 사람이 문의를 그보다 자주 보낼 일이 없고,
    // 익명 쓰기는 한 번 열리면 곧 스팸의 통로가 된다.
    //
    // 캡차 대신 이것과 허니팟으로 시작한다(결정 D-S4). 외부 스크립트를 부르지 않는 쪽을
    // 골랐는데, 준수사항 5(글꼴은 저장소 안의 파일만)와 같은 취지다 — 내부망에서도 돌아야 한다.
    options.AddPolicy("public-write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"success\":false,\"code\":\"429\",\"message\":\"시도가 너무 잦습니다. 잠시 후 다시 시도해 주세요.\"}",
            token);
    };
});

var app = builder.Build();
// 헬스체크 엔드포인트. 프로세스가 요청을 처리할 수 있는 상태인지만 보고한다.
app.MapHealthChecks("/health").AllowAnonymous();


app.UseCors("AllowFrontend");

// 레이트 리미터. 정책이 지정된 라우트에만 적용된다.
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == StatusCodes.Status502BadGateway || 
        context.Response.StatusCode == StatusCodes.Status504GatewayTimeout)
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            success = false,
            code = "E502",
            message = "서비스 연결에 실패했습니다. 잠시 후 다시 시도해 주세요.",
            data = (object?)null,
            timestamp = DateTime.UtcNow,
            traceId = context.TraceIdentifier,
            path = context.Request.Path.Value,
            realmessage = "Gateway: Bad Gateway or Gateway Timeout."
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// 비밀번호 사용 기간 만료 차단
// ============================================================
//
// 90일마다 비밀번호를 바꾸도록 **요구**한다. 화면에서 안내만 하면 요구가 아니라 부탁이다.
// API 를 직접 부르면 그대로 통과하므로, 실제 차단은 모든 요청이 지나가는 이곳에서 한다.
//
// 판단 근거는 토큰의 `PwdChangedAt` 클레임(비밀번호를 마지막으로 바꾼 시각)이다.
// **만료 여부를 불린으로 싣지 않고 시각을 싣는 이유**는 토큰 수명이 7일이기 때문이다.
// 불린이면 토큰을 받은 뒤 만료되는 구간(발급 시점에는 아직 안 지났던 경우)을 놓친다.
// 시각을 싣고 매 요청마다 다시 계산하면 그 구간이 없다.
//
// 막지 않는 경로가 있다. 비밀번호를 바꾸려면 로그인 상태로 그 화면까지 가야 하므로,
// **비밀번호를 바꾸는 데 꼭 필요한 만큼만** 열어 둔다.
//
// 정책을 끄려면 Auth:PasswordExpiryDays 를 0 으로 둔다(코드 수정 없이 되돌릴 수 있어야 한다).
// AuthServer 도 같은 설정을 읽는다 — 그쪽은 화면에 보여 줄 값을 만들고, 차단은 여기서만 한다.
var passwordExpiryDays = builder.Configuration.GetValue<int?>("Auth:PasswordExpiryDays") ?? 90;

// 만료 상태에서도 통과시키는 경로. 앞부분이 일치하면 통과한다(대소문자 무시).
var passwordExpiryAllowList = new[]
{
    "/api/auth/login",                   // 익명 경로지만 명시해 둔다
    "/api/auth/logout",                  // 잠긴 상태에서 나갈 길은 항상 열려 있어야 한다
    "/api/auth/user/change-password",    // 이 차단을 푸는 유일한 방법
    "/api/auth/user/info",               // /profile 화면이 만료 안내를 그리는 데 쓴다
    "/api/auth/codes",                   // 로그인 직후 프론트가 항상 부른다
    "/api/auth/menu",                    // 메뉴가 없으면 라우트가 생기지 않아 /profile 에도 못 간다
    "/api/file/download",                // 프로필 사진(읽기 전용). 없으면 화면이 깨져 보인다
    "/api/file/thumbnail",
};

if (passwordExpiryDays > 0)
{
    app.Use(async (context, next) =>
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (passwordExpiryAllowList.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        var changedAtRaw = user.FindFirst("PwdChangedAt")?.Value;
        // 클레임이 없으면 기준을 모르는 것이다. 모른다는 이유로 잠그지 않는다
        // (칸을 새로 만든 직후처럼 데이터가 아직 없는 상황에서 전원이 갇힌다).
        if (string.IsNullOrWhiteSpace(changedAtRaw) ||
            !DateTimeOffset.TryParse(
                changedAtRaw,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var changedAt))
        {
            await next();
            return;
        }

        if (DateTimeOffset.UtcNow < changedAt.AddDays(passwordExpiryDays))
        {
            await next();
            return;
        }

        // 프론트가 이 코드를 보고 비밀번호 변경 화면으로 보낸다.
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            code = "E403_PWD_EXPIRED",
            message = $"비밀번호를 바꾼 지 {passwordExpiryDays}일이 지났습니다. 비밀번호를 변경한 뒤 이용해 주세요.",
            data = (object?)null,
            timestamp = DateTime.UtcNow,
            traceId = context.TraceIdentifier,
            path = context.Request.Path.Value,
            realmessage = "Gateway: password expired."
        });
    });
}

// ============================================================
// [서버 상태 모니터링]
// 게이트웨이가 알고 있는 모든 클러스터/목적지를 실제로 찔러 상태를 모아준다.
//
// 관리자 화면에서 브라우저가 각 서비스(:5264, :5320 ...)를 직접 호출하는 것은
// CORS 와 내부망 접근 문제로 불가능하다. 유일하게 모든 서비스를 알고 있는
// 게이트웨이가 대신 조회해 한 번에 돌려주는 구조가 맞다.
//
// YARP 의 능동 헬스체크 결과에 의존하지 않고 요청 시점에 직접 프로브한다.
// (헬스체크를 꺼두더라도 이 화면은 정상 동작해야 한다)
// ============================================================
app.MapGet("/api/gateway/status", async (
    IProxyStateLookup lookup,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var client = httpClientFactory.CreateClient();
    var results = new List<object>();

    foreach (var cluster in lookup.GetClusters())
    {
        foreach (var destination in cluster.DestinationsState.AllDestinations)
        {
            var address = destination.Model.Config.Address.TrimEnd('/');
            var probeUrl = $"{address}/health";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            string status;
            int? httpStatus = null;
            string? error = null;
            string? reason = null;
            List<object> dependencies = new();

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                // LLM 처럼 딸린 것을 점검하는 서비스는 응답이 조금 더 걸린다.
                // 서비스 쪽 점검 타임아웃(3초)보다 넉넉해야 우리가 먼저 끊지 않는다.
                cts.CancelAfter(TimeSpan.FromSeconds(6));

                var response = await client.GetAsync(probeUrl, cts.Token);
                httpStatus = (int)response.StatusCode;
                // /health 를 제공하지 않는 구버전이라도 응답 자체가 오면 프로세스는 살아 있다.
                status = response.IsSuccessStatusCode ? "UP" : "DEGRADED";

                // ── 딸린 것까지 읽는다 ──────────────────────────────
                //
                // **상태 코드만 보면 안 된다.** 프로세스가 멀쩡하면 200 이므로,
                // LLM 장비가 꺼진 AIAgentServer 도 UP 으로 보였다. 그것이 이 화면에서
                // "동작한다" 고 오해하게 만든 원인이다.
                //
                // 각 서비스가 자기 의존 대상을 점검해 본문에 담아 보내므로
                // (JSini.Shared.Infrastructure/HealthChecks) 게이트웨이는 **읽어 올리기만** 한다.
                // 판정 기준을 게이트웨이에 두면 LLM 주소·모델명을 여기서도 알아야 한다.
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                var parsed = HealthBody.Parse(body);
                if (parsed is not null)
                {
                    dependencies = parsed.Dependencies;
                    reason = parsed.Reason;

                    // 서비스가 스스로 Degraded 라고 하면 그 말을 따른다.
                    // 프로세스는 살아 있으니 DOWN 은 아니고, 제 일을 못 하니 UP 도 아니다.
                    if (status == "UP" && parsed.IsDegraded) status = "DEGRADED";
                }
            }
            catch (Exception ex)
            {
                status = "DOWN";
                error = ex.InnerException?.Message ?? ex.Message;
            }
            sw.Stop();

            results.Add(new
            {
                cluster = cluster.ClusterId,
                destination = destination.DestinationId,
                address,
                status,
                httpStatus,
                latencyMs = sw.ElapsedMilliseconds,
                error,
                // 왜 이 상태인지 한 줄. 화면이 배지 옆에 그대로 보여 준다.
                reason,
                // 딸린 것(LLM · DB · 큐 · 저장소 …). 화면이 자식 줄로 펼쳐 보여 준다.
                dependencies,
            });
        }
    }

    // 프론트 요청 클라이언트가 { code: "S000", data: ... } 봉투를 기대하므로 형식을 맞춘다.
    return Results.Ok(new
    {
        success = true,
        code = "S000",
        message = "Success",
        data = new
        {
            gateway = new { status = "UP", checkedAt = DateTime.UtcNow },
            services = results,
        },
        timestamp = DateTime.UtcNow,
    });
}).AllowAnonymous().WithName("GetGatewayStatus");

app.MapReverseProxy();

app.MapFallback(async (HttpContext context) =>
{
    var response = new
    {
        success = false,
        code = "E404",
        message = "요청하신 경로를 찾을 수 없습니다.",
        data = (object?)null,
        timestamp = DateTime.UtcNow,
        traceId = context.TraceIdentifier,
        path = context.Request.Path.Value,
        realmessage = "Gateway: Route not found."
    };
    
    return Results.Json(response, statusCode: StatusCodes.Status404NotFound);
});


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
