using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// AI 작업 현황 — <c>projmng/ai-dashboard</c> · <c>ai/providers</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>두 군데를 본다.</b> 「얼마나 처리했나」는 프로젝트관리 서버가 자기
/// 자료를 세어 주고, 「AI 모델이 얼마나 남았나」는 두 곳에서 온다 —
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>작업지시가 쓰는 CLI</b>(claude · agy · copilot)의 한도는 실행기가
///     <c>/usage</c> 로 읽어 올린 것이고, 대시보드 응답에 함께 실려 온다.
///   </description></item>
///   <item><description>
///     <b>포털의 AI 도우미가 쓰는 공급자</b>의 한도는 AIAgentServer 가
///     응답 헤더에서 주워 둔 것이다(<c>ai/providers</c>). 따로 물어보면
///     그 호출이 한도를 깎으므로 지나가는 것을 줍는다.
///   </description></item>
/// </list>
/// <para>
/// 둘은 다른 계정의 다른 한도다. 화면이 한 칸에 뭉개지 않는다.
/// </para>
/// </remarks>
public sealed class AiDashboardClient(GatewayClient gateway)
{
    private const string Url = "projmng/ai-dashboard";

    /// <summary>
    /// 대시보드 한 판. <b>한 번에 다 온다</b> — 조각마다 부르면 화면이
    /// 조각조각 채워지고 그것이 「자료가 없다」로 읽힌다.
    /// </summary>
    public Task<AiDashboardData?> LoadAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (from is not null) query.Add($"from={from:yyyy-MM-dd}");
        if (to is not null) query.Add($"to={to:yyyy-MM-dd}");

        return gateway.GetOneAsync<AiDashboardData>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

    /// <summary>
    /// AI 공급자 한도. <b>곁들이는 값이다</b> — 못 읽어도 대시보드는 그린다.
    /// </summary>
    public async Task<AiProviderStatus?> ProvidersAsync(CancellationToken ct = default)
    {
        try
        {
            return await gateway.GetOneAsync<AiProviderStatus>("ai/providers", ct);
        }
        catch (ApiException)
        {
            return null;
        }
    }
}

/// <summary>대시보드 한 판.</summary>
public sealed class AiDashboardData
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    public AiDashboardSummary Summary { get; set; } = new();
    public List<AiDashboardPoint> Daily { get; set; } = [];
    public List<AiDashboardPoint> Monthly { get; set; } = [];
    public List<AiDashboardBucket> Hourly { get; set; } = [];
    public List<AiDashboardBucket> Weekday { get; set; } = [];
    public List<AiDashboardSlice> ByStatus { get; set; } = [];
    public List<AiDashboardSlice> ByRunner { get; set; } = [];
    public List<AiDashboardSlice> ByTarget { get; set; } = [];
    public List<AiDashboardSlice> ByRequester { get; set; } = [];
    public List<AiDashboardSlice> ByDuration { get; set; } = [];
    public AiDashboardForecast Forecast { get; set; } = new();
    public List<AiUsageSnapshot> Usage { get; set; } = [];
    public List<AiDashboardRecent> Recent { get; set; } = [];
}

/// <summary>머리 숫자들. <b>「지금」으로 끝나는 칸은 기간을 타지 않는다.</b></summary>
public sealed class AiDashboardSummary
{
    public int Runs { get; set; }
    public int Tasks { get; set; }
    public int CreatedTasks { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Timeout { get; set; }
    public int Canceled { get; set; }
    public int Interrupted { get; set; }
    public decimal SuccessRate { get; set; }
    public decimal RetryRate { get; set; }
    public int Pushed { get; set; }

    public decimal AvgSeconds { get; set; }
    public decimal MedianSeconds { get; set; }
    public decimal P90Seconds { get; set; }
    public decimal MaxSeconds { get; set; }
    public decimal TotalSeconds { get; set; }

    public int QueuedNow { get; set; }
    public int RunningNow { get; set; }
    public int UnconfirmedNow { get; set; }
    public DateTime? RunnerSeenAt { get; set; }
    public int RunnersAlive { get; set; }
}

/// <summary>일별·월별 한 칸.</summary>
public sealed class AiDashboardPoint
{
    public string Label { get; set; } = string.Empty;
    public DateTime Bucket { get; set; }
    public int Runs { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public decimal AvgMinutes { get; set; }
    public decimal TotalMinutes { get; set; }
}

/// <summary>시간대·요일 한 칸.</summary>
public sealed class AiDashboardBucket
{
    public int Slot { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Runs { get; set; }
}

/// <summary>「무엇별」 한 조각.</summary>
public sealed class AiDashboardSlice
{
    public string Label { get; set; } = string.Empty;
    public int Runs { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public decimal AvgMinutes { get; set; }
}

/// <summary>예상 사용 건수. <b>근거를 함께 들고 다닌다.</b></summary>
public sealed class AiDashboardForecast
{
    public int BasisDays { get; set; }
    public int BasisRuns { get; set; }
    public decimal PerDay { get; set; }
    public int Today { get; set; }
    public decimal TodayExpected { get; set; }
    public int ThisWeek { get; set; }
    public int ThisMonth { get; set; }
    public decimal MonthExpected { get; set; }
    public decimal? MonthOverMonth { get; set; }
}

/// <summary>
/// AI CLI 한도의 마지막 스냅샷 한 칸.
/// </summary>
/// <remarks>
/// <para>
/// <b>퍼센트는 「쓴 비율」이다.</b> 남은 비율이 아니다 — 화면도 그대로
/// 「사용 n%」로 보여 준다. <c>agy</c>·<c>copilot</c> 은 남은 비율로 말하지만
/// <b>뒤집는 자리는 실행기 한 곳뿐이다</b>(<c>UsageAgy</c>·<c>UsageCopilot</c>) —
/// 화면에서 또 뒤집으면 원문과 숫자가 어긋나는 순간을 아무도 못 잡는다.
/// </para>
/// <para>
/// <b>CLI 하나가 한 줄이 아니다.</b> <see cref="BucketNm"/> 로 갈린다.
/// </para>
/// </remarks>
public sealed class AiUsageSnapshot
{
    public long UsageKey { get; set; }
    public string? RunnerNm { get; set; }
    public string? RunnerKind { get; set; }

