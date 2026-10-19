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
    /// 목록에서 어디에 사람이 있는지 보여 주려고 내려준다 — 서버가 한 번에 묶어 센다(N+1 방지).
    public int UserCount { get; set; }

    /// <summary>이 회사에 등록된 부서 수.</summary>
    public int DeptCount { get; set; }

    /// <summary>
    /// 회사 사용처 — 이 회사가 쓰이는 시스템들.
    /// </summary>
    /// 공통코드 COMPANY_USAGE_LOCATION 의 code_value 목록. 빈 목록일 수 있다(아무 데도 배정하지 않은 회사).
    public List<string> UsageLocations { get; set; } = new();
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

    /// <summary>
    /// 회사 사용처 (<c>COMPANY_USAGE_LOCATION</c> 의 <c>code_value</c> 목록).
    /// 일부러 nullable 이다 — 값을 싣지 않으면 사용처를 건드리지 않는다(셀 편집이 사용처를 지우지 않게). 빈 목록([])은 전부 해제다.
    /// </summary>
    public List<string>? UsageLocations { get; set; }
}
