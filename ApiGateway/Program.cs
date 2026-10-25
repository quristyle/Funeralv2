using Microsoft.AspNetCore.Http.Features;
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

// 로컬 개별 설정(Git 제외). 이 줄이 없으면 Local 키를 못 읽어 저장소의 옛 키로 검증한다(D1-B).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 환경변수는 맨 뒤에 둔다. 앞 줄의 Local.json 이 환경변수를 덮으면 「환경변수 > appsettings」 규칙이 뒤집힌다.
builder.Configuration.AddEnvironmentVariables();

// 요청 본문 상한. AI 첨부 다섯 장(장당 100MB)이 500MB 를 넘어 1GB 로 둔다. 게이트웨이는 본문을 모으지 않고 흘린다.
const long MaxBodyBytes = 1024L * 1024 * 1024; // 1GB

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxBodyBytes;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxBodyBytes;
    options.ValueLengthLimit = int.MaxValue;
});


// 1. JWT 검증 (1차). 서명 키는 appsettings.Local.json(Git 제외)에만 있고, 없으면 기동에 실패한다.
var jwtKey = JwtKeyGuard.Require(builder.Configuration, "Jwt:Key", "ApiGateway");
var key = Encoding.ASCII.GetBytes(jwtKey);

// 파일 읽기 경로에서만 쿠키를 신원으로 받는다. <img> 는 Authorization 헤더를 못 붙이므로 jsini_file_at 쿠키를 쓴다.
// 업로드·삭제 등에는 받지 않는다(CSRF). 쿠키 이름을 바꾸면 AuthServer 도 함께 바꾼다.
const string fileCookieName = "jsini_file_at";
// 파일 쿠키를 심었는지 확인하는 지문 쿠키(아래 자가 치유 참고).
const string fileCookieCheckName = "jsini_file_at_chk";
string[] fileCookiePaths =
[
    "/api/file/download",
    "/api/file/thumbnail",
    "/api/file/medium",
    "/api/file/large",
    "/api/file/resize"
];

// 앱알림 아이콘 열쇠는 브라우저가 쿠키 없이 가져가므로 파일 읽기 경로에서만 신원이다.
// 열쇠도 한 시간짜리 토큰이다. 글자를 바꾸면 알림 서비스 · 게이트웨이 · FileServer 세 곳을 함께 바꾼다.
const string iconTokenSubjectPrefix = "push-icon:";

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
            // 아이콘 열쇠는 파일 읽기 경로에서만 신원이다 (iconTokenSubjectPrefix 참고).
            OnTokenValidated = ctx =>
            {
                var subject = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (subject is not null
                    && subject.StartsWith(iconTokenSubjectPrefix, StringComparison.Ordinal))
                {
                    var path = ctx.Request.Path.Value ?? string.Empty;
                    var readable = fileCookiePaths.Any(p =>
                        path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                    if (!readable)
                    {
                        ctx.Fail("알림 아이콘 열쇠는 파일 읽기 경로에서만 쓸 수 있습니다.");
                    }
                }

                return Task.CompletedTask;
            },

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
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
                var companyId = user.FindFirst("CompanyId")?.Value;
                var userName = user.FindFirst("RealName")?.Value ?? user.FindFirst(ClaimTypes.Name)?.Value;
                var email = user.FindFirst(ClaimTypes.Email)?.Value;
                // 이관 계정은 아이디에 접두어가 붙어(pm_jskim) 아이디만으로는 각 서비스가 사용자를 못 찾는다.
                // 계정이 온 MSA 레코드(<서비스>:<테이블>:<원본키>)를 함께 싣는다.
                var msaSource = user.FindFirst("MsaSource")?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Id", userId);
                }

                // X-User-Role 은 단수라 여러 역할을 못 담는다. 기존 서비스 호환을 위해 첫 역할을 두고, 전체는 X-User-Roles 로 보낸다.
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

                // 이름은 한글이라 헤더(Latin-1)에서 깨지므로 URL 인코딩해 보내고, 받는 쪽이 Uri.UnescapeDataString 으로 되돌린다.
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

// 헬스체크 대상(LB · liveness). 인증 없이 열어 둔다.
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

