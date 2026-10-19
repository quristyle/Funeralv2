namespace LifeEnvServer.Dtos;

/// <summary>
/// 기상 타임라인 한 칸(날짜·시간·기온·강수·하늘·바람 등). IsForecast 로 실황과 예보를 가른다.
/// </summary>
public record WeatherTimelineDto(
    string Date,
    string Time,
    double Temp,
    int? Pop,
    double? Rain,
    string Sky,
    string Pty,
    double WindSpeed,
    double WindDir,
    int? Reh,
    double? Uuu,
    double? Vvv,
    double? Sno,
    bool IsForecast
);
