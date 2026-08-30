using System.ComponentModel.DataAnnotations;

namespace GhubServer.Models;

/// <summary>
/// 격자 좌표 정보 모델 (기상청 행정구역 → nx/ny 변환표, 검색 전용).
/// 감사 컬럼이 없는 순수 변환표라 GhubBaseEntity 를 상속하지 않는다 —
/// PK 는 행정구역코드(문자열)다 (GhubDbContext.OnModelCreating 에서 HasKey 지정).
/// </summary>
public class GridCoordinate
{
    /// <summary>
    /// 행정구역코드 (PK)
    /// </summary>
    [Key]
    public string AdministrativeCode { get; set; } = string.Empty;

    /// <summary>
    /// 구분 (예: kor, en, jp 등)
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 1단계 행정구역 (예: 시/도)
    /// </summary>
    public string? Region1 { get; set; }

    /// <summary>
    /// 2단계 행정구역 (예: 시/군/구)
    /// </summary>
    public string? Region2 { get; set; }

    /// <summary>
    /// 3단계 행정구역 (예: 읍/면/동)
    /// </summary>
    public string? Region3 { get; set; }

    /// <summary>
    /// 기상청 격자 X
    /// </summary>
    public int NX { get; set; }

    /// <summary>
    /// 기상청 격자 Y
    /// </summary>
    public int NY { get; set; }

    /// <summary>
    /// 경도 (시)
    /// </summary>
    public int? LongitudeHour { get; set; }

    /// <summary>
    /// 경도 (분)
    /// </summary>
    public int? LongitudeMinute { get; set; }

    /// <summary>
    /// 경도 (초)
    /// </summary>
    public decimal? LongitudeSecond { get; set; }

    /// <summary>
    /// 위도 (시)
    /// </summary>
    public int? LatitudeHour { get; set; }

    /// <summary>
    /// 위도 (분)
    /// </summary>
    public int? LatitudeMinute { get; set; }

    /// <summary>
    /// 위도 (초)
    /// </summary>
    public decimal? LatitudeSecond { get; set; }

    /// <summary>
    /// 경도 (초/100)
    /// </summary>
    public decimal? LongitudeSecond100 { get; set; }

    /// <summary>
    /// 위도 (초/100)
    /// </summary>
    public decimal? LatitudeSecond100 { get; set; }

    /// <summary>
    /// 위치 업데이트 일시
    /// </summary>
    public DateTimeOffset? LocationUpdatedAt { get; set; }
}
