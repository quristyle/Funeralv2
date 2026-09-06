namespace JSini.Web.Funeral.Api;

// ── 장례식장 DTO 모음 ─────────────────────────────────────────────
//
// Vue 원본: fronts/apps/jsini-portal/src/api/funeral/**/index.ts 의 인터페이스를
// 그대로 옮겼다. 백엔드(funeralv2Api, .NET)가 camelCase JSON 으로 내려주고
// GatewayClient 가 대소문자를 가리지 않으므로 속성 이름만 PascalCase 로 바꾼다.
//
// 화면 폼에 바로 바인딩할 수 있게 전부 변경 가능(mutable) 클래스다.

/// <summary>건물 (빈소가 있는 시설 한 동)</summary>
public sealed class Building
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Abbreviation { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }
    public string? AddressDetail { get; set; }
    public string? Remark { get; set; }
    public string? BuildingPhotoGroupId { get; set; }
    public string? ParkingPhotoGroupId { get; set; }

    /// <summary>
    /// 전경 사진 주소들. <b>서버가 세어 준다 — 보내지 않는다.</b>
    /// 목록에서 「몇 장인가」를 보여 주는 데만 쓴다(그룹 아이디가 정본이다).
    /// </summary>
    public List<string> BuildingPhotos { get; set; } = [];

    /// <summary>주차장 안내 이미지 주소들. <see cref="BuildingPhotos"/> 와 같다.</summary>
    public List<string> ParkingPhotos { get; set; } = [];

    public DateTime? CreatedAt { get; set; }
}

/// <summary>층</summary>
public sealed class Floor
{
    public string Id { get; set; } = string.Empty;
    public string BuildingId { get; set; } = string.Empty;
    public string? BuildingName { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>호실 (빈소·안치실·참관실 등)</summary>
public sealed class Room
{
    public string Id { get; set; } = string.Empty;
    public string? BuildingId { get; set; }
    public string? FloorId { get; set; }
    public string? FloorName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    /// <summary>빈소, 안치실, 참관실 등</summary>
    public string RoomType { get; set; } = string.Empty;
    /// <summary>ACTIVE · INACTIVE</summary>
    public string Status { get; set; } = "ACTIVE";
    public string? Remark { get; set; }

    /// <summary>
    /// 편집 폼의 「사용」 스위치가 묶이는 자리.
    ///
    /// <para>
    /// 팝업 안 편집기는 <c>@@bind-</c> 로 묶어야 한다 — 값과 콜백을 따로 주면
    /// 검증 식이 없어 <b>팝업이 말없이 안 열린다.</b> 자료가 글자라 그대로는
    /// 못 묶으므로 형이 맞는 창을 낸다.
    /// </para>
    /// </summary>
    public bool IsActive
    {
        get => Status == "ACTIVE";
        set => Status = value ? "ACTIVE" : "INACTIVE";
    }
}

/// <summary>장비 (DID · 키오스크 · 현판 등)</summary>
public sealed class Device
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? PublicIpAddress { get; set; }
    /// <summary>ONLINE · OFFLINE · UNKNOWN</summary>
    public string Status { get; set; } = "UNKNOWN";
    /// <summary>장비가 마지막으로 SignalR 에 붙은 시각</summary>
    public DateTime? LastSeenAt { get; set; }
    public int SortOrder { get; set; }
    public string? CompanyId { get; set; }
    public string? BuildingId { get; set; }
    public string? FloorId { get; set; }
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string? BuildingShortName { get; set; }
    public string? FloorShortName { get; set; }
    public string? RoomShortName { get; set; }
    public string? VideoId { get; set; }
    public string? MusicId { get; set; }
    public bool? IsVideoEnabled { get; set; }
    public bool? IsMusicEnabled { get; set; }
    public string? VideoName { get; set; }
    public string? MusicName { get; set; }
}

/// <summary>장비 기기 설정 (볼륨·밝기·전원 시각)</summary>
public sealed class DeviceConfig
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public int Volume { get; set; }
    public int Brightness { get; set; }
    public string? RebootTime { get; set; }
    public bool IsAutoPower { get; set; }
    public string? PowerOnTime { get; set; }
    public string? PowerOffTime { get; set; }
}

/// <summary>미디어 소스 (영상 · 음원 · 이미지 · 배경)</summary>
public sealed class MediaSource
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>VIDEO · AUDIO · IMAGE · BACKGROUND</summary>
    public string SourceType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? Remark { get; set; }
    public string? ShortName { get; set; }
    public int? SortOrder { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ThumbnailFileId { get; set; }
    public string? OriginalFileId { get; set; }
}

