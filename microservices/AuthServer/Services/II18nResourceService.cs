using AuthServer.DTOs;

namespace AuthServer.Services;

public interface II18nResourceService
{
    Task<List<I18nResourceDto>> GetAllResourcesAsync();
    Task<List<I18nResourceDto>> GetResourcesByLocaleAsync(string locale);
    Task<PagedI18nResourceDto> GetPagedResourcesAsync(SearchI18nParams searchParams);
    Task<I18nResourceDto> CreateResourceAsync(CreateI18nResourceDto request);
    Task<bool> UpdateResourceAsync(int id, CreateI18nResourceDto request);
    Task<bool> DeleteResourceAsync(int id);
    Task EnsureResourceExistsAsync(string locale, string key, string? defaultValue);
}
