namespace ProjMngServer.Models;

/// <summary>AI CLI 한도의 마지막 스냅샷(projmng.ai_usage_snapshot). 실행기가 올리고 웹 서버는 읽기만 한다.</summary>
/// 퍼센트는 남은 비율이 아니라 쓴 비율이다 — CLI 가 그렇게 말하고 화면도 그대로 보여 준다.
public sealed class AiUsageSnapshot
{
    public long UsageKey { get; set; }

    /// <summary>어느 장비에서 읽었나.</summary>
    public string? RunnerNm { get; set; }

    /// <summary>어느 CLI 의 한도인가 — <c>claude</c> · <c>antigravity</c> · <c>copilot</c>.</summary>
    public string? RunnerKind { get; set; }

    /// <summary>같은 CLI 안에서 무엇의 한도인가. 하나뿐인 CLI 는 빈 값이다.</summary>
    /// CLI 하나가 한 줄이 아니다 — antigravity 는 모델군마다, copilot 은 한도 종류마다 따로 센다. 한 줄에 뭉개면 값이 주기마다 바뀐다.
    public string? BucketNm { get; set; }

    /// <summary>읽기에 성공했는가. <b>거짓이면 숫자가 비어 있고 <see cref="ErrorText"/> 에 이유가 있다.</b></summary>
    public bool Ok { get; set; } = true;

    /// <summary>세션 한도 사용률(%). claude 는 5시간 창이다.</summary>
    public decimal? SessionPct { get; set; }

    /// <summary>세션 한도가 다시 차는 시각.</summary>
    public DateTime? SessionResetAt { get; set; }

    /// <summary>주간 한도 사용률(%).</summary>
    public decimal? WeekPct { get; set; }

    public DateTime? WeekResetAt { get; set; }

    /// <summary>주간 한도 중 특정 모델만 따로 세는 칸(claude 의 Opus). 없으면 비운다.</summary>
    public decimal? WeekOpusPct { get; set; }

    public DateTime? WeekOpusResetAt { get; set; }

    /// <summary>월간 한도 사용률(%). copilot 처럼 달로 끊는 CLI 만 채운다.</summary>
    /// 주간 칸에 밀어 넣지 않는다 — 담으면 주간 화면이 다른 기간을 뜻하게 된다.
    public decimal? MonthPct { get; set; }

    public DateTime? MonthResetAt { get; set; }

    /// <summary>토큰(또는 크레딧)으로 한도를 주는 CLI 를 위한 칸.</summary>
    public long? LimitTokens { get; set; }

    public long? RemainingTokens { get; set; }

    /// <summary>요금제 이름(Max · Pro …).</summary>
    public string? PlanNm { get; set; }

    /// <summary>로그인(갱신 토큰)이 끊기는 시각. 한도 갱신과 달리 사람이 장비에서 다시 로그인해야 풀린다.</summary>
    /// 말해 주는 CLI 만 채우며, 비어 있는 것이 정상이다.
    public DateTime? AuthExpiresAt { get; set; }

    /// <summary>CLI 출력 원문. 가려낸 숫자가 전부가 아니다 — 출력이 바뀌었는지는 여기를 봐야 안다.</summary>
    public string? RawText { get; set; }

    public string? ErrorText { get; set; }

    /// <summary><b>언제 기준의 값인가.</b> 없으면 숫자를 믿을 수 없다.</summary>
    public DateTime ObservedAt { get; set; }
}
