namespace funeralv2Api.DTOs;

// 알림정보 DTO 셋(NoticeDto · NoticeCreateDto · NoticeUpdateDto)은 2026-09-03 에
// 걷어냈다 — 쓰지 않는 화면이었다 (Endpoints/InfoEndpoints.cs 머리말).

/// <summary>
/// 호실 히스토리 한 줄. 옛 <c>fr.room_goin.list4room</c> 이 돌려주던 모양이다.
/// </summary>
public class RoomHistoryDto
{
    public string Id { get; set; } = string.Empty;

    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string? FloorName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }

    public string DeceasedId { get; set; } = string.Empty;
    public string DeceasedName { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public int? Age { get; set; }

    /// <summary>영정 사진 (썸네일용 파일 아이디)</summary>
    public string? MemorialPhotoFileId { get; set; }
    public string? MemorialPhotoUrl { get; set; }

    /// <summary>입실 (옛 <c>layout_corpse_dt</c>)</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>퇴실 (옛 <c>chulsang_dt</c>)</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>사용 일수. 퇴실 전이면 오늘까지로 센다.</summary>
    public int UseDays { get; set; }

    /// <summary>
    /// 발인 (옛 <c>borne_out_dt</c>). 값은 <c>Deceased.BurialDate</c> 다 —
    /// 그 엔티티에서 <c>FuneralDate</c> 는 입관, <c>BurialDate</c> 가 발인이다.
    /// </summary>
    public DateTime? DepartureDate { get; set; }

    /// <summary>장지 (옛 <c>jangji</c>)</summary>
    public string? BurialPlot { get; set; }

    /// <summary>지금 쓰고 있는 방인지 (옛 <c>using</c>)</summary>
    public bool InUse { get; set; }

    /// <summary>출상 여부 (옛 <c>chulsang</c>)</summary>
    public bool Departed { get; set; }

    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 고인 정보 조회 결과 한 줄. 옛 <c>page/room/goin4room.jsp</c> 의 표에 대응한다.
/// </summary>
public class DeceasedLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public int? Age { get; set; }
    public string? Religion { get; set; }

    public string? MemorialPhotoFileId { get; set; }
    public string? MemorialPhotoUrl { get; set; }

    public DateTime? DeathDate { get; set; }
    public DateTime? FuneralDate { get; set; }
    public DateTime? BurialDate { get; set; }
    public string? BurialPlot { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string? FloorName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>상주 이름을 쉼표로 이어 붙인 것 (목록에서 한눈에 보라고)</summary>
    public string? MournerNames { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 나의 정보. 옛 <c>page/ui_config.jsp</c> 왼쪽 칸이다.
/// </summary>
public class MyInfoDto
{
    public string UserId { get; set; } = string.Empty;
    public string? Role { get; set; }

    /// <summary>이 사람이 다루는 건물 수 (권한 범위를 가늠하라고 준다)</summary>
    public int BuildingCount { get; set; }

    /// <summary>지금 쓰고 있는 빈소 수</summary>
    public int RoomsInUse { get; set; }

    /// <summary>계정별 업무 설정</summary>
    public List<AccountSettingDto> Settings { get; set; } = new();
}

/// <summary>
/// 미리보기 화면에 뿌릴 장비 한 대. 옛 <c>client_machine/{번호}/index.jsp</c> 대응이다.
/// </summary>
public class DevicePreviewDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>장비 인증 코드 (옛 <c>auth_key</c>). 미리보기 주소에 들어간다.</summary>
    public string? DeviceCode { get; set; }

    public string? DeviceType { get; set; }
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }

    public bool IsOnline { get; set; }
    public DateTime? LastConnectedAt { get; set; }

    /// <summary>새 창으로 열 미리보기 주소</summary>
    public string PreviewUrl { get; set; } = string.Empty;
}
