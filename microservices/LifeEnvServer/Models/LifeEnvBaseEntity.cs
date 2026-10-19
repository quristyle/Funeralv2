namespace LifeEnvServer.Models;

/// <summary>
/// 이식 엔티티 공통 감사 컬럼. 컬럼 이름이 원본과 같아야 자료 이관이 복사만으로 끝나므로 포털 BaseEntity 를 쓰지 않는다.
/// </summary>
public abstract class LifeEnvBaseEntity
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
