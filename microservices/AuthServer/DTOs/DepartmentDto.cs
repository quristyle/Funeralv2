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
    /// 이 부서에 **직접** 소속된 사용자 수.
    /// </summary>
    /// <remarks>
    /// 하위 부서의 인원은 포함하지 않는다. 사용자는 부서 하나에만 붙으므로
    /// (<c>accounts.department_id</c>) 직접 소속이 곧 "이 부서 인원" 이다.
    /// 하위까지 합친 값이 필요하면 <see cref="TotalUserCount"/> 를 쓴다.
    /// </remarks>
    public int UserCount { get; set; }

    /// <summary>
    /// 하위 부서까지 합친 사용자 수.
    /// </summary>
    /// <remarks>
    /// 트리를 접어 둔 상태에서 "이 조직 전체에 몇 명인지" 를 보여 줄 때 쓴다.
    /// 자식이 없으면 <see cref="UserCount"/> 와 같다.
    /// </remarks>
    public int TotalUserCount { get; set; }

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
