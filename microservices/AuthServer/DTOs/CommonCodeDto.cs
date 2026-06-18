using System.ComponentModel.DataAnnotations;

namespace AuthServer.DTOs;

/// <summary>
/// 다단계 공통코드 DTO
/// </summary>
public class CommonCodeDto
{
    public string Id { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string CodeValue { get; set; } = string.Empty;
    public string CodeName { get; set; } = string.Empty;
    public string? I18nKey { get; set; }
    public int SortOrder { get; set; }
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    
    /// <summary>하위 코드 목록 (트리 구조 표현용)</summary>
    public List<CommonCodeDto>? Children { get; set; }
}

public class CommonCodeCreateDto
{
    [Required]
    public string GroupId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    [Required]
    public string CodeValue { get; set; } = string.Empty;
    [Required]
    public string CodeName { get; set; } = string.Empty;
    public string? I18nKey { get; set; }
    public int SortOrder { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}