// 레이트 리미팅 — 로그인·비밀번호 초기화 경로에 IP 단위 창(1분 10회)을 건다. 라우트에서 RateLimiterPolicy 로 지정한다.
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

    // 로그인 없이 쓰는 경로(소개 사이트 문의)는 분당 3회로 조인다. 캡차 대신 허니팟과 함께 쓰며 외부 스크립트는 부르지 않는다(D-S4).
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

    // 서비스워커의 「푸시가 닿았다」 보고(익명). 쪽지 메일 전환이 이 보고로 보냈다/닿았다를 가른다.
    // 거절되면 멀쩡히 받은 사람에게 메일이 또 가므로 문의보다 느슨하다(분당 60). 절전 해제로 몰리는 푸시와 NAT 공유 IP 를 고려했다.
    options.AddPolicy("push-ack", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 플레이어가 부르는 익명 읽기 경로. 열쇠가 추측 가능한 장비 코드뿐이라 요청 수를 제한한다(분당 900, 장례식장 단위 — NAT 공유 IP 고려).
    // 코드 대입 자체는 막지 못한다. 근본 대책은 아래 장비 토큰(D-M1)이다. SignalR 허브에는 걸지 않는다(거부되면 밀어주기가 끊긴다).
    options.AddPolicy("player-read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 900,
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

// Gmail 보관함의 mbox 올리기만 본문 상한을 푼다. Takeout 은 메일함 하나가 GB
// 단위라 위의 1GB 로도 모자란다. 게이트웨이는 본문을 모으지 않고 흘리므로
// 상한을 풀어도 여기서 쌓이는 것은 없다 — 받는 쪽(AIAgentServer)이 디스크로
// 바로 붓는다. **다른 길은 그대로 1GB 다.**
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/ai/gmail/import/upload",
            StringComparison.OrdinalIgnoreCase)
        && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
    {
        limit.MaxRequestBodySize = null;
    }

    await next();
});

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

// 플레이어 익명 경로의 장비 토큰 검증(D-M1, 기본 꺼짐). 켜면 X-Device-Token(웹소켓은 ?deviceToken=)이 PlayerAuth:DeviceTokens 목록과 맞아야 통과한다.
// 켜는 순서: 플레이어 v1.0.1 이상 배포 → 모든 장비에 키 입력 확인 → Local.json 에 DeviceTokens 입력 후 RequireDeviceToken 을 켠다. 순서가 틀리면 현장 화면이 꺼진다.
// 막을 때는 401 이 아니라 404 다(장비 코드의 존재를 알리지 않는다). JWT 로그인 사용자는 토큰 없이 통과한다. 목록 방식은 개별 토큰(DB)으로 가기 전의 임시 형태다.
var playerAuthSection = app.Configuration.GetSection("PlayerAuth");
string[] playerTokenPaths = playerAuthSection.GetSection("Paths").Get<string[]>() ?? new[]
{
    "/api/funeral/building/device/code/",
    "/api/funeral/building/deceased/deviceCode/",
    "/api/funeral/building/deceased/guide/deviceCode/",
    "/api/funeral/building/deceased/kiosk/deviceCode/",
    "/api/funeral/building/source/",
    "/api/funeral/hubs/device/"
};

app.Use(async (context, next) =>
{
    // 요청마다 설정을 다시 읽어 Local.json 변경을 재기동 없이 반영한다.
    if (!app.Configuration.GetValue("PlayerAuth:RequireDeviceToken", false))
    {
        await next();
        return;
    }

    var path = context.Request.Path.Value ?? string.Empty;
    if (!playerTokenPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
    {
        await next();
        return;
    }

    // 로그인 사용자는 통과 (JWT 는 위의 인증 미들웨어가 이미 검증했다)
    if (context.User.Identity?.IsAuthenticated == true)
    {
        await next();
        return;
    }

    var supplied = context.Request.Headers["X-Device-Token"].ToString();
    if (string.IsNullOrEmpty(supplied))
    {
        // SignalR 웹소켓 업그레이드는 커스텀 헤더를 싣기 어려워 쿼리로도 받는다.
        supplied = context.Request.Query["deviceToken"].ToString();
    }

    var allowed = app.Configuration.GetSection("PlayerAuth:DeviceTokens").Get<string[]>() ?? Array.Empty<string>();
    if (!string.IsNullOrEmpty(supplied) && allowed.Contains(supplied, StringComparer.Ordinal))
    {
        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        success = false,
        code = "404",
        message = "요청한 자원을 찾을 수 없습니다.",
        data = (object?)null,
        timestamp = DateTime.UtcNow
    });
});

