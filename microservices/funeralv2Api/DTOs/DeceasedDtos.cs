using System;
using System.Collections.Generic;

namespace funeralv2Api.DTOs;

/// <summary>
/// 고인 정보 반환 DTO (기본 목록 조회용)
/// </summary>
public class DeceasedDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Gender { get; set; } = null!; // MALE, FEMALE
    public int Age { get; set; }
    public string? Religion { get; set; }
    public DateTime DeathDate { get; set; }
    public DateTime? FuneralDate { get; set; }
    public DateTime? BurialDate { get; set; }
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string Status { get; set; } = null!; // IN_HOSPITAL, DISCHARGED, COMPLETED
    public string? Remark { get; set; }
    public string? Ssn { get; set; }
    public string? CauseOfDeath { get; set; }
    public string? BurialPlot { get; set; }
    public string? MemorialPhotoUrl { get; set; }
    public string? MemorialPhotoFileId { get; set; }
    public string? FamilyPhotoGroupId { get; set; }
}

/// <summary>
/// 고인 생성 DTO
/// </summary>
public class DeceasedCreateDto
{
    public string Name { get; set; } = null!;
    public string Gender { get; set; } = null!; // MALE, FEMALE
    public int Age { get; set; }
    public string? Religion { get; set; }
    public DateTime DeathDate { get; set; }
    public DateTime? FuneralDate { get; set; }
    public DateTime? BurialDate { get; set; }
    public string? RoomId { get; set; }
    public string Status { get; set; } = "IN_HOSPITAL";
    public string? Remark { get; set; }
    public string? Ssn { get; set; }
    public string? CauseOfDeath { get; set; }
    public string? BurialPlot { get; set; }
}

/// <summary>
/// 고인 수정 DTO
/// </summary>
public class DeceasedUpdateDto
{
    public string Name { get; set; } = null!;
    public string Gender { get; set; } = null!; // MALE, FEMALE
    public int Age { get; set; }
    public string? Religion { get; set; }
    public DateTime DeathDate { get; set; }
    public DateTime? FuneralDate { get; set; }
    public DateTime? BurialDate { get; set; }
    public string? RoomId { get; set; }
    public string Status { get; set; } = null!;
    public string? Remark { get; set; }
    public string? Ssn { get; set; }
    public string? CauseOfDeath { get; set; }
    public string? BurialPlot { get; set; }
}

/// <summary>
/// 상주 정보 DTO
/// </summary>
public class DeceasedMournerDto
{
    public string? Id { get; set; }
    public string Name { get; set; } = null!;
    public string Relation { get; set; } = null!;
    public string Contact { get; set; } = null!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsChief { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 계약자 정보 DTO
/// </summary>
public class DeceasedContractorDto
{
    public string Name { get; set; } = null!;
    public string Contact { get; set; } = null!;
    public string? Relation { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
    public string? SignatureFileId { get; set; }
}

/// <summary>
/// 담당자 정보 DTO
/// </summary>
public class DeceasedManagerDto
{
    public string? DirectorName { get; set; }
    public string? DirectorContact { get; set; }
    public string? MutualAidCompany { get; set; }
    public string? StaffName { get; set; }
    public string? StaffContact { get; set; }
}

/// <summary>
/// 시설이용 정보 DTO
/// </summary>
public class DeceasedFacilityDto
{
    public string? Id { get; set; }
    public string FacilityType { get; set; } = null!; // MORGUE, WASH_ROOM, HALL, ETC
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double UseHours { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 호실지정 정보 DTO
/// </summary>
public class DeceasedRoomDto
{
    public string? Id { get; set; }
    public string RoomId { get; set; } = null!;
    public string? RoomName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 고인 종합 상세 DTO (조회 및 저장)
/// </summary>
public class DeceasedDetailDto
{
    // 기본정보
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Gender { get; set; } = null!; // MALE, FEMALE
    public int Age { get; set; }
    public string? Religion { get; set; }
    public DateTime DeathDate { get; set; }
    public DateTime? FuneralDate { get; set; }
    public DateTime? BurialDate { get; set; }
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string Status { get; set; } = null!;
    public string? Remark { get; set; }
    public string? Ssn { get; set; }
    public string? CauseOfDeath { get; set; }
    public string? BurialPlot { get; set; }
    public string? MemorialPhotoUrl { get; set; }
    public string? MemorialPhotoFileId { get; set; }
    public string? FamilyPhotoGroupId { get; set; }

    // 관계 리스트 및 단일 속성들
    public List<DeceasedMournerDto> Mourners { get; set; } = new();
    public DeceasedContractorDto? Contractor { get; set; }
    public DeceasedManagerDto? Manager { get; set; }
    public List<DeceasedFacilityDto> Facilities { get; set; } = new();
    public List<DeceasedRoomDto> Rooms { get; set; } = new();
}
