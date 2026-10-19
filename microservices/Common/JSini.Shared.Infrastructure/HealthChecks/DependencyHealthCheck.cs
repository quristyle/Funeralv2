using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JSini.Shared.Infrastructure.HealthChecks;

/// <summary>
/// 딸린 것(DB · 큐 · 저장소)을 점검하는 범용 점검. 타임아웃과 결과 캐시(폴링이 DB 를 두들기지 않게)를 여기서 한 번만 쓴다.
/// Degraded 로 볼지 Unhealthy 로 볼지는 부르는 쪽이 정한다 — 일부 기능만 막히면 Degraded.
/// </summary>
public sealed class DependencyHealthCheck : IHealthCheck
{
    private readonly string _name;
    private readonly Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>> _probe;
    private readonly IServiceProvider _services;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _cacheFor;

    // 점검 이름별로 결과를 나눠 담는다. 한 서비스가 여러 개를 등록할 수 있다.
    private static readonly Dictionary<string, (HealthCheckResult Result, DateTimeOffset At)> Cache = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public DependencyHealthCheck(
        string name,
        Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>> probe,
        IServiceProvider services,
        TimeSpan timeout,
        TimeSpan cacheFor)
    {
        _name = name;
        _probe = probe;
        _services = services;
        _timeout = timeout;
        _cacheFor = cacheFor;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (Cache.TryGetValue(_name, out var hit)
                && DateTimeOffset.UtcNow - hit.At < _cacheFor)
            {
                return hit.Result;
            }

            HealthCheckResult result;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);
                result = await _probe(_services, cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = HealthCheckResult.Unhealthy(
                    $"{_timeout.TotalSeconds:0}초 안에 응답하지 않습니다.");
            }
            catch (Exception ex)
            {
                result = HealthCheckResult.Unhealthy(
                    $"연결할 수 없습니다. ({ex.GetBaseException().Message})");
            }

            Cache[_name] = (result, DateTimeOffset.UtcNow);
            return result;
        }
        finally
        {
            Gate.Release();
        }
    }
}

/// <summary>
/// <see cref="DependencyHealthCheck"/> 등록 도우미.
/// </summary>
public static class DependencyHealthCheckExtensions
{
    /// <summary>
    /// 딸린 것 점검을 등록한다. <c>dependency</c> 태그가 자동으로 붙어
    /// 상태 화면이 '연결 대상' 줄로 보여 준다.
    /// </summary>
    /// <param name="name">화면에 보일 점검 이름 (예: <c>database</c>)</param>
    /// <param name="probe">실제 점검. 두 번째 인자는 타임아웃이 걸린 토큰이다.</param>
    /// <param name="timeoutSeconds">점검 한 번에 허용할 시간</param>
    /// <param name="cacheSeconds">같은 결과를 재사용할 시간</param>
    public static IHealthChecksBuilder AddDependencyCheck(
        this IHealthChecksBuilder builder,
        string name,
        Func<IServiceProvider, CancellationToken, Task<HealthCheckResult>> probe,
        int timeoutSeconds = 3,
        int cacheSeconds = 30)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new DependencyHealthCheck(
                name, probe, sp,
                TimeSpan.FromSeconds(timeoutSeconds),
                TimeSpan.FromSeconds(cacheSeconds)),
            failureStatus: null,
            tags: new[] { HealthCheckJson.DependencyTag }));
    }
}
