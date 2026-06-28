using System;

namespace funeralv2Api.DTOs;

/// <summary>
/// 고인 정보 반환 DTO
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
}
