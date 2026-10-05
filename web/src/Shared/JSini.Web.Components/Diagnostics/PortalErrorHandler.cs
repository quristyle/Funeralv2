using System.Text;
using JSini.Web.Components.Data;
using JSini.Web.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Diagnostics;

/// <summary>
/// 미처리 예외를 <b>추적 번호와 함께 적어 두고</b> 그대로 흘려 보낸다.
/// </summary>
/// <remarks>
/// <para>
/// [어디에 끼는가]
/// </para>
///
/// <para>
/// <c>UseExceptionHandler("/error")</c> 앞이다. .NET 8 부터 등록된
/// <see cref="IExceptionHandler"/> 들이 먼저 돌고, 아무도 처리했다고 말하지
/// 않으면(=<c>false</c>) 그때 설정한 경로로 넘어간다. 그래서 이것은
/// <b>응답을 만들지 않는다</b> — 사용자가 보는 화면은 전과 똑같은
/// <c>Error.razor</c> 이고, 여기서는 기록만 한다.
/// </para>
///
/// <para>
/// [회로 안에서 난 예외는 여기 안 온다]
/// </para>
///
/// <para>
/// Blazor Server 의 대화형 화면에서 던진 예외는 HTTP 파이프라인이 아니라
/// 회로를 타고 죽는다. 그때 사용자가 보는 것은 오류 화면이 아니라 아래쪽
/// 「연결이 끊겼습니다」 막대이고, <b>추적 번호도 안 보인다</b>. 그 길은
/// 이 기능의 대상이 아니다 — 번호가 없으니 번호로 찾을 일도 없다.
/// 여기 걸리는 것은 정적 SSR(첫 그림 · 폼 제출 · 셸의 중계 경로)에서 난 것,
/// 즉 <b>사용자가 번호를 받아 든 바로 그 경우</b>다.
/// </para>
/// </remarks>
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

            // **`http.Request.Path` 를 그대로 쓰면 안 된다 — 전부 `/error` 가 된다.**
            //
            // UseExceptionHandler 는 처리기를 부르기 **전에** 요청 경로를
            // 오류 화면 경로로 갈아 끼운다(재실행 준비). 그래서 여기서 보는
            // 경로는 터진 화면이 아니라 언제나 `/error` 다 — 기록이 남기는
            // 하는데 「어느 화면에서 났나」가 모든 줄에서 똑같아져
            // 쓸모가 없어진다.
            //
            // 원래 경로는 이 기능이 들고 있다.
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

            // **콘솔에도 번호를 남긴다.** 기본 콘솔 로거는 추적 번호를 찍지
            // 않아서(IncludeScopes 가 꺼져 있다) 컨테이너 로그만 보면
            // 어느 줄이 어느 신고인지 짝지을 수가 없다.
            log.LogError(exception, "미처리 예외 — {Path} (추적 {Trace})",
                original ?? http.Request.Path.Value, number);
        }
        catch (Exception ex)
        {
            // 기록하다 터지면 원래 오류까지 묻힌다. 여기서 끝낸다.
            log.LogWarning(ex, "오류 기록을 남기지 못했다");
        }

        // **처리했다고 말하지 않는다.** 그래야 UseExceptionHandler 가
        // /error 로 넘겨 사용자에게 번호를 보여 준다.
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

    /// <summary>
    /// 안쪽 예외까지 펼쳐 한 덩어리로 만든다.
    /// </summary>
    /// <remarks>
    /// <c>ToString()</c> 하나로도 안쪽이 따라오지만, 깊이가 깊으면 같은 스택이
    /// 여러 번 되풀이되어 수십 KB 가 된다. 겹마다 <b>타입·메시지·스택 한 벌씩</b>만
    /// 적는다 — 읽는 사람이 실제로 보는 것이 그것이다.
    /// </remarks>
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
