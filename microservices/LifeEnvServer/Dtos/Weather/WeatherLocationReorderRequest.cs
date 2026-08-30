namespace LifeEnvServer.Dtos;

/// <summary>
/// 기상 위치 순서 재정렬 요청 DTO
/// </summary>
/// <param name="Id">위치 ID</param>
/// <param name="SortOrder">정렬 순서</param>
public record WeatherLocationReorderRequest(int Id, int SortOrder);
