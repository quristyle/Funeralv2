namespace Funeralv2.Shared.Domain;

/// <summary>
/// 모든 엔티티의 기본 클래스입니다. (ID 타입을 지정할 수 있는 제네릭 버전)
/// </summary>
/// <typeparam name="TKey">Primary Key의 타입</typeparam>
public abstract class BaseEntity<TKey>
{
    /// <summary>
    /// 기본 키 (ID)
    /// </summary>
    public TKey Id { get; set; } = default!;

    /// <summary>
    /// 생성 일시
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 생성자 ID
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// 수정 일시
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 수정자 ID
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// 삭제 여부 (Soft Delete)
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}

/// <summary>
/// 기본 int 형 ID를 사용하는 베이스 엔티티입니다.
/// </summary>
public abstract class BaseEntity : BaseEntity<int>
{
}