/// <summary>고인 (목록용)</summary>
public sealed class Deceased
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>MALE · FEMALE</summary>
    public string Gender { get; set; } = "MALE";
    public int Age { get; set; }
    public string? Religion { get; set; }
    public DateTime? DeathDate { get; set; }
    public DateTime? FuneralDate { get; set; }
    public DateTime? BurialDate { get; set; }
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    /// <summary>장례 상태 — COMPLETED · FUNERAL_DEPARTURE_COMPLETED · FUNERAL_IN_PROGRESS</summary>
    public string Status { get; set; } = "FUNERAL_IN_PROGRESS";
}

/// <summary>상주</summary>
public sealed class DeceasedMourner
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsChief { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>계약자</summary>
public sealed class DeceasedContractor
{
    public string Name { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string? Relation { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
    public string? SignatureFileId { get; set; }
}

/// <summary>장례지도사·직원 정보</summary>
public sealed class DeceasedManager
{
    public string? DirectorName { get; set; }
    public string? DirectorContact { get; set; }
    public string? MutualAidCompany { get; set; }
    public string? StaffName { get; set; }
    public string? StaffContact { get; set; }
}

/// <summary>시설 사용 (과금 항목의 원천)</summary>
public sealed class DeceasedFacility
{
    public string? Id { get; set; }
    public string FacilityType { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double UseHours { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Remark { get; set; }
}

/// <summary>호실 배정 이력 한 줄</summary>
public sealed class DeceasedRoomAssignment
{
    public string? Id { get; set; }
    public string RoomId { get; set; } = string.Empty;
    public string? RoomName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>고인 상세 — 상주·계약자·시설·호실까지 한 번에</summary>
public sealed class DeceasedDetail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = "MALE";
    public int Age { get; set; }
    public string? Religion { get; set; }
    public DateTime? DeathDate { get; set; }
    public DateTime? FuneralDate { get; set; }
    public DateTime? BurialDate { get; set; }
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string Status { get; set; } = "FUNERAL_IN_PROGRESS";
    public string? Remark { get; set; }
    public string? Ssn { get; set; }
    public string? CauseOfDeath { get; set; }
    public string? BurialPlot { get; set; }
    public string? MemorialPhotoUrl { get; set; }
    public string? MemorialPhotoFileId { get; set; }
    public string? MemorialEditedPhotoUrl { get; set; }
    public string? MemorialEditedPhotoFileId { get; set; }
    public string? FamilyPhotoGroupId { get; set; }
    public string? ChiefMourner { get; set; }

    public List<DeceasedMourner> Mourners { get; set; } = [];
    public DeceasedContractor? Contractor { get; set; }
    public DeceasedManager? Manager { get; set; }
    public List<DeceasedFacility> Facilities { get; set; } = [];
    public List<DeceasedRoomAssignment> Rooms { get; set; } = [];
}

/// <summary>장비 속성 — 플레이어 표시 방식 전반</summary>
public sealed class DeviceAttribute
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;

    // 공통 표시 설정
    public string DisplayOrientation { get; set; } = "LANDSCAPE";
    public string PortraitOrientation { get; set; } = "HORIZONTAL";
    public string VideoOrientation { get; set; } = "HORIZONTAL";
    public int DisplayPaddingTop { get; set; }
    public int DisplayPaddingLeft { get; set; }
    public int DisplayPaddingRight { get; set; }
    public int DisplayPaddingBottom { get; set; }
    public int ContentIntervalSec { get; set; }
    public bool IsScreensaverEnabled { get; set; }
    public int ScreensaverTimeoutSec { get; set; }

    // 영정사진/추모 콘텐츠 설정
    public bool IsMemorialPhotoEnabled { get; set; }
    public string MemorialPhotoEffect { get; set; } = "FADE";
    public string PhotoVerticalAlignment { get; set; } = "TOP";
    public string PhotoHorizontalAlignment { get; set; } = "CENTER";
    public bool IsDeceasedNameVisible { get; set; }
    public bool IsFamilyContactVisible { get; set; }
    public bool IsMemorialPhotoKeepAspectRatio { get; set; }
    public int MemorialPaddingTop { get; set; }
    public int MemorialPaddingLeft { get; set; }
    public int MemorialPaddingRight { get; set; }
    public int MemorialPaddingBottom { get; set; }

    // 멀티미디어 콘텐츠 설정
    public bool IsVideoEnabled { get; set; }
    public bool IsMusicEnabled { get; set; }
    public string? VideoId { get; set; }
    public string? MusicId { get; set; }
    public int? MusicVolume { get; set; }
    public bool IsMediaLoop { get; set; }
    public bool IsMuted { get; set; }
    public bool IsBackgroundImageEnabled { get; set; }
    public string? BackgroundImageId { get; set; }
    public string BackgroundOrientation { get; set; } = "HORIZONTAL";

    // 층별 안내 설정
    public bool IsFloorGuideEnabled { get; set; }
    public bool IsRoomAssignmentVisible { get; set; }
    public bool IsActiveRoomsOnly { get; set; }
    public int FloorGuideRefreshSec { get; set; }

    // 입구 정보/키오스크 설정
    public bool IsTouchEnabled { get; set; }
    public bool IsQrCodeVisible { get; set; }
    public bool IsBuildingMapVisible { get; set; }
    public string? EntranceGreeting { get; set; }
    public bool IsNoticeVisible { get; set; }
    public int NoticeScrollSpeed { get; set; }
    public string? Remark { get; set; }
}

/// <summary>장비 리본(장식 이미지) 배치 한 건</summary>
public sealed class DeviceRibbon
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string MediaSourceId { get; set; } = string.Empty;
    public string? MediaSourceName { get; set; }
    public string? MediaSourceUrl { get; set; }
    public string? MediaSourceThumbnailUrl { get; set; }
    /// <summary>위치·크기는 % (소수점 3자리)</summary>
    public double PositionLeft { get; set; }
    public double PositionTop { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>리본 저장 요청 한 건</summary>
public sealed class DeviceRibbonUpsert
{
    public string DeviceId { get; set; } = string.Empty;
    public string MediaSourceId { get; set; } = string.Empty;
    public double PositionLeft { get; set; }
    public double PositionTop { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>텍스트 오버레이 한 건</summary>
public sealed class DeviceTextOverlay
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public int FontSize { get; set; } = 24;
    public string FontColor { get; set; } = "#ffffff";
    public string BackgroundColor { get; set; } = "transparent";
    /// <summary>left · center · right</summary>
    public string TextAlign { get; set; } = "center";
    /// <summary>normal · bold</summary>
    public string FontWeight { get; set; } = "normal";
    public double PositionLeft { get; set; }
    public double PositionTop { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

// ── 빈소 현황 ────────────────────────────────────────────────────

/// <summary>빈소 한 칸의 현황</summary>
public sealed class FuneralStatus
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string? RoomShortName { get; set; }
    public string? FloorId { get; set; }
    public string? FloorName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }
    public int SortOrder { get; set; }

    /// <summary>EMPTY 비어 있음 · USING 사용 중</summary>
    public string Status { get; set; } = "EMPTY";

    public string? DeceasedId { get; set; }
    /// <summary>고인 장례 상태 — FUNERAL_IN_PROGRESS · FUNERAL_DEPARTURE_COMPLETED · COMPLETED</summary>
    public string? DeceasedStatus { get; set; }
    public string? DeceasedName { get; set; }
    public string? DeceasedGender { get; set; }
    public int? DeceasedAge { get; set; }
    public string? Religion { get; set; }

    /// <summary>영정 — 서버가 보정본 우선으로 골라 준다</summary>
    public string? PhotoFileId { get; set; }
    public string? PhotoUrl { get; set; }

    /// <summary>상주. 여러 명이면 쉼표로 이어져 온다.</summary>
    public string? ChiefMourner { get; set; }

    /// <summary>입관 일시</summary>
    public DateTime? CoffinTime { get; set; }
    /// <summary>발인 일시</summary>
    public DateTime? DischargeTime { get; set; }
    /// <summary>장지</summary>
    public string? BurialPlace { get; set; }
    /// <summary>사망 일시</summary>
    public DateTime? DeathDate { get; set; }

    public DateTime? StartTime { get; set; }
    /// <summary>빈 호실의 마지막 퇴실 일시</summary>
    public DateTime? LastVacatedAt { get; set; }
    /// <summary>빈 호실에서 마지막으로 출상한 고인 — 출상 취소 진입점</summary>
    public string? LastDepartedDeceasedId { get; set; }
    public string? LastDepartedDeceasedName { get; set; }
    public int UseDays { get; set; }

    public int DeviceCount { get; set; }
    public int OnlineDeviceCount { get; set; }

    /// <summary>이 호실의 장비 목록 — room-board 만 채운다.</summary>
    public List<Device>? Devices { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 지금 고인이 모셔져 있는가.
    ///
    /// <b><c>Status</c> 문자열을 화면에서 비교하지 않는다.</b> 서버가 쓰는 값이
    /// EMPTY · IN_USE 두 가지만이 아니고(출상 대기 같은 중간 상태가 있다),
    /// 화면마다 각자 비교하면 어느 화면은 중간 상태를 빈방으로 센다.
    /// 판정을 여기 한 줄로 모아 둔다.
    /// </summary>
    public bool Occupied => !string.IsNullOrEmpty(DeceasedId);
}

/// <summary>현황 요약 숫자</summary>
public sealed class StatusSummary
{
    public int TotalRooms { get; set; }
    public int UsingRooms { get; set; }
    public int EmptyRooms { get; set; }
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
}

/// <summary>현황 목록 + 요약</summary>
public class StatusBoard
{
    public List<FuneralStatus> Rooms { get; set; } = [];
    public StatusSummary Summary { get; set; } = new();
}

/// <summary>빈소현황 대시보드 응답 — 건물 공용 장비까지</summary>
public sealed class RoomBoard : StatusBoard
{
    /// <summary>호실에 매이지 않은 건물 공용 장비</summary>
    public List<Device> CommonDevices { get; set; } = [];
}

// ── 정보 화면 묶음 ───────────────────────────────────────────────

/// <summary>호실 히스토리 한 줄</summary>
public sealed class RoomHistory
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
    public string? MemorialPhotoFileId { get; set; }
    public string? MemorialPhotoUrl { get; set; }
    /// <summary>입실</summary>
    public DateTime? StartTime { get; set; }
    /// <summary>퇴실</summary>
    public DateTime? EndTime { get; set; }
    public int UseDays { get; set; }
    /// <summary>발인</summary>
    public DateTime? DepartureDate { get; set; }
    public string? BurialPlot { get; set; }
    public bool InUse { get; set; }
    public bool Departed { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>고인 정보 조회 결과 한 줄</summary>
public sealed class DeceasedLookup
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
    /// <summary>상주 이름을 쉼표로 이어 붙인 것</summary>
    public string? MournerNames { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>계정별 업무 설정 한 줄 (환경설정 · 나의정보 공용)</summary>
public sealed class EnvironmentSetting
{
    /// <summary>설정 코드 (옛 conf_cd)</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>화면에서 구역을 나누는 묶음 이름</summary>
    public string GroupName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool DefaultValue { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>나의 정보</summary>
public sealed class MyInfo
{
    public string UserId { get; set; } = string.Empty;
    public string? Role { get; set; }
    public int BuildingCount { get; set; }
    public int RoomsInUse { get; set; }
    public List<EnvironmentSetting> Settings { get; set; } = [];
}

/// <summary>미리보기 대상 장비</summary>
public sealed class DevicePreview
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DeviceCode { get; set; }
    public string? DeviceType { get; set; }
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public string PreviewUrl { get; set; } = string.Empty;
}

// ── 통계 ────────────────────────────────────────────────────────

/// <summary>과금 항목 한 줄</summary>
public sealed class BillingItem
{
    public string Id { get; set; } = string.Empty;
    /// <summary>기본료 · 환경부담금 · 시설관리비 등</summary>
    public string Title { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    /// <summary>사용일수를 곱하는 항목인지</summary>
    public bool ApplyPerDay { get; set; }
    public decimal Amount { get; set; }
    public string? Remark { get; set; }
}

/// <summary>고인 한 명의 과금 내역</summary>
public sealed class Billing
{
    public string DeceasedId { get; set; } = string.Empty;
    public string DeceasedName { get; set; } = string.Empty;
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string? BuildingId { get; set; }
    public string? BuildingName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int UseDays { get; set; }
    public List<BillingItem> Items { get; set; } = [];
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>빈소 사용 내역 한 줄</summary>
public sealed class RoomUsage
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
    public decimal BillingAmount { get; set; }
    public bool InUse { get; set; }
}

/// <summary>통계 요약 숫자</summary>
public sealed class StatSummary
{
    public int DeceasedCount { get; set; }
    public int RoomUsageCount { get; set; }
    public int TotalUseDays { get; set; }
    public decimal TotalAmount { get; set; }
}

// ── 음원-건물 배정 ───────────────────────────────────────────────

/// <summary>건물 한 줄. 고른 음원이 이 건물에 배정돼 있는지가 함께 온다.</summary>
public sealed class BuildingMusicMapping
{
    public string BuildingId { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string? BuildingShortName { get; set; }
    public string? Address { get; set; }
    public int SortOrder { get; set; }
    public bool Mapped { get; set; }
    public string? MappingId { get; set; }
}

// ── 파일 업로드 ─────────────────────────────────────────────────

/// <summary>FileServer 업로드 결과 한 건 (봉투의 result[0])</summary>
public sealed class UploadedFile
{
    public string? Id { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    /// <summary>내려받기 주소 — 화면이 저장하는 값</summary>
    public string? DownloadUrl { get; set; }
    public string? Url { get; set; }
    /// <summary>영상이면 서버가 즉시 뽑아 주는 썸네일</summary>
    public string? ThumbnailUrl { get; set; }
    public string? ThumbnailFileId { get; set; }

    /// <summary>화면이 저장할 대표 주소. downloadUrl 우선, 없으면 url.</summary>
    public string? BestUrl => string.IsNullOrWhiteSpace(DownloadUrl) ? Url : DownloadUrl;
}
