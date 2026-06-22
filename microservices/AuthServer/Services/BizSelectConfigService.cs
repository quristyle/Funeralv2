using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Mapster;

namespace AuthServer.Services;

/// <summary>
/// BizSelect 메타데이터 설정을 관리하는 서비스 클래스
/// </summary>
public class BizSelectConfigService : IBizSelectConfigService
{
    private readonly AppDbContext _context;

    public BizSelectConfigService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BizSelectConfigDto>> GetAllConfigsAsync()
    {
        return await _context.BizSelectConfigs
            .OrderBy(c => c.BizType)
            .ProjectToType<BizSelectConfigDto>()
            .ToListAsync();
    }

    public async Task<BizSelectConfigDto?> GetConfigByIdAsync(string id)
    {
        var config = await _context.BizSelectConfigs.FindAsync(id);
        return config?.Adapt<BizSelectConfigDto>();
    }

    public async Task<BizSelectConfigDto> CreateConfigAsync(BizSelectConfigCreateDto createDto)
    {
        var config = createDto.Adapt<BizSelectConfig>();

        _context.BizSelectConfigs.Add(config);
        await _context.SaveChangesAsync();

        return config.Adapt<BizSelectConfigDto>();
    }

    public async Task<bool> UpdateConfigAsync(string id, BizSelectConfigCreateDto updateDto)
    {
        var config = await _context.BizSelectConfigs.FindAsync(id);
        if (config == null) return false;

        updateDto.Adapt(config);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteConfigAsync(string id)
    {
        var config = await _context.BizSelectConfigs.FindAsync(id);
        if (config == null) return false;

        _context.BizSelectConfigs.Remove(config);
        await _context.SaveChangesAsync();
        return true;
    }
}
