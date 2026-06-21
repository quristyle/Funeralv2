using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

public class I18nResourceService : II18nResourceService
{
    private readonly AppDbContext _context;

    public I18nResourceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<I18nResourceDto>> GetAllResourcesAsync()
    {
        return await _context.I18nResources
            .Select(r => new I18nResourceDto
            {
                Id = r.Id,
                Key = r.Key,
                Locale = r.Locale,
                Value = r.Value,
                Category = r.Category
            })
            .ToListAsync();
    }

    public async Task<List<I18nResourceDto>> GetResourcesByLocaleAsync(string locale)
    {
        return await _context.I18nResources
            .Where(r => r.Locale == locale)
            .Select(r => new I18nResourceDto
            {
                Id = r.Id,
                Key = r.Key,
                Locale = r.Locale,
                Value = r.Value,
                Category = r.Category
            })
            .ToListAsync();
    }

    public async Task<List<I18nResourceDto>> GetPagedResourcesAsync(SearchI18nParams searchParams)
    {
        var query = _context.I18nResources.AsQueryable();

        // 필터링 적용
        if (!string.IsNullOrEmpty(searchParams.Locale))
            query = query.Where(r => r.Locale == searchParams.Locale);
        
        if (!string.IsNullOrEmpty(searchParams.Category))
            query = query.Where(r => r.Category != null && r.Category.Contains(searchParams.Category));
        
        if (!string.IsNullOrEmpty(searchParams.Key))
            query = query.Where(r => r.Key.Contains(searchParams.Key));
        
        if (!string.IsNullOrEmpty(searchParams.Value))
            query = query.Where(r => r.Value.Contains(searchParams.Value));

        // 전체 개수 조회
        var total = await query.CountAsync();

        // 페이징 적용
        var items = await query
            .OrderByDescending(r => r.Id)
            .Skip((searchParams.Page - 1) * searchParams.PageSize)
            .Take(searchParams.PageSize)
            .Select(r => new I18nResourceDto
            {
                Id = r.Id,
                Key = r.Key,
                Locale = r.Locale,
                Value = r.Value,
                Category = r.Category
            })
            .ToListAsync();
return items;
        // return new PagedI18nResourceDto
        // {
        //     Items = items,
        //     Total = total
        // };
    }

    public async Task<I18nResourceDto> CreateResourceAsync(CreateI18nResourceDto request)
    {
        var resource = new I18nResource
        {
            Key = request.Key,
            Locale = request.Locale,
            Value = request.Value,
            Category = request.Category
        };

        _context.I18nResources.Add(resource);
        await _context.SaveChangesAsync();

        return new I18nResourceDto
        {
            Id = resource.Id,
            Key = resource.Key,
            Locale = resource.Locale,
            Value = resource.Value,
            Category = resource.Category
        };
    }

    public async Task<bool> UpdateResourceAsync(int id, CreateI18nResourceDto request)
    {
        var resource = await _context.I18nResources.FindAsync(id);
        if (resource == null) return false;

        resource.Key = request.Key;
        resource.Locale = request.Locale;
        resource.Value = request.Value;
        resource.Category = request.Category;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteResourceAsync(int id)
    {
        var resource = await _context.I18nResources.FindAsync(id);
        if (resource == null) return false;

        _context.I18nResources.Remove(resource);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task EnsureResourceExistsAsync(string locale, string key, string? defaultValue)
    {
        // 1. 현재 로케일에 이미 존재하는지 확인
        var exists = await _context.I18nResources
            .AnyAsync(r => r.Locale == locale && r.Key == key);
        
        if (!exists)
        {
            // 2. 결정된 기본값 (전달받은 값 -> DB의 영어 값 -> 찾지못함 접미사 순)
            string finalValue;

            if (!string.IsNullOrEmpty(defaultValue))
            {
                finalValue = defaultValue;
            }
            else
            {
                // DB에서 영어(en-US) 로케일에 해당 키가 있는지 확인 (Secondary Fallback)
                var englishValue = await _context.I18nResources
                    .Where(r => r.Locale == "en-US" && r.Key == key)
                    .Select(r => r.Value)
                    .FirstOrDefaultAsync();
                
                finalValue = !string.IsNullOrEmpty(englishValue) 
                    ? englishValue 
                    : $"{key}(찾지못함)";
            }

            var category = key.Contains('.') ? key.Split('.')[0] : "auto";
            var resource = new I18nResource
            {
                Locale = locale,
                Key = key,
                Value = finalValue,
                Category = category,
                CreatedBy = "System-Auto",
                UpdatedBy = "System-Auto"
            };

            _context.I18nResources.Add(resource);
            await _context.SaveChangesAsync();
        }
    }
}
