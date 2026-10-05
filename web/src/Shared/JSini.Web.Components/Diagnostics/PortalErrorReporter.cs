using System.Net.Http.Json;
using System.Threading.Channels;
using JSini.Web.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Diagnostics;

/// <summary>
/// 잡은 오류를 <b>게이트웨이 너머 표로</b> 보낸다. 관리자가 추적 번호로 찾는 그 표다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 요청 안에서 바로 보내지 않나]
/// </para>
///
/// <para>
/// 보내는 자리가 <b>이미 뭔가 터진 요청 안</b>이다. 거기서 HTTP 호출을 기다리면
/// 셋을 한꺼번에 잃는다 —
/// </para>
///
/// <list type="bullet">
///   <item>오류 화면이 게이트웨이 왕복만큼 늦게 뜬다. 게이트웨이가 죽어 있으면
///         시간 제한이 끝날 때까지 흰 화면이다.</item>
///   <item>보내다 또 던지면 <b>오류 처리기 안에서 난 예외</b>라 아무 데도 안 잡힌다.</item>
///   <item>게이트웨이가 죽은 것이 원래 오류의 까닭일 때, 그 오류는 영영 기록되지 않는다.</item>
/// </list>
///
/// <para>
/// 그래서 큐에 넣고 바로 돌아온다. 실제 전송은 이 배경 작업이 한다.
/// </para>
///
/// <para>
/// [큐가 차면 버린다]
/// </para>
///
/// <para>
/// 상한이 있는 통이고 넘치면 <b>가장 오래된 것을 버린다</b>. 오류는 몰려서
/// 나기 때문이다 — 게이트웨이가 죽으면 모든 화면이 동시에 터지고, 그때 큐를
/// 무한정 늘리면 오류 기록이 포털을 먼저 죽인다. 버린 건수는 로그에 남는다.
/// </para>
/// </remarks>
public sealed class PortalErrorReporter : BackgroundService
{
    /// <summary>토큰을 붙이지 않는 전용 클라이언트의 이름.</summary>
    /// <remarks>
    /// <para>
    /// 게이트웨이 클라이언트(<c>GatewayClient</c>)를 쓰지 않는 이유가 둘이다.
    /// </para>
    ///
    /// <list type="bullet">
    ///   <item>그것은 <b>scoped</b> 다. 배경 작업에는 범위가 없고, 범위를 열어
    ///         봐야 거기에는 사용자도 <c>HttpContext</c> 도 없다.</item>
    ///   <item>오류는 <b>로그인 전에도</b> 난다. 토큰을 붙이는 길로 보내면
    ///         정작 가장 알고 싶은 오류가 401 로 떨어진다.</item>
    /// </list>
    /// </remarks>
    public const string HttpClientName = "JSini.PortalErrorReport";

    /// <summary>게이트웨이 경로. AuthServer 의 <c>/portal-errors</c> 다.</summary>
    private const string Endpoint = "auth/portal-errors";

    /// <summary>큐에 들고 있을 최대 건수.</summary>
    private const int Capacity = 200;

    private readonly Channel<PortalErrorDto> _queue = Channel.CreateBounded<PortalErrorDto>(
        new BoundedChannelOptions(Capacity)
        {
            // 새 것이 더 쓸모 있다 — 사용자가 방금 신고한 번호가 그것이다.
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private readonly IHttpClientFactory _clients;
    private readonly ILogger<PortalErrorReporter> _log;
    private readonly string? _token;
    private readonly bool _enabled;

    private int _dropped;

    public PortalErrorReporter(
        IHttpClientFactory clients,
        IConfiguration configuration,
        ILogger<PortalErrorReporter> log)
    {
        _clients = clients;
        _log = log;

        // AuthServer 의 `PortalError:Token` 과 **같은 값**이어야 한다.
        // 비어 있으면 서버도 검사하지 않는다(개발 장비).
        _token = configuration["ErrorReport:Token"];

        // 되돌리는 손잡이. 기록이 말썽이면 코드를 고치지 않고 끌 수 있다.
        _enabled = configuration.GetValue("ErrorReport:Enabled", true);
    }

    /// <summary>
    /// 한 건을 큐에 넣는다. <b>절대 던지지 않고 기다리지도 않는다</b> —
    /// 부르는 자리가 오류 처리기 안이다.
    /// </summary>
    public void Report(PortalErrorDto error)
    {
        if (!_enabled) return;

        if (!_queue.Writer.TryWrite(error))
        {
            // DropOldest 라 여기 올 일이 거의 없다(통이 닫혔을 때뿐).
            _dropped++;
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _log.LogInformation("포털 오류 기록이 꺼져 있다 (ErrorReport:Enabled=false)");
            return;
        }

        await foreach (var error in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await SendAsync(error, stoppingToken);
        }
    }

    /// <summary>
    /// 한 건을 보낸다. <b>실패해도 다시 큐에 넣지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// 되돌려 넣으면 게이트웨이가 죽어 있는 동안 같은 건이 무한히 돈다 —
    /// 그러면 되살아난 뒤 밀린 것을 보낼 여력도 없어진다. 한 번 더 해 보고
    /// (대개 순간적인 끊김이다) 그래도 안 되면 로그만 남기고 버린다.
    /// </remarks>
    private async Task SendAsync(PortalErrorDto error, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var client = _clients.CreateClient(HttpClientName);

                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = JsonContent.Create(error),
                };

                if (!string.IsNullOrWhiteSpace(_token))
                {
                    request.Headers.TryAddWithoutValidation("X-Portal-Error-Token", _token);
                }

                // 사용자가 보낸 요청의 추적 맥락을 그대로 물려받으면, 이 호출
                // 자체가 원래 오류와 같은 번호로 묶여 기록이 헷갈린다.
                // HttpClient 가 Activity.Current 를 보고 traceparent 를 붙이는데,
                // 배경 작업에는 Activity 가 없으므로 저절로 끊어진다.
                using var response = await client.SendAsync(request, ct);

                if (response.IsSuccessStatusCode) return;

                _log.LogWarning("오류 기록을 보내지 못했다 — {Status} (trace {TraceId})",
                    (int)response.StatusCode, error.TraceId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "오류 기록을 보내지 못했다 (trace {TraceId}, {Attempt}회째)",
                    error.TraceId, attempt);
            }

            if (attempt == 1)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        _dropped++;
        _log.LogError("오류 기록 한 건을 버렸다 (trace {TraceId}, 누적 {Dropped}건)",
            error.TraceId, _dropped);
    }
}
