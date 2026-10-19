namespace AiTaskRunner;

/// <summary>
/// 서버로 올리는 사용량 보고. 칸 이름이 서버의 AiUsageReport 와 같아야 한다 — 어긋나도 예외 없이 기본값으로 지나가 「보고는 갔는데 화면이 비어 있다」로 보인다.
/// </summary>
public sealed class AiUsageReport
{
    public string RunnerName { get; set; } = string.Empty;

    public string? HostName { get; set; }

    public string? RunnerVersion { get; set; }

    /// <summary>이 장비가 돌릴 수 있는 CLI.</summary>
    public List<string> Kinds { get; set; } = [];

    /// <summary>
    /// 읽어 온 한도들. <b>비어 있어도 보낸다</b> — 이 요청에는 「장비가 살아
    /// 있다」는 뜻이 함께 실려 있다.
    /// </summary>
    public List<AiUsageItem> Items { get; set; } = [];
}

/// <summary>한도 한 칸. CLI 하나가 한 줄이 아니다 — agy 는 모델군마다, copilot 은 한도 종류마다 따로 센다(BucketNm 으로 가른다).</summary>
public sealed class AiUsageItem
{
    public string? RunnerKind { get; set; }

    /// <summary>같은 CLI 안에서 무엇의 한도인가. 하나뿐인 CLI 는 비운다.</summary>
    public string? BucketNm { get; set; }

    /// <summary>읽기에 성공했는가. 거짓이면 <see cref="ErrorText"/> 에 이유가 있다.</summary>
    public bool Ok { get; set; } = true;

    /// <summary>세션 한도 사용률(%). <b>쓴 비율이다</b> — 남은 비율이 아니다.</summary>
    public decimal? SessionPct { get; set; }

    public DateTime? SessionResetAt { get; set; }

    public decimal? WeekPct { get; set; }

    public DateTime? WeekResetAt { get; set; }

    public decimal? WeekOpusPct { get; set; }

    public DateTime? WeekOpusResetAt { get; set; }

    /// <summary>월간 한도 사용률(%). <c>copilot</c> 처럼 달로 끊는 CLI 가 쓴다.</summary>
    public decimal? MonthPct { get; set; }

    public DateTime? MonthResetAt { get; set; }

    public long? LimitTokens { get; set; }

    public long? RemainingTokens { get; set; }

    public string? PlanNm { get; set; }

    /// <summary>로그인(갱신 토큰)이 끊기는 시각. 한도 갱신 시각과 다르다 — 사람이 다시 로그인해야 풀린다. 말해 주는 CLI 만 채운다.</summary>
    public DateTime? AuthExpiresAt { get; set; }

    /// <summary>CLI 출력 원문. <b>늘 함께 올린다</b> — 형식이 바뀌면 이것만이 단서다.</summary>
    public string? RawText { get; set; }

    public string? ErrorText { get; set; }
}
