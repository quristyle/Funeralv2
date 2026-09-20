namespace AiTaskRunner;

/// <summary>
/// 서버로 올리는 사용량 보고. <b>칸 이름이 서버의 <c>AiUsageReport</c> 와 같아야 한다.</b>
/// </summary>
/// <remarks>
/// 어긋나도 예외가 안 난다 — 서버가 못 읽은 칸을 기본값으로 두고 지나가므로,
/// 증상이 「보고는 갔다는데 화면이 비어 있다」로만 보인다.
/// </remarks>
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

/// <summary>CLI 하나의 한도.</summary>
public sealed class AiUsageItem
{
    public string? RunnerKind { get; set; }

    /// <summary>읽기에 성공했는가. 거짓이면 <see cref="ErrorText"/> 에 이유가 있다.</summary>
    public bool Ok { get; set; } = true;

    /// <summary>세션 한도 사용률(%). <b>쓴 비율이다</b> — 남은 비율이 아니다.</summary>
    public decimal? SessionPct { get; set; }

    public DateTime? SessionResetAt { get; set; }

    public decimal? WeekPct { get; set; }

    public DateTime? WeekResetAt { get; set; }

    public decimal? WeekOpusPct { get; set; }

    public DateTime? WeekOpusResetAt { get; set; }

    public long? LimitTokens { get; set; }

    public long? RemainingTokens { get; set; }

    public string? PlanNm { get; set; }

    /// <summary>CLI 출력 원문. <b>늘 함께 올린다</b> — 형식이 바뀌면 이것만이 단서다.</summary>
    public string? RawText { get; set; }

    public string? ErrorText { get; set; }
}
