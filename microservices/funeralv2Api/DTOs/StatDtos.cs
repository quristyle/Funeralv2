namespace funeralv2Api.DTOs;

/// <summary>
/// 과금 내역 한 줄. 고인 한 명이 쓴 시설 비용을 모아 놓은 것이다.
/// </summary>
/// <remarks>
/// 옛 시스템은 <c>t_goin_pay</c> 에 고인 한 명당 세 줄(기본료·환경부담금·시설관리비)을
/// 두고 <c>gp_day_apply</c> 가 켜져 있으면 사용일수를 곱했다.
/// 지금은 <c>smfr.deceased_facilities</c> 가 같은 자리를 맡는다.
/// </remarks>
public class BillingDto
{
    public string DeceasedId { get; set; } = string.Empty;
    public string DeceasedName { get; set; } = string.Empty;

    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>사용 일수. 하루 미만도 하루로 센다 (옛 규칙).</summary>
    public int UseDays { get; set; }

    /// <summary>비용 항목들</summary>
    public List<BillingItemDto> Items { get; set; } = new();

    /// <summary>항목 합계</summary>
    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 과금 항목 한 줄 (옛 <c>t_goin_pay</c> 한 행).
/// </summary>
public class BillingItemDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>항목명 (옛 <c>gp_title</c>) — 기본료 · 환경부담금 · 시설관리비 등</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>단가 (옛 <c>gp_pay</c>)</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>사용일수를 곱하는 항목인지 (옛 <c>gp_day_apply</c>)</summary>
    public bool ApplyPerDay { get; set; }

    /// <summary>곱한 뒤 금액</summary>
    public decimal Amount { get; set; }

    /// <summary>비고 (옛 <c>gp_comment</c>)</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 빈소 사용 내역 한 줄.
/// </summary>
public class RoomUsageDto
{
    public string Id { get; set; } = string.Empty;

    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string? FloorName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }

    public string DeceasedId { get; set; } = string.Empty;
    public string DeceasedName { get; set; } = string.Empty;

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int UseDays { get; set; }

    /// <summary>이 사용 건의 비용 합계</summary>
    public decimal BillingAmount { get; set; }

    public bool InUse { get; set; }
}

/// <summary>
/// 과금·사용 내역 화면 위에 얹는 요약 숫자.
/// </summary>
public class StatSummaryDto
{
    public int DeceasedCount { get; set; }
    public int RoomUsageCount { get; set; }
    public int TotalUseDays { get; set; }
    public decimal TotalAmount { get; set; }
}
