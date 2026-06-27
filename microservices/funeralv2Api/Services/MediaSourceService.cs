using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;

namespace funeralv2Api.Services;

/// <summary>
/// 미디어 소스 관리 서비스 구현체
/// </summary>
public class MediaSourceService : IMediaSourceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MediaSourceService> _logger;

    public MediaSourceService(AppDbContext context, ILogger<MediaSourceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<MediaSourceDto>> GetMediaSourcesAsync(string? type)
    {
        _logger.LogInformation("Retrieving media sources list for type: {Type}", type);
        var query = _context.Set<MediaSource>()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(x => x.SourceType == type);
        }

        var list = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return list.Select(x => new MediaSourceDto
        {
            Id = x.Id,
            Name = x.Name,
            ShortName = x.ShortName,
            SourceType = x.SourceType,
            Url = x.Url,
            ThumbnailUrl = x.ThumbnailUrl,
            FileSize = x.FileSize,
            SortOrder = x.SortOrder,
            Remark = x.Remark
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<MediaSourceDto> CreateMediaSourceAsync(MediaSourceCreateDto dto)
    {
        _logger.LogInformation("Creating new media source. Name: {Name}, Type: {Type}", dto.Name, dto.SourceType);
        
        var source = new MediaSource
        {
            Name = dto.Name,
            ShortName = dto.ShortName,
            SourceType = dto.SourceType,
            Url = dto.Url,
            ThumbnailUrl = dto.ThumbnailUrl,
            FileSize = dto.FileSize,
            SortOrder = dto.SortOrder,
            Remark = dto.Remark,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<MediaSource>().Add(source);
        await _context.SaveChangesAsync();

        return new MediaSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            ShortName = source.ShortName,
            SourceType = source.SourceType,
            Url = source.Url,
            ThumbnailUrl = source.ThumbnailUrl,
            FileSize = source.FileSize,
            SortOrder = source.SortOrder,
            Remark = source.Remark
        };
    }

    /// <inheritdoc />
    public async Task<bool> DeleteMediaSourceAsync(string id)
    {
        _logger.LogInformation("Deleting media source. Id: {Id}", id);
        
        var source = await _context.Set<MediaSource>()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (source == null) return false;

        source.IsDeleted = true;
        source.UpdatedAt = DateTime.UtcNow;
        source.UpdatedBy = "System";

        await _context.SaveChangesAsync();
        return true;
    }
}
