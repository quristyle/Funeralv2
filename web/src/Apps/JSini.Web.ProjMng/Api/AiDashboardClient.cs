using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>AI 작업 현황. 처리 건수는 프로젝트관리 서버가, CLI 한도는 실행기가(대시보드 응답에 실려), 공급자 한도는 AIAgentServer 가 응답 헤더에서 준다.
/// 두 한도는 다른 계정의 다른 값이라 화면이 한 칸에 뭉개지 않는다.</summary>
public sealed class AiDashboardClient(GatewayClient gateway)
{
    private const string Url = "projmng/ai-dashboard";

    /// <summary>대시보드 한 판을 한 번에 받는다. 조각마다 부르면 화면이 조각조각 채워져 「자료가 없다」로 읽힌다.</summary>
    public Task<AiDashboardData?> LoadAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (from is not null) query.Add($"from={from:yyyy-MM-dd}");
        if (to is not null) query.Add($"to={to:yyyy-MM-dd}");

        return gateway.GetOneAsync<AiDashboardData>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

    /// <summary>AI CLI 한도만. 곁들이는 값이라 실패는 삼킨다 — 한도를 못 읽는다고 지시 글상자가 안 뜨면 안 된다. 「빠른 지시」 고르개가 쓰며, 30일치 집계를 끌고 오지 않는다.</summary>
    public async Task<IReadOnlyList<AiUsageSnapshot>> UsageAsync(CancellationToken ct = default)
    {
        try
        {
            return await gateway.GetListAsync<AiUsageSnapshot>($"{Url}/usage", ct);
        }
        catch (ApiException)
        {
            return [];
        }
    }

    /// <summary>AI 공급자 한도. 곁들이는 값이라 못 읽어도 대시보드는 그린다.</summary>
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
    public List<AiUsageTrendPoint> UsageHourly { get; set; } = [];
    public List<AiUsageTrendPoint> UsageDaily { get; set; } = [];
    public List<AiUsageTrendPoint> UsageMonthly { get; set; } = [];
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
    public decimal TodayTotalSeconds { get; set; }
    public long TodayTokens { get; set; }
    public int TodayCalls { get; set; }
    public long PeriodTokens { get; set; }
    public int PeriodCalls { get; set; }

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
    public long TodayTokens { get; set; }
    public decimal TodayExpected { get; set; }
    public int ThisWeek { get; set; }
    public int ThisMonth { get; set; }
    public decimal MonthExpected { get; set; }
    public decimal? MonthOverMonth { get; set; }
}

/// <summary>AI CLI 한도의 마지막 스냅샷 한 칸. 퍼센트는 쓴 비율이다 — 남은 비율로 뒤집는 자리는 실행기 한 곳뿐이라 화면에서 또 뒤집지 않는다. CLI 하나가 여러 줄이 될 수 있다(BucketNm).</summary>
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

    /// <summary>로그인(갱신 토큰)이 끊기는 시각. 한도 갱신 시각과 다르다 — 사람이 그 장비에서 다시 로그인해야 풀린다.</summary>
    public DateTime? AuthExpiresAt { get; set; }

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

    /// <summary>시작 시각을 한국 시각으로. 그리드 칸은 DisplayFormat 에 옮길 자리가 없어 여기 옮긴 값을 따로 둔다 (StartedAt 은 UTC).</summary>
    public DateTime? StartedAtKst => StartedAt.KstTime();
}

// ── AI 공급자 (AIAgentServer) ──
// 관리 모듈의 AiProviderStatusDto 와 베낀다 — 업무 모듈끼리 참조하지 않는 것이 규칙이고, 두 화면이 서로 다른 칸을 본다.

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

/// <summary>공급자가 응답 헤더로 준 한도. 숫자가 아니라 글자로 들고 온다(1.2M · 59.9s) — 화면이 고쳐 적으면 원문과 어긋난다.</summary>
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

/// <summary>AI 모델 사용량 추이 한 점. 시간별·일별·월별이 같은 모양을 쓴다.</summary>
public sealed class AiUsageTrendPoint
{
    /// <summary>차트 축에 찍을 글자 — 시간별은 <c>HH:00</c>, 일별은 <c>MM-dd</c>, 월별은 <c>yyyy-MM</c>.</summary>
    public string Label { get; set; } = string.Empty;

    public DateTime Bucket { get; set; }

    /// <summary>Claude CLI 주간 사용량 비율(%).</summary>
    public decimal ClaudePct { get; set; }

    /// <summary>안티그래비티 (agy) 주간 사용량 비율(%).</summary>
    public decimal AntigravityPct { get; set; }

    /// <summary>Copilot CLI 주간 사용량 비율(%).</summary>
    public decimal CopilotPct { get; set; }

    /// <summary>모델 평균 주간 사용량 비율(%).</summary>
    public decimal AvgPct { get; set; }
}
