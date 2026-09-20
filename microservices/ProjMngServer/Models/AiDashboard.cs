namespace ProjMngServer.Models;

/// <summary>
/// AI 작업 대시보드 한 판 — <c>/api/ai-dashboard</c> 의 응답.
/// </summary>
/// <remarks>
/// <para>
/// <b>한 번에 다 준다.</b> 조각마다 주소를 두면 화면이 열릴 때 여덟 번
/// 왕복하고, 그중 하나가 늦으면 화면이 조각조각 채워진다 — 대시보드에서는
/// 그것이 「자료가 없다」로 읽힌다. 전부 같은 기간·같은 표를 훑는 집계라
/// 한 연결에서 끝내는 편이 서버에도 싸다.
/// </para>
/// <para>
/// <b>기간과 현재는 다른 값이다.</b> 집계는 고른 기간의 것이고,
/// <see cref="AiDashboardSummary.QueuedNow"/> 처럼 「지금」을 말하는 칸은
/// 기간을 타지 않는다. 섞으면 어제로 조회한 화면이 「실행 중 0건」이라고
/// 말하면서 실제로는 지금 돌고 있는 상황이 생긴다.
/// </para>
/// </remarks>
public sealed class AiDashboardData
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public AiDashboardSummary Summary { get; set; } = new();

    /// <summary>일별. 빈 날도 0 으로 채워 준다 — 차트에 구멍이 나면 추세가 거짓말을 한다.</summary>
    public List<AiDashboardPoint> Daily { get; set; } = [];

    /// <summary>월별. 기간과 무관하게 <b>최근 12개월</b>이다.</summary>
    public List<AiDashboardPoint> Monthly { get; set; } = [];

    /// <summary>시간대별 사용 빈도(0~23시). 24칸을 늘 채운다.</summary>
    public List<AiDashboardBucket> Hourly { get; set; } = [];

    /// <summary>요일별 사용 빈도(일~토). 7칸을 늘 채운다.</summary>
    public List<AiDashboardBucket> Weekday { get; set; } = [];

    /// <summary>실행 상태 분포.</summary>
    public List<AiDashboardSlice> ByStatus { get; set; } = [];

    /// <summary>실행기(CLI)별.</summary>
    public List<AiDashboardSlice> ByRunner { get; set; } = [];

    /// <summary>작업 대상별.</summary>
    public List<AiDashboardSlice> ByTarget { get; set; } = [];

    /// <summary>지시한 사람별.</summary>
    public List<AiDashboardSlice> ByRequester { get; set; } = [];

    /// <summary>처리시간 분포. 평균 하나로는 「대부분 1분, 하나가 3시간」을 못 본다.</summary>
    public List<AiDashboardSlice> ByDuration { get; set; } = [];

    public AiDashboardForecast Forecast { get; set; } = new();

    /// <summary>AI CLI 한도. 실행기가 <c>/usage</c> 로 읽어 올린 마지막 값이다.</summary>
    public List<AiUsageSnapshot> Usage { get; set; } = [];

    /// <summary>최근 실행. 「방금 무슨 일이 있었나」를 대시보드에서 바로 본다.</summary>
    public List<AiDashboardRecent> Recent { get; set; } = [];
}

/// <summary>머리 숫자들.</summary>
public sealed class AiDashboardSummary
{
    // ── 고른 기간 ───────────────────────────────────────────

    /// <summary>기간 안에 시작된 실행 건수. <b>지시 건수가 아니다</b> — 한 지시를 세 번 돌리면 3이다.</summary>
    public int Runs { get; set; }

    /// <summary>기간 안에 실행된 서로 다른 지시 건수.</summary>
    public int Tasks { get; set; }

    /// <summary>기간 안에 새로 등록된 지시 건수.</summary>
    public int CreatedTasks { get; set; }

    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Timeout { get; set; }
    public int Canceled { get; set; }

    /// <summary>실행기와 연락이 끊긴 것. <b>실패와 다른 값이다</b>(무엇이 일어났는지 모른다).</summary>
    public int Interrupted { get; set; }

    /// <summary>성공률(%). 끝난 것만 분모로 센다 — 아직 도는 것을 실패로 세면 낮게 보인다.</summary>
    public decimal SuccessRate { get; set; }

