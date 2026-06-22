using AuthServer.DTOs;

namespace AuthServer.Services;

public interface IBizSelectConfigService
{
    Task<IEnumerable<BizSelectConfigDto>> GetAllConfigsAsync();
    Task<BizSelectConfigDto?> GetConfigByIdAsync(string id);
    Task<BizSelectConfigDto> CreateConfigAsync(BizSelectConfigCreateDto createDto);
    Task<bool> UpdateConfigAsync(string id, BizSelectConfigCreateDto updateDto);
    Task<bool> DeleteConfigAsync(string id);
}
