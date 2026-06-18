using Mapster;

namespace Funeralv2.Shared.DTOs;

/// <summary>
/// Mapster 전역 매핑 설정 클래스
/// </summary>
public static class MapsterConfig
{
    public static void Configure()
    {
        // 전역 설정: 이름이 같은 필드는 자동으로 매핑
        TypeAdapterConfig.GlobalSettings.Default
            .NameMatchingStrategy(NameMatchingStrategy.Flexible);

        // 공통 변환 규칙 (예: DateTime -> String)
        // TypeAdapterConfig<DateTime, string>.NewConfig()
        //     .MapWith(dest => dest.ToString("yyyy-MM-dd HH:mm:ss"));

        // 필요한 경우 여기에 전역 매핑 규칙 추가
    }
}