    /// <summary>재시도로 돈 실행(<c>seq &gt; 1</c>) 비율(%).</summary>
    public decimal RetryRate { get; set; }

    /// <summary>실제로 push 까지 간 건수. <b>그만큼 운영 배포가 일어났다.</b></summary>
    public int Pushed { get; set; }

    // ── 처리시간(초) ────────────────────────────────────────

    public decimal AvgSeconds { get; set; }

    /// <summary>중앙값. 평균이 긴 한 건에 끌려가는 것을 이것이 드러낸다.</summary>
    public decimal MedianSeconds { get; set; }

    public decimal P90Seconds { get; set; }
    public decimal MaxSeconds { get; set; }

    /// <summary>기간에 AI 가 돈 시간의 합.</summary>
    public decimal TotalSeconds { get; set; }

    // ── 지금 (기간을 타지 않는다) ───────────────────────────

    /// <summary>요청해 두고 아직 아무도 안 집은 것.</summary>
    public int QueuedNow { get; set; }

    /// <summary>지금 도는 중인 것.</summary>
    public int RunningNow { get; set; }

    /// <summary>끝났는데 사람이 아직 확인하지 않은 것.</summary>
    public int UnconfirmedNow { get; set; }

    /// <summary>마지막으로 살아 있다고 알린 실행기의 시각. 없으면 한 번도 붙은 적이 없다.</summary>
    public DateTime? RunnerSeenAt { get; set; }

    /// <summary>살아 있는 것으로 보이는 실행기 수(최근 5분).</summary>
    public int RunnersAlive { get; set; }
}

/// <summary>일별·월별 한 칸.</summary>
public sealed class AiDashboardPoint
{
    /// <summary>차트 축에 찍을 글자 — 일별은 <c>MM-dd</c>, 월별은 <c>yyyy-MM</c>.</summary>
    public string Label { get; set; } = string.Empty;

    public DateTime Bucket { get; set; }

    public int Runs { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }

    /// <summary>평균 처리시간(분). 차트에서 건수와 같이 그리려고 분으로 준다.</summary>
    public decimal AvgMinutes { get; set; }

    /// <summary>그날 AI 가 돈 시간의 합(분).</summary>
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

    /// <summary>평균 처리시간(분).</summary>
    public decimal AvgMinutes { get; set; }
}

/// <summary>
/// 예상 사용 건수.
/// </summary>
/// <remarks>
/// <b>지어내지 않는다.</b> 최근 며칠의 일평균을 그대로 늘려 잡은 것이고,
/// 무엇을 근거로 했는지(<see cref="BasisDays"/> · <see cref="BasisRuns"/>)를
/// 함께 준다 — 근거를 안 주면 화면의 숫자가 예언처럼 읽힌다.
/// </remarks>
public sealed class AiDashboardForecast
{
    /// <summary>근거 기간(일). 지금은 28일이다.</summary>
    public int BasisDays { get; set; }

    /// <summary>그 기간의 실행 건수.</summary>
    public int BasisRuns { get; set; }

    /// <summary>일평균 실행 건수.</summary>
    public decimal PerDay { get; set; }

    /// <summary>오늘 지금까지의 실행 건수.</summary>
    public int Today { get; set; }

    /// <summary>오늘 하루가 끝났을 때의 예상 건수. 오늘 실적과 남은 시간의 몫을 더한다.</summary>
    public decimal TodayExpected { get; set; }

    /// <summary>이번 주 실적.</summary>
    public int ThisWeek { get; set; }

    /// <summary>이번 달 실적.</summary>
    public int ThisMonth { get; set; }

    /// <summary>이번 달이 끝났을 때의 예상 건수.</summary>
    public decimal MonthExpected { get; set; }

    /// <summary>지난달 같은 기간 대비 증감(%). 근거가 없으면 <c>null</c>.</summary>
    public decimal? MonthOverMonth { get; set; }
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

    /// <summary>처리시간(초). 아직 안 끝났으면 비어 있다.</summary>
    public decimal? Seconds { get; set; }

    public string? ErrorSummary { get; set; }
}
