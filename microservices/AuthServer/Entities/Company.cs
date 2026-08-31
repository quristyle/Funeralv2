using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 회사 엔티티 클래스
/// </summary>
[Table("companies", Schema = "scom")]
public class Company : BaseEntity<string>
{
    /// <summary>
    /// Company 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public Company()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 회사 명칭
    /// </summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 사업자 등록 번호
    /// </summary>
    [Column("business_number")]
    public string? BusinessNumber { get; set; }

    /// <summary>
    /// 대표자명
    /// </summary>
    [Column("representative")]
    public string? Representative { get; set; }

    /// <summary>
    /// 회사 사용 상태 (1: 활성, 0: 비활성)
    /// </summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>
    /// 회사 약칭
    /// </summary>
    [Column("short_name")]
    public string? ShortName { get; set; }

    /// <summary>
    /// 우편번호
    /// </summary>
    [Column("zip_code")]
    public string? ZipCode { get; set; }

    /// <summary>
    /// 회사 주소
    /// </summary>
    [Column("address")]
    public string? Address { get; set; }

    /// <summary>
    /// 회사 상세 주소
    /// </summary>
    [Column("address_detail")]
    public string? AddressDetail { get; set; }

    /// <summary>
    /// 회사 승인 일자
    /// </summary>
    [Column("approval_date")]
    public DateTime? ApprovalDate { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 회사 소속 부서 목록 탐색 속성 (1:N 관계)
    /// </summary>
    public ICollection<Department>? Departments { get; set; }

    /// <summary>
    /// 이 회사의 사용처(<c>COMPANY_USAGE_LOCATION</c>) 연결 목록.
    /// </summary>
    /// <remarks>
    /// 이름을 DTO 의 <c>UsageLocations</c>(문자열 목록)와 <b>일부러 다르게</b> 두었다.
    /// 같으면 Mapster 가 엔티티 목록을 문자열 목록으로 옮기려 들어 조회가 깨진다.
    /// </remarks>
    public ICollection<CompanyUsageLocation>? UsageLocationLinks { get; set; }
}
