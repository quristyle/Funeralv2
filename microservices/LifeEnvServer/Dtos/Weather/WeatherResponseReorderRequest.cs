namespace LifeEnvServer.Dtos;

/// <summary>
/// 기상 기준별 대응 정보 정렬 순서 변경 요청 DTO
/// </summary>
/// <param name="Id">대응 정보 ID</param>
/// <param name="SortOrder">변경할 정렬 순서</param>
public record WeatherResponseReorderRequest(int Id, int SortOrder);
