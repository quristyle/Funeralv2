using System.ComponentModel.DataAnnotations;

namespace GhubServer.Models;

/// <summary>
/// 생일 대상자.
/// ASIS(GHUB) user_profiles 에서 생일 관련 필드만 이관한 신설 테이블이다.
/// 사용자 정본은 scom(AuthServer)이라 이 DB 안에서는 FK 를 두지 않는다 —
/// UserId 문자열 값으로만 연결한다. (docs/analysis/38-ghub-migration.md 3절)
/// </summary>
public class BirthdayProfile : GhubBaseEntity
{
    /// <summary>사용자 ID (scom 계정의 로그인 ID 값, UNIQUE)</summary>
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>이름</summary>
    [Required]
    public string FullName { get; set; } = string.Empty;

    /// <summary>부서</summary>
    public string? Department { get; set; }

    /// <summary>회사 코드</summary>
    public string? CompanyCode { get; set; }

    /// <summary>썸네일(프로필 사진) URL</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>생년월일</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>음력 여부</summary>
    public bool IsLunar { get; set; }

    /// <summary>올해 축하 완료 여부</summary>
    public bool IsCelebrated { get; set; }

    /// <summary>사용 여부</summary>
    public bool IsActive { get; set; } = true;
}
