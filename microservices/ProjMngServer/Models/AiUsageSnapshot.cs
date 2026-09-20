namespace ProjMngServer.Models;

/// <summary>
/// AI CLI 한도의 마지막 스냅샷 — <c>projmng.ai_usage_snapshot</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>실행기가 읽어 올린다.</b> `/usage` 를 아는 것은 CLI 뿐이고 그 CLI 는
/// 실행기 장비에만 있다. 웹 서버는 이 표를 읽기만 한다.
/// </para>
/// <para>
/// <b>퍼센트는 「쓴 비율」이다.</b> 남은 비율이 아니다 — CLI 가 그렇게
/// 말하기 때문이고, 화면도 그대로 「사용 n%」로 보여 준다. 뒤집어 적으면
/// 원문과 화면이 어긋나 무엇이 맞는지 알 수 없어진다.
/// </para>
/// </remarks>
public sealed class AiUsageSnapshot
{
    public long UsageKey { get; set; }

    /// <summary>어느 장비에서 읽었나.</summary>
    public string? RunnerNm { get; set; }

    /// <summary>어느 CLI 의 한도인가 — <c>claude</c> · <c>antigravity</c> · <c>copilot</c>.</summary>
    public string? RunnerKind { get; set; }

    /// <summary>
    /// 읽기에 성공했는가. <b>거짓이면 숫자가 비어 있고 <see cref="ErrorText"/> 에 이유가 있다.</b>
    /// </summary>
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

    /// <summary>토큰으로 한도를 주는 CLI 를 위한 칸.</summary>
    public long? LimitTokens { get; set; }

    public long? RemainingTokens { get; set; }

    /// <summary>요금제 이름(Max · Pro …).</summary>
    public string? PlanNm { get; set; }

    /// <summary>
    /// CLI 출력 원문. <b>가려낸 숫자가 전부가 아니다</b> — 출력 형식이 바뀌면
    /// 여기를 봐야 무엇이 바뀌었는지 안다.
    /// </summary>
    public string? RawText { get; set; }

    public string? ErrorText { get; set; }

    /// <summary><b>언제 기준의 값인가.</b> 없으면 숫자를 믿을 수 없다.</summary>
    public DateTime ObservedAt { get; set; }
}
