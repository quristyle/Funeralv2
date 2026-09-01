namespace funeralv2Api.DTOs;

/// <summary>
/// 빈소 한 칸의 현황. 빈소 정보 · 빈소 현황 · 고인 현황 · 심플 · 모바일이 모두 이것을 쓴다.
/// </summary>
/// <remarks>
/// 옛 <c>page/monitor/room_status.jsp</c> · <c>room_status_simple.jsp</c> ·
/// <c>mobile/room_status.jsp</c> 가 각자 프로시저를 부르고 각자 그렸는데,
/// 담는 내용은 같았다. 여기서는 하나로 모으고 화면이 골라 그린다.
/// </remarks>
public class FuneralStatusDto
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;

    /// <summary>호실 짧은 명칭 (옛 <c>s_nm</c>). 좁은 화면에서 쓴다.</summary>
    public string? RoomShortName { get; set; }

    public string? FloorId { get; set; }
    public string? FloorName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }
    public int SortOrder { get; set; }

    /// <summary>EMPTY 비어 있음 · USING 사용 중 · RESERVED 예약</summary>
    public string Status { get; set; } = "EMPTY";

    public string? DeceasedId { get; set; }
    public string? DeceasedName { get; set; }
    public string? DeceasedGender { get; set; }
    public int? DeceasedAge { get; set; }
    public string? Religion { get; set; }

    /// <summary>영정 사진</summary>
    public string? PhotoFileId { get; set; }
    public string? PhotoUrl { get; set; }

    /// <summary>상주 (옛 <c>sangju</c>). 여러 명이면 쉼표로 잇는다.</summary>
    public string? ChiefMourner { get; set; }

    /// <summary>입관 일시 (옛 <c>layout_corpse_dt</c>)</summary>
    public DateTime? CoffinTime { get; set; }

    /// <summary>발인 일시 (옛 <c>borne_out_dt</c>)</summary>
    public DateTime? DischargeTime { get; set; }

    /// <summary>장지 (옛 <c>jangji</c>)</summary>
    public string? BurialPlace { get; set; }

    /// <summary>입실 일시</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>사용 일수</summary>
    public int UseDays { get; set; }

    /// <summary>이 호실에 붙은 장비 수</summary>
    public int DeviceCount { get; set; }

    /// <summary>그중 지금 살아 있는 장비 수</summary>
    public int OnlineDeviceCount { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 현황 화면 위에 얹는 요약 숫자.
/// </summary>
public class FuneralStatusSummaryDto
{
    public int TotalRooms { get; set; }
    public int UsingRooms { get; set; }
    public int EmptyRooms { get; set; }
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
}

/// <summary>
/// 현황 목록과 요약을 함께 돌려준다 — 화면이 두 번 부르지 않도록.
/// </summary>
public class FuneralStatusBoardDto
{
    public List<FuneralStatusDto> Rooms { get; set; } = new();
    public FuneralStatusSummaryDto Summary { get; set; } = new();
}
