namespace LifeEnvServer.Dtos;

/// <summary>
/// 중기 기상 예보 정보 DTO
/// </summary>
/// <param name="Date">날짜</param>
/// <param name="DayDisplay">요일 표시</param>
/// <param name="MinTemp">최저 기온</param>
/// <param name="MaxTemp">최고 기온</param>
/// <param name="AmSky">오전 하늘 상태</param>
/// <param name="PmSky">오후 하늘 상태</param>
/// <param name="AmPop">오전 강수 확률</param>
/// <param name="PmPop">오후 강수 확률</param>
public record MidTermForecastDto(
    string Date,
    string DayDisplay,
    int MinTemp,
    int MaxTemp,
    string AmSky,
    string PmSky,
    int AmPop,
    int PmPop
);
