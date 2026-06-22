namespace AuthServer.DTOs;

/// <summary>
/// 역할(권한 그룹) 정보를 전달하기 위한 DTO
/// </summary>
public class RoleDto
{
    /// <summary>
    /// 역할 아이디 (GUID)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 역할 명칭
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 해당 역할에 부여된 권한(코드) 목록
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// 역할에 대한 설명/메모
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 역할 활성화 상태 (0: 비활성, 1: 활성)
    /// </summary>
    public int Status { get; set; } // 0 | 1
}

/// <summary>
/// 역할 생성을 위한 데이터 구조 DTO
/// </summary>
public class CreateRoleDto
{
    /// <summary>
    /// 생성할 역할 아이디
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 생성할 역할 명칭
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 부여할 권한 목록
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// 역할에 대한 설명/메모
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 초기 활성화 상태
    /// </summary>
    public int Status { get; set; }
}
