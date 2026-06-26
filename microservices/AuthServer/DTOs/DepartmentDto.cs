namespace AuthServer.DTOs;

/// <summary>
/// 부서 정보를 전달하기 위한 DTO
/// </summary>
public class DepartmentDto
{
    /// <summary>
    /// 부서 아이디 (GUID)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 부서명
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 상위 부서 아이디 (트리 구조용)
    /// </summary>
    public string? Pid { get; set; }

    /// <summary>
    /// 소속 회사 아이디
    /// </summary>
    public string? CompanyId { get; set; }

    /// <summary>
    /// 소속 회사명
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// 부서 설명/메모
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 부서 상태 (0: 비활성, 1: 활성)
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 하위 부서 목록 (트리 구조용)
    /// </summary>
    public List<DepartmentDto>? Children { get; set; }
}

/// <summary>
/// 부서 생성을 위한 데이터 구조 DTO
/// </summary>
public class CreateDepartmentDto
{
    /// <summary>
    /// 생성할 부서명
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 상위 부서 아이디
    /// </summary>
    public string? Pid { get; set; }

    /// <summary>
    /// 소속 회사 아이디
    /// </summary>
    public string? CompanyId { get; set; }

    /// <summary>
    /// 부서 설명/메모
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 부서 상태 (0: 비활성, 1: 활성)
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; }
}
