using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인 정보 관리 엔티티 클래스
/// </summary>
[Table("deceaseds")]
public class Deceased
{
    /// <summary>
    /// 고인 식별자 (ID, GUID 또는 고유 코드)
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// 고인 성명
    /// </summary>
    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// 고인 성별 (MALE, FEMALE 등)
    /// </summary>
    [Required]
    [Column("gender")]
    [MaxLength(10)]
    public string Gender { get; set; } = null!;

    /// <summary>
    /// 고인 연세 (나이)
    /// </summary>
    [Column("age")]
    public int Age { get; set; }

    /// <summary>
    /// 종교
    /// </summary>
    [Column("religion")]
    [MaxLength(50)]
    public string? Religion { get; set; }

    /// <summary>
    /// 사망 일시
    /// </summary>
    [Required]
    [Column("death_date")]
    public DateTime DeathDate { get; set; }

    /// <summary>
    /// 입관/장례 일시
    /// </summary>
    [Column("funeral_date")]
    public DateTime? FuneralDate { get; set; }

    /// <summary>
    /// 발인 일시
    /// </summary>
    [Column("burial_date")]
    public DateTime? BurialDate { get; set; }

    /// <summary>
    /// 장례 진행 상태. 허용 값은 DeceasedStatus 의 셋뿐이다 —
    /// FUNERAL_IN_PROGRESS(진행중) · FUNERAL_DEPARTURE_COMPLETED(출상) · COMPLETED(종료).
    /// </summary>
    [Required]
    [Column("status")]
    [MaxLength(30)]
    public string Status { get; set; } = DeceasedStatus.InProgress;

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 주민등록번호
    /// </summary>
    [Column("ssn")]
    [MaxLength(50)]
    public string? Ssn { get; set; }

    /// <summary>
    /// 사망 원인
    /// </summary>
    [Column("cause_of_death")]
    [MaxLength(200)]
    public string? CauseOfDeath { get; set; }

    /// <summary>
    /// 장지 위치
    /// </summary>
    [Column("burial_plot")]
    [MaxLength(200)]
    public string? BurialPlot { get; set; }

    /// <summary>
    /// 영정사진 웹 URL 주소
    /// </summary>
    [Column("memorial_photo_url")]
    [MaxLength(500)]
    public string? MemorialPhotoUrl { get; set; }

    /// <summary>
    /// 영정사진 원본 파일 식별자 (ID)
    /// </summary>
    [Column("memorial_photo_file_id")]
    [MaxLength(50)]
    public string? MemorialPhotoFileId { get; set; }

    /// <summary>
    /// 편집된(보정/배경합성 등) 영정사진 웹 URL 주소
    /// </summary>
    [Column("memorial_edited_photo_url")]
    [MaxLength(500)]
    public string? MemorialEditedPhotoUrl { get; set; }

    /// <summary>
    /// 편집된 영정사진 파일 식별자 (ID)
    /// </summary>
    [Column("memorial_edited_photo_file_id")]
    [MaxLength(50)]
    public string? MemorialEditedPhotoFileId { get; set; }

    /// <summary>
    /// 유족 추모용 사진 파일 그룹 식별자 (ID)
    /// </summary>
    [Column("family_photo_group_id")]
    [MaxLength(50)]
    public string? FamilyPhotoGroupId { get; set; }

    /// <summary>
    /// 최초 등록자 식별자 (ID)
    /// </summary>
    [Column("created_by")]
    [MaxLength(50)]
    public string CreatedBy { get; set; } = null!;

    /// <summary>
    /// 최초 등록 일시
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 최종 수정자 식별자 (ID)
    /// </summary>
    [Column("updated_by")]
    [MaxLength(50)]
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// 최종 수정 일시
    /// </summary>
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 삭제 여부 플래그
    /// </summary>
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
