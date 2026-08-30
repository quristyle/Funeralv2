namespace GhubServer.Models;

/// <summary>
/// GHUB 이식 엔티티 공통 컬럼.
///
/// 포털 공통(JSini.Shared.Domain.BaseEntity)이 아니라 원본(GHUB skgRestApi)의
/// BaseEntity 모양을 그대로 둔다 — 컬럼 이름(modified_* 등)이 ASIS 와 같아야
/// 자료 이관이 복사만으로 끝나기 때문이다. (docs/sql/ghub_schema.sql 머리말)
/// </summary>
public abstract class GhubBaseEntity
{
    /// <summary>PK</summary>
    public int Id { get; set; }

    /// <summary>생성자(로그인 ID)</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>생성 일시(UTC)</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>수정자</summary>
    public string? ModifiedBy { get; set; }

    /// <summary>수정 일시(UTC)</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>감사: 처리 서비스</summary>
    public string? ActionService { get; set; }

    /// <summary>감사: 호출 화면</summary>
    public string? MenuContext { get; set; }

    /// <summary>감사: 요청 IP</summary>
    public string? RemoteAddr { get; set; }

    /// <summary>감사: 요청 단말 정보</summary>
    public string? RemoteMachineInfo { get; set; }

    /// <summary>논리 삭제</summary>
    public bool IsDeleted { get; set; }
}
