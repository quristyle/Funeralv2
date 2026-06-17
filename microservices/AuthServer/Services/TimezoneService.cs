using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 타임존 정보 제공 서비스 구현체
/// </summary>
public class TimezoneService : ITimezoneService
{
    /// <summary>
    /// 현재 사용자의 설정된 타임존 반환
    /// </summary>
    public Task<string> GetCurrentTimezoneAsync(string userId)
    {
        // 기본값으로 한국 시간대 반환
        return Task.FromResult("Asia/Seoul");
    }

    /// <summary>
    /// 선택 가능한 타임존 옵션 목록 반환
    /// </summary>
    public Task<List<TimezoneOptionDto>> GetTimezoneOptionsAsync()
    {
        var options = new List<TimezoneOptionDto>
        {
            new TimezoneOptionDto { Label = "Asia/Seoul (한국)", Value = "Asia/Seoul" },
            new TimezoneOptionDto { Label = "UTC (Universal Time)", Value = "UTC" },
            new TimezoneOptionDto { Label = "America/New_York", Value = "America/New_York" },
            new TimezoneOptionDto { Label = "Europe/London", Value = "Europe/London" },
            new TimezoneOptionDto { Label = "Asia/Tokyo", Value = "Asia/Tokyo" }
        };

        return Task.FromResult(options);
    }
}
