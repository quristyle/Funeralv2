namespace LifeEnvServer.Dtos;

/// <summary>중기 기상 예보 하루치 DTO.</summary>
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
