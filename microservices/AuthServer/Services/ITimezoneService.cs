using AuthServer.DTOs;

namespace AuthServer.Services;

public interface ITimezoneService
{
    Task<string> GetCurrentTimezoneAsync(string userId);
    Task<List<TimezoneOptionDto>> GetTimezoneOptionsAsync();
}
