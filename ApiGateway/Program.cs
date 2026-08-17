using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;
using Spectre.Console;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

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
var jwtKey = builder.Configuration["Jwt:Key"] ?? "a-very-secret-key-that-is-long-enough-for-security";
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
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
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
            requestContext.ProxyRequest.Headers.Remove("X-User-Company-Id");

            var user = requestContext.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                // 게이트웨이가 검증한 JWT 클레임에서 정보 추출
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var role = user.FindFirst(ClaimTypes.Role)?.Value ?? "User";
                var companyId = user.FindFirst("CompanyId")?.Value;

                // 검증된 정보를 바탕으로 내부 전용 헤더 재생성
                if (!string.IsNullOrEmpty(userId))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Id", userId);
                }
                if (!string.IsNullOrEmpty(role))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Role", role);
                }
                if (!string.IsNullOrEmpty(companyId))
                {
                    requestContext.ProxyRequest.Headers.Add("X-User-Company-Id", companyId);
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

var app = builder.Build();
// 헬스체크 엔드포인트. 프로세스가 요청을 처리할 수 있는 상태인지만 보고한다.
app.MapHealthChecks("/health").AllowAnonymous();


app.UseCors("AllowFrontend");

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

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                var response = await client.GetAsync(probeUrl, cts.Token);
                httpStatus = (int)response.StatusCode;
                // /health 를 제공하지 않는 구버전이라도 응답 자체가 오면 프로세스는 살아 있다.
                status = response.IsSuccessStatusCode ? "UP" : "DEGRADED";
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
