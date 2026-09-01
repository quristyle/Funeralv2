using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 계정별 장례식장 업무 설정 서비스.
/// </summary>
public interface ISettingService
{
    /// <summary>한 사람의 설정 전부. 저장된 적 없는 것은 기본값으로 채워 돌려준다.</summary>
    Task<List<AccountSettingDto>> GetSettingsAsync(string userId);

    /// <summary>설정 한 줄을 바꾼다.</summary>
    Task<AccountSettingDto?> UpdateSettingAsync(string userId, string code, bool enabled);

    /// <summary>여러 줄을 한 번에 바꾼다. 목록에 없는 코드는 건드리지 않는다.</summary>
    Task<List<AccountSettingDto>> UpdateSettingsAsync(string userId, Dictionary<string, bool> values);
}
