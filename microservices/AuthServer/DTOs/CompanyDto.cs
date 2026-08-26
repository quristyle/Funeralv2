using System.ComponentModel.DataAnnotations;

namespace AuthServer.DTOs;

/// <summary>
/// 회사 관리(CRUD)를 위한 데이터 구조 DTO
/// </summary>
public class CompanyDto
{
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? BusinessNumber { get; set; }

    public string? Representative { get; set; }

    public int Status { get; set; } = 1;

    public string? Remark { get; set; }

    public string? ShortName { get; set; }

    public string? ZipCode { get; set; }

    public string? Address { get; set; }

    public string? AddressDetail { get; set; }

    public DateTime? ApprovalDate { get; set; }

    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 이 회사에 소속된 사용자 수.
    /// </summary>
    /// <remarks>
    /// 회사 목록에서 "어디에 사람이 있는지" 를 바로 보여 주려고 함께 내려준다.
    /// 회사마다 따로 세면 회사 수만큼 질의가 나가므로(N+1) 한 번에 묶어 센다.
    /// </remarks>
    public int UserCount { get; set; }

    /// <summary>이 회사에 등록된 부서 수.</summary>
    public int DeptCount { get; set; }
}

public class CompanyCreateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? BusinessNumber { get; set; }

    public string? Representative { get; set; }

    public int Status { get; set; } = 1;

    public string? Remark { get; set; }

    public string? ShortName { get; set; }

    public string? ZipCode { get; set; }

    public string? Address { get; set; }

    public string? AddressDetail { get; set; }

    public DateTime? ApprovalDate { get; set; }

    public int SortOrder { get; set; } = 0;
}
