using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationServer.Entities;

/// <summary>
/// scom 계정 · 역할 표의 **읽기 전용** 매핑.
///
/// 정본은 AuthServer 다 — 이 서비스는 "이 역할 사용자들의 이메일" 을 풀기 위해
/// 조회만 한다 (문의 접수 알림을 시스템관리자에게 보내는 용도). scom 은 이 서비스가
/// 원래 접속하는 DB 라(머리말 참조) 서비스 경계를 넘지 않는다.
/// 쓰기는 절대 하지 않는다 — 컬럼도 필요한 것만 올린다.
/// </summary>
[Table("role_accounts", Schema = "scom")]
public class RoleAccountRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>scom 계정 (읽기 전용 — <see cref="RoleAccountRow"/> 머리말 참조)</summary>
[Table("accounts", Schema = "scom")]
public class AccountRow
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름. 쪽지의 보낸 이·받는 이에 적는다.</summary>
    [Column("user_name")]
    public string? UserName { get; set; }

    /// <summary>실명. <see cref="UserName"/> 이 비어 있을 때만 쓴다.</summary>
    [Column("real_name")]
    public string? RealName { get; set; }

    /// <summary>소속 부서. 같은 이름이 둘일 때 사람을 가르는 값이다.</summary>
    [Column("department_id")]
    public string? DepartmentId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>scom 부서 (읽기 전용 — 이름만 본다. <see cref="RoleAccountRow"/> 머리말 참조)</summary>
[Table("departments", Schema = "scom")]
public class DepartmentRow
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>scom 계정 확장 속성 (읽기 전용 — 이메일만 본다)</summary>
[Table("account_profile_details", Schema = "scom")]
public class AccountProfileDetailRow
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Email · Phone · Photo …</summary>
    [Column("detail_type")]
    public string DetailType { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>
/// scom 권한 역할 (읽기 전용 — <see cref="RoleAccountRow"/> 머리말 참조).
/// </summary>
/// <remarks>
/// 「알림관리」 화면이 고를 역할 목록과, 저장할 때 <b>없는 역할을 받지 않으려고</b>
/// 본다. 정본은 AuthServer 의 「역할 관리」다 — 여기서는 절대 쓰지 않는다.
/// </remarks>
[Table("roles", Schema = "scom")]
public class RoleRow
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>1 이 쓰는 역할이다. 0 은 멈춰 둔 것.</summary>
    [Column("status")]
    public int Status { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>
/// scom 메뉴 (읽기 전용 — 권한 판정에만 쓴다).
/// </summary>
/// <remarks>
/// 「이 화면을 볼 수 있는 사람인가」를 사이드바와 <b>같은 표에</b> 묻기 위해
/// 올린다(<see cref="Services.MenuViewAccess"/> 머리말). 역할 이름을 코드에
/// 적어 두면 메뉴 권한을 한 역할에 더하는 날 「메뉴는 보이는데 403」이 된다.
/// </remarks>
[Table("system_menus", Schema = "scom")]
public class SystemMenuRow
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>화면이 <c>RouteKey</c> 로 선언한 열쇠. 연결 고리는 이쪽이다.</summary>
    [Column("route_key")]
    public string? RouteKey { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>scom 역할-메뉴 권한 (읽기 전용 — <see cref="SystemMenuRow"/> 머리말 참조)</summary>
[Table("role_menus", Schema = "scom")]
public class RoleMenuRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [Column("menu_id")]
    public string MenuId { get; set; } = string.Empty;

    [Column("can_view")]
    public bool CanView { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
