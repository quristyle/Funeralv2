using System.ComponentModel.DataAnnotations;

namespace AuthServer.DTOs;

/// <summary>
/// 공통코드 그룹 DTO
/// </summary>
public class CommonCodeGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public bool IsHierarchical { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CommonCodeGroupCreateDto
{
    [Required]
    public string GroupCode { get; set; } = string.Empty;
    [Required]
    public string GroupName { get; set; } = string.Empty;
    public bool IsHierarchical { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}
