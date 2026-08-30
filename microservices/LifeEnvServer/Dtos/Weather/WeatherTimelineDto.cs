namespace LifeEnvServer.Dtos;

/// <summary>
/// 기상 타임라인 정보 DTO
/// </summary>
/// <param name="Date">날짜</param>
/// <param name="Time">시간</param>
/// <param name="Temp">온도</param>
/// <param name="Pop">강수 확률</param>
/// <param name="Rain">강수량</param>
/// <param name="Sky">하늘 상태</param>
/// <param name="Pty">강수 형태</param>
/// <param name="WindSpeed">풍속</param>
/// <param name="WindDir">풍향</param>
/// <param name="Reh">습도</param>
/// <param name="Uuu">동서바람</param>
/// <param name="Vvv">남북바람</param>
/// <param name="Sno">적설량</param>
/// <param name="IsForecast">예보 여부</param>
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
