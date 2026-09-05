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

    /// <summary>EMPTY 비어 있음 · USING 사용 중 (예약은 옛 시스템에서도 실사용 0건이라 이식하지 않았다 — 40번 문서)</summary>
    public string Status { get; set; } = "EMPTY";

    public string? DeceasedId { get; set; }

    /// <summary>고인 장례 상태 (DeceasedStatus 셋 중 하나). 사용 중일 때만 채운다.</summary>
    public string? DeceasedStatus { get; set; }
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

    /// <summary>사망 일시</summary>
    public DateTime? DeathDate { get; set; }

    /// <summary>입실 일시</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 빈 호실의 마지막 퇴실 일시 (옛 <c>t_room.last_feave_dt</c>).
    /// 전용 컬럼 없이 배정 이력의 마지막 <c>end_time</c> 으로 유도한다.
    /// </summary>
    public DateTime? LastVacatedAt { get; set; }

    /// <summary>
    /// 빈 호실에서 마지막으로 출상한 고인 — 출상 취소 진입점.
    /// 마지막 배정의 고인이 '출상 완료' 상태일 때만 채운다 (대시보드 전용).
    /// </summary>
    public string? LastDepartedDeceasedId { get; set; }
    public string? LastDepartedDeceasedName { get; set; }

    /// <summary>사용 일수</summary>
    public int UseDays { get; set; }

    /// <summary>이 호실에 붙은 장비 수</summary>
    public int DeviceCount { get; set; }

    /// <summary>그중 지금 살아 있는 장비 수</summary>
    public int OnlineDeviceCount { get; set; }

    /// <summary>
    /// 이 호실의 장비 목록. 대시보드(<c>/status/room-board</c>)만 채우고
    /// 나머지 현황 화면에는 null 로 나간다 — 필요한 화면만 골라 그린다.
    /// </summary>
    public List<DeviceDto>? Devices { get; set; }

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

/// <summary>
/// 빈소현황 대시보드(<c>/room_status</c>) 조회 조건.
/// </summary>
/// <remarks>
/// 예전에는 화면이 건물·호실·장비·고인 네 목록을 따로 받아 브라우저에서
/// 조인했다(47번 문서 0단계). 이 조건 하나로 서버가 다 붙여 준다.
/// 기간 이름은 실제로 거르는 컬럼을 그대로 말한다 — 옛
/// <c>DeceasedSearchDto.RoomEnter*</c> 가 실은 입관일을 거르던 혼동을 되풀이하지 않는다.
/// </remarks>
public class RoomBoardQueryDto
{
    public string? CompanyId { get; set; }
    public string? BuildingId { get; set; }
    public string? FloorId { get; set; }

    /// <summary>고인명 부분 일치</summary>
    public string? Name { get; set; }

    /// <summary>입관 일시(<c>funeral_date</c>) 범위</summary>
    public DateTime? CoffinStartDate { get; set; }
    public DateTime? CoffinEndDate { get; set; }

    /// <summary>발인 일시(<c>burial_date</c>) 범위</summary>
    public DateTime? BurialStartDate { get; set; }
    public DateTime? BurialEndDate { get; set; }

    /// <summary>
    /// 응답의 자세함. <c>full</c>(기본) 또는 <c>summary</c>.
    /// </summary>
    /// <remarks>
    /// 화면이 밀도 셋으로 갈리면서 생긴 칸이다(47번 문서 5단계).
    /// 시설 하나를 다루는 '운영' 밀도만 장비 목록·영정 사진·상주까지 쓰고,
    /// '감시'·'상황판' 은 호실 타일에 고인명과 발인 시각만 찍는다.
    /// 시설 스무 곳을 60초마다 다시 받을 때, 안 그리는 칸을 빼는 것만으로
    /// 장비 DTO 조립(속성·미디어명 조인)과 상주 조회가 통째로 빠진다.
    /// </remarks>
    public string? Detail { get; set; }
}

/// <summary>
/// 빈소현황 대시보드 응답. 호실(장비 포함)과 건물 공용 장비를 한 번에 준다.
/// </summary>
public class RoomBoardDto
{
    public List<FuneralStatusDto> Rooms { get; set; } = new();

    /// <summary>호실에 매이지 않은 건물 공용 장비 (입구 안내 등)</summary>
    public List<DeviceDto> CommonDevices { get; set; } = new();

    public FuneralStatusSummaryDto Summary { get; set; } = new();
}
