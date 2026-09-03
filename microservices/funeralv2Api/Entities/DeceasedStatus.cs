namespace funeralv2Api.Entities;

/// <summary>
/// 고인 장례 상태의 정본. 저장은 이 세 값만 허용한다 (47번 문서 D-RS1).
/// </summary>
/// <remarks>
/// 이전에는 화면마다 다른 값(IN_HOSPITAL · DISCHARGED · SETTLEMENT_COMPLETED)을
/// 쓰고 있었고, 그 어긋남 때문에 빈소현황 카드의 출상 버튼이 else 가지로
/// '우연히' 노출되고 있었다. 발인 완료는 별도 상태가 아니라
/// <c>burial_date</c> 경과로 표현한다.
/// </remarks>
public static class DeceasedStatus
{
    /// <summary>장례 진행중 — 호실을 점유하는 유일한 상태</summary>
    public const string InProgress = "FUNERAL_IN_PROGRESS";

    /// <summary>출상 완료 — 호실 배정이 풀린 상태. 출상 취소로 되돌릴 수 있다</summary>
    public const string Departed = "FUNERAL_DEPARTURE_COMPLETED";

    /// <summary>장례 종료 — 정산까지 끝나 더 이상 다루지 않는 상태</summary>
    public const string Completed = "COMPLETED";

    /// <summary>저장을 허용하는 값인지</summary>
    public static bool IsValid(string? status) =>
        status is InProgress or Departed or Completed;

    /// <summary>
    /// 옛 코드값을 정본으로 바꾼다. 옛 화면·이관 데이터가 보내던 값을 받아 준다.
    /// (IN_HOSPITAL → 진행중 · DISCHARGED → 출상 · SETTLEMENT_COMPLETED → 종료)
    /// </summary>
    public static string Normalize(string? status) => status switch
    {
        "IN_HOSPITAL" => InProgress,
        "DISCHARGED" => Departed,
        "SETTLEMENT_COMPLETED" => Completed,
        null or "" => InProgress,
        _ => status,
    };

    /// <summary>
    /// 호실을 점유 중으로 치는 상태인지. 현황 화면·상황판·플레이어가
    /// 모두 이 하나를 기준으로 삼는다 — 점유 판정이 세 곳에서 달랐던 것을 모은 것.
    /// </summary>
    public static bool IsOccupying(string? status) => Normalize(status) == InProgress;
}
