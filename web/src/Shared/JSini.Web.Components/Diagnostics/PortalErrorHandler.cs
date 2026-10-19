using System.Text;
using JSini.Web.Components.Data;
using JSini.Web.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Diagnostics;

/// <summary>
/// 미처리 예외를 추적 번호와 함께 기록하고 그대로 흘려 보낸다. UseExceptionHandler("/error") 앞에 끼며, false 를 돌려 응답은 만들지 않는다.
/// 회로 안에서 난 예외는 HTTP 파이프라인이 아니라 회로로 죽어 추적 번호가 없으므로 여기 오지 않는다. 여기 걸리는 것은 정적 SSR 에서 난 것, 즉 사용자가 번호를 받아 든 경우다.
/// </summary>
public sealed class PortalErrorHandler(
    PortalErrorReporter reporter,
    ILogger<PortalErrorHandler> log) : IExceptionHandler
{
    /// <summary>스택을 몇 겹까지 펼칠지. 순환 참조로 무한히 도는 것을 막는다.</summary>
    private const int MaxInnerDepth = 8;

    /// <inheritdoc />
    public ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            var number = TraceNumber.Of(http);

            // UseExceptionHandler 는 처리기 호출 전에 요청 경로를 /error 로 갈아 끼운다. 원래 경로는 이 기능이 따로 들고 있다.
            var original = http.Features.Get<IExceptionHandlerPathFeature>()?.Path;

            reporter.Report(new PortalErrorDto
            {
                TraceId = TraceNumber.KeyOf(number) ?? string.Empty,
                Traceparent = number,
                OccurredAt = AppTime.UtcNow,
                Source = "portal",
                Path = original ?? http.Request.Path.Value,
                QueryString = http.Request.QueryString.HasValue ? http.Request.QueryString.Value : null,
                Method = http.Request.Method,

                // 로그인 쿠키의 이름 클레임이 로그인 아이디다(LoginService).
                UserId = http.User.Identity?.IsAuthenticated == true
                    ? http.User.Identity.Name
                    : null,

                Ip = ClientIp(http),
                UserAgent = http.Request.Headers.UserAgent.ToString(),
                ExceptionType = exception.GetType().FullName,
                Message = exception.Message,
                Detail = Unwrap(exception),
            });

            // 콘솔에도 번호를 남긴다. 기본 콘솔 로거는 추적 번호를 찍지 않아(IncludeScopes 꺼짐) 컨테이너 로그에서 짝을 찾을 수 없다.
            log.LogError(exception, "미처리 예외 — {Path} (추적 {Trace})",
                original ?? http.Request.Path.Value, number);
        }
        catch (Exception ex)
        {
            // 기록하다 터지면 원래 오류까지 묻힌다. 여기서 끝낸다.
            log.LogWarning(ex, "오류 기록을 남기지 못했다");
        }

        // 처리했다고 말하지 않는다. 그래야 UseExceptionHandler 가 /error 로 넘겨 번호를 보여 준다.
        return ValueTask.FromResult(false);
    }

    /// <summary>
    /// 포털은 nginx·게이트웨이 뒤다. 프록시가 넣어 준 첫 값이 사람의 주소다.
    /// </summary>
    private static string? ClientIp(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (first.Length > 0) return first;
        }

        return http.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>안쪽 예외까지 펼쳐 한 덩어리로 만든다. 겹마다 타입·메시지·스택 한 벌만 적는다(ToString 은 깊이가 깊으면 같은 스택이 되풀이되어 수십 KB 가 된다).</summary>
    private static string Unwrap(Exception exception)
    {
        var sb = new StringBuilder();

        for (var (ex, depth) = (exception, 0); ex is not null && depth < MaxInnerDepth; depth++)
        {
            if (depth > 0) sb.AppendLine().AppendLine("── 안쪽 예외 ──");

            sb.AppendLine($"{ex.GetType().FullName}: {ex.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace)) sb.AppendLine(ex.StackTrace);

            ex = ex.InnerException!;
        }

        return sb.ToString();
    }
}
