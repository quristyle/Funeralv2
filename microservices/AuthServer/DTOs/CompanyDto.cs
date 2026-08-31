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

    /// <summary>
    /// 회사 사용처 — 이 회사가 쓰이는 시스템들.
    /// </summary>
    /// <remarks>
    /// 공통코드 그룹 <c>COMPANY_USAGE_LOCATION</c> 의 <c>code_value</c> 목록이다.
    /// 여러 개일 수 있고 <b>빈 목록일 수도 있다</b>(아무 곳에도 배정하지 않은 회사).
    /// </remarks>
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
    ///
    /// <b>일부러 nullable 이다.</b> 값을 싣지 않은 요청은 사용처를 건드리지 않는다 —
    /// 회사 목록의 셀 편집처럼 일부 항목만 보내는 호출자가 사용처를 지우는 일을 막는다.
    /// 빈 목록(<c>[]</c>)을 보내면 '전부 해제' 다.
    /// </summary>
    public List<string>? UsageLocations { get; set; }
}