    /// <summary>같은 CLI 안에서 무엇의 한도인가. 하나뿐인 CLI 는 빈 글자다.</summary>
    public string? BucketNm { get; set; }

    public bool Ok { get; set; }
    public decimal? SessionPct { get; set; }
    public DateTime? SessionResetAt { get; set; }
    public decimal? WeekPct { get; set; }
    public DateTime? WeekResetAt { get; set; }
    public decimal? WeekOpusPct { get; set; }
    public DateTime? WeekOpusResetAt { get; set; }

    /// <summary>월간 한도 사용률(%). 달로 끊는 CLI(<c>copilot</c>)가 쓴다.</summary>
    public decimal? MonthPct { get; set; }

    public DateTime? MonthResetAt { get; set; }
    public long? LimitTokens { get; set; }
    public long? RemainingTokens { get; set; }
    public string? PlanNm { get; set; }
    public string? RawText { get; set; }
    public string? ErrorText { get; set; }
    public DateTime ObservedAt { get; set; }
}

/// <summary>최근 실행 한 줄.</summary>
public sealed class AiDashboardRecent
{
    public long RunKey { get; set; }
    public long TaskKey { get; set; }
    public int Seq { get; set; }
    public string? Title { get; set; }
    public string? RunStatus { get; set; }
    public string? RunnerKind { get; set; }
    public string? TargetNm { get; set; }
    public string? CreId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public decimal? Seconds { get; set; }
    public string? ErrorSummary { get; set; }
}

// ── AI 공급자 (AIAgentServer) ───────────────────────────────
//
// **관리 모듈에도 비슷한 것이 있다**(`AiProviderStatusDto`). 베끼는 것이
// 맞다 — 업무 모듈끼리 참조하지 않는 것이 이 저장소의 규칙이고, 두 화면이
// 같은 응답에서 서로 다른 칸을 본다(저쪽은 하루 한도, 이쪽은 남은 토큰과
// 갱신 시각). 세 번째가 생기면 그때 공유 자리로 올린다.

/// <summary><c>ai/providers</c> 응답.</summary>
public sealed class AiProviderStatus
{
    public string? DefaultProvider { get; set; }

    public List<AiProviderItem> Providers { get; set; } = [];
}

/// <summary>공급자 하나.</summary>
public sealed class AiProviderItem
{
    public string Key { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Model { get; set; }
    public bool Configured { get; set; }
    public bool IsDefault { get; set; }

    /// <summary>우리 쪽 하루 상한. 0 이면 제한 없음.</summary>
    public int MaxRequestsPerDay { get; set; }

    public int UsedToday { get; set; }

    /// <summary>공급자가 응답 헤더로 알려 준 마지막 값. <b>한 번도 안 불렀으면 없다.</b></summary>
    public AiProviderUsage? Usage { get; set; }
}

/// <summary>
/// 공급자가 응답 헤더로 준 한도.
/// </summary>
/// <remarks>
/// <b>숫자가 아니라 글자다.</b> 공급자가 <c>1.2M</c> · <c>59.9s</c> 처럼
/// 보내므로 서버도 받은 그대로 들고 온다 — 화면이 해석해서 고쳐 적으면
/// 원문과 어긋나는 순간을 아무도 못 잡는다.
/// </remarks>
public sealed class AiProviderUsage
{
    public int CallsOk { get; set; }
    public int CallsFailed { get; set; }
    public DateTime? LastCallAt { get; set; }
    public int? LastLatencyMs { get; set; }
    public string? LimitRequests { get; set; }
    public string? RemainingRequests { get; set; }
    public string? LimitTokens { get; set; }
    public string? RemainingTokens { get; set; }
    public string? ResetRequests { get; set; }
    public string? ResetTokens { get; set; }

    /// <summary><b>언제 기준의 값인가.</b> 없으면 숫자를 믿을 수 없다.</summary>
    public DateTime? ObservedAt { get; set; }
}
