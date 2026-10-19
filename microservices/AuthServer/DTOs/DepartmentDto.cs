namespace AuthServer.DTOs;

/// <summary>부서 정보를 전달하기 위한 DTO</summary>
public class DepartmentDto
{
    /// <summary>부서 아이디 (GUID)</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>부서명</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>상위 부서 아이디 (트리 구조용)</summary>
    public string? Pid { get; set; }

    /// <summary>소속 회사 아이디</summary>
    public string? CompanyId { get; set; }

    /// <summary>소속 회사명</summary>
    public string? CompanyName { get; set; }

    /// <summary>부서 설명/메모</summary>
    public string? Remark { get; set; }

    /// <summary>부서 상태 (0: 비활성, 1: 활성)</summary>
    public int Status { get; set; }

    /// <summary>정렬 순서</summary>
    public int SortOrder { get; set; }

    /// <summary>이 부서에 직접 소속된 사용자 수. 하위 부서 인원은 뺀다 — 사용자는 부서 하나에만 붙으므로 직접 소속이 곧 이 부서 인원이다.</summary>
    public int UserCount { get; set; }

    /// <summary>하위 부서까지 합친 사용자 수. 자식이 없으면 UserCount 와 같다.</summary>
    public int TotalUserCount { get; set; }

    /// <summary>하위 부서 목록 (트리 구조용)</summary>
    public List<DepartmentDto>? Children { get; set; }
}

/// <summary>부서 생성을 위한 데이터 구조 DTO</summary>
public class CreateDepartmentDto
{
    /// <summary>생성할 부서명</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>상위 부서 아이디</summary>
    public string? Pid { get; set; }

    /// <summary>소속 회사 아이디</summary>
    public string? CompanyId { get; set; }

    /// <summary>부서 설명/메모</summary>
    public string? Remark { get; set; }

    /// <summary>부서 상태 (0: 비활성, 1: 활성)</summary>
    public int Status { get; set; }

    /// <summary>정렬 순서</summary>
    public int SortOrder { get; set; }
}

/// <summary>끌어 옮긴 뒤의 부서 자리 한 줄. 화면이 확정한 배치를 그대로 받는다 — 옮긴 한 건만 받으면 서버가 나머지 순번을 짐작해야 해서 화면과 어긋난다.</summary>
public class DeptOrderDto
{
    /// <summary>부서 아이디</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>새 상위 부서 아이디 (회사 직속은 null)</summary>
    public string? Pid { get; set; }

    /// <summary>형제 안에서의 순번 (1부터)</summary>
    public int SortOrder { get; set; }
}
