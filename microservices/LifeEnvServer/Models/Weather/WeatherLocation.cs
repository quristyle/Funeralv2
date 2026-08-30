using System.ComponentModel.DataAnnotations;

namespace LifeEnvServer.Models;

/// <summary>
/// 기상 관측 지역 정보
/// </summary>
public class WeatherLocation : LifeEnvBaseEntity
{
    /// <summary>지역 명칭 (예: 울산 현장, 서울 본사)</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>기상청 API용 격자 X 좌표</summary>
    [Required]
    public int NX { get; set; }

    /// <summary>기상청 API용 격자 Y 좌표</summary>
    [Required]
    public int NY { get; set; }

    /// <summary>읍면동 명칭</summary>
    [MaxLength(100)]
    public string? Region3 { get; set; }

    /// <summary>지역 설명</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>중기 육상 예보 구역 코드 (예: 11B00000)</summary>
    [MaxLength(20)]
    public string? MidTermLandCode { get; set; }

    /// <summary>중기 기상 예보 구역 코드 (기온)</summary>
    public string? MidTermTempCode { get; set; }

    /// <summary>기상청 특보 구역 코드 (예: L1100600)</summary>
    [MaxLength(20)]
    public string? WarningAreaCode { get; set; }

    /// <summary>사용 여부</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>정렬 순서</summary>
    public int SortOrder { get; set; } = 0;
}
