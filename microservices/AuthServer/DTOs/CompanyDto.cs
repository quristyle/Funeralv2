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

    public DateTime CreatedAt { get; set; }
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
}
