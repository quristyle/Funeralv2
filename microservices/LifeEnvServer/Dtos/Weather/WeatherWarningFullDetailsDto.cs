using LifeEnvServer.Models;

namespace LifeEnvServer.Dtos;

/// <summary>
/// 기상 특보 통합 상세 정보 DTO
/// </summary>
/// <remarks>
/// 원본의 <c>Details</c>(weather_warning_details)는 영구 빈 테이블이라 이식하지 않았다.
/// 응답 JSON 모양을 유지하기 위해 항상 빈 목록으로 내려 준다.
/// </remarks>
public record WeatherWarningFullDetailsDto(
    WeatherWarning Warning,
    WeatherWarningMsg? Msg,
    List<object> Details,
    List<WeatherLocation> MatchedLocations,
    List<WeatherWarningZone> RelatedZones,
    List<WeatherWarningMsgSentence> Sentences
);