// 파일 쿠키(jsini_file_at) 자가 치유. 로그인 때만 심는 구조라 쿠키가 없는 옛 세션(토큰은 유효)의 이미지가 익명 404 가 되는 것을 막는다.
// 검증된 Authorization 토큰이면 쿠키를 다시 심는다. 매번 심지 않도록 Path=/ 의 SHA-256 지문 쿠키로 같은 토큰인지 보며, 지문으로는 토큰을 되만들 수 없다.
// 로그인 때의 심기(AuthServer)는 그대로 둔다. 로그인 직후 첫 이미지는 API 호출 없이 나와야 하기 때문이다.
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers.Authorization.ToString();

    // 알림 아이콘 열쇠는 심지 않는다. 한 장짜리 열쇠가 로그인 신원 자리에 앉는 것을 막는다(셸 HttpClient 는 쿠키 통을 끈다).
    var subject = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var isIconToken = subject is not null
        && subject.StartsWith(iconTokenSubjectPrefix, StringComparison.Ordinal);

    if (context.User.Identity?.IsAuthenticated == true
        && !isIconToken
        && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var token = authHeader["Bearer ".Length..].Trim();
        if (token.Length > 0)
        {
            var fingerprint = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token)));

            context.Request.Cookies.TryGetValue(fileCookieCheckName, out var planted);
            if (!string.Equals(planted, fingerprint, StringComparison.Ordinal))
            {
                // 만료는 토큰과 같게 둔다(exp 는 유닉스 초). 없으면 세션 쿠키로 둔다.
                DateTimeOffset? expires = null;
                if (long.TryParse(context.User.FindFirst("exp")?.Value, out var expUnix))
                {
                    expires = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                }

                // 옵션(특히 Path)은 AuthServer 가 심을 때와 같아야 한다. 다르면 다른 쿠키가 된다.
                var secure = !app.Environment.IsDevelopment();
                context.Response.Cookies.Append(fileCookieName, token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = secure,
                    SameSite = SameSiteMode.Lax,
                    Path = "/api/file",
                    Expires = expires
                });
                context.Response.Cookies.Append(fileCookieCheckName, fingerprint, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = secure,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    Expires = expires
                });
            }
        }
    }

    await next();
});

// 비밀번호 사용 기간(Auth:PasswordExpiryDays) 만료 차단. 화면 안내만으로는 API 직접 호출을 못 막아 여기서 막는다.
// 토큰의 PwdChangedAt 시각 클레임으로 매 요청 계산한다. 불린을 실으면 토큰 수명 중 만료되는 구간을 놓친다. 0 이면 끈다.
// 비밀번호를 바꾸는 데 필요한 경로만 열어 둔다.
var passwordExpiryDays = builder.Configuration.GetValue<int?>("Auth:PasswordExpiryDays") ?? 90;

// 만료 상태에서도 통과시키는 경로. 앞부분이 일치하면 통과한다(대소문자 무시).
var passwordExpiryAllowList = new[]
{
    "/api/auth/login",                   // 익명 경로지만 명시해 둔다
    "/api/auth/logout",                  // 잠긴 상태에서 나갈 길은 항상 열려 있어야 한다
    "/api/auth/user/change-password",    // 이 차단을 푸는 유일한 방법
    "/api/auth/password",                // 비밀번호 찾기(익명 경로지만, 로그인한 채로 열 수 있다)
    "/api/auth/user/verify-password",    // 잠금화면을 푸는 길은 잠긴 상태에서도 열려 있어야 한다
    "/api/auth/webauthn/verify",         // 같은 잠금화면을 지문으로 푸는 길 (…/verify · …/verify/options)
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
        // 클레임이 없으면 기준을 모르므로 잠그지 않는다(칸을 막 만든 직후 전원이 갇히는 것을 막는다).
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

// 서버 상태 모니터링. 게이트웨이가 아는 클러스터를 요청 시점에 직접 찔러 모은다(브라우저는 CORS·내부망 때문에 각 서비스를 못 부른다).
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
                // LLM 처럼 딸린 것을 점검하는 서비스는 응답이 더 걸리므로 서비스 쪽 점검 타임아웃(3초)보다 넉넉히 기다린다.
                cts.CancelAfter(TimeSpan.FromSeconds(6));

                var response = await client.GetAsync(probeUrl, cts.Token);
                httpStatus = (int)response.StatusCode;
                // /health 가 없는 구버전은 응답이 오기만 하면 살아 있는 것으로 본다.
                status = response.IsSuccessStatusCode ? "UP" : "DEGRADED";

                // 상태 코드만으로는 부족하다. 프로세스가 살아 있으면 200 이라 LLM 이 꺼진 AIAgentServer 도 UP 으로 보였다.
                // 본문의 점검 결과(JSini.Shared.Infrastructure/HealthChecks)를 읽어 올리기만 하고, 판정 기준은 각 서비스에 둔다.
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                var parsed = HealthBody.Parse(body);
                if (parsed is not null)
                {
                    dependencies = parsed.Dependencies;
                    reason = parsed.Reason;

                    // 서비스가 Degraded 라고 하면 그 말을 따른다(살아 있으나 제 일을 못 함).
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

    var color = serverName.ToUpper() switch
    {
        "GATEWAY" => Color.Cyan,
        "FUNERALV2" => Color.Green,
        "PAYMENT" => Color.Yellow,
        "AUTH" => Color.Magenta,
        _ => Color.White
    };

    var envColor = env switch
    {
        "Development" => Color.Green,
        "Staging" => Color.Yellow,
        "Production" => Color.Red,
        _ => Color.Grey
    };

    AnsiConsole.Write(
        new FigletText(serverName + "")
            .Color(color)
            .Centered());

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

    AnsiConsole.Write(
        new Rule($"[bold {color}]READY[/]")
            .RuleStyle(color.ToString())
            .Centered());
});


app.Run();
