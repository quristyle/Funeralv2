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
