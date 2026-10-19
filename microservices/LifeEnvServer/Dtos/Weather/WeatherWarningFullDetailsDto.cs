using LifeEnvServer.Models;

namespace LifeEnvServer.Dtos;

/// <summary>기상 특보 통합 상세 DTO. 원본의 Details 는 영구 빈 테이블이라 이식하지 않았고, 응답 모양을 위해 항상 빈 목록을 내린다.</summary>
public record WeatherWarningFullDetailsDto(
    WeatherWarning Warning,
    WeatherWarningMsg? Msg,
    List<object> Details,
    List<WeatherLocation> MatchedLocations,
    List<WeatherWarningZone> RelatedZones,
    List<WeatherWarningMsgSentence> Sentences
);
