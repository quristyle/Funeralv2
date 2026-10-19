using Mapster;

namespace JSini.Shared.DTOs;

/// <summary>Mapster 전역 매핑 설정.</summary>
public static class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig.GlobalSettings.Default
            .NameMatchingStrategy(NameMatchingStrategy.Flexible)
            .MaxDepth(3); // 순환 참조로 인한 무한 루프 및 매핑 컴파일 오류 전역 방지

    }
}
