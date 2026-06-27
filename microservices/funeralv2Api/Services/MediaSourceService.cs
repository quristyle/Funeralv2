using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using System.Text.RegularExpressions;

namespace funeralv2Api.Services;

/// <summary>
/// 미디어 소스 관리 서비스 구현체
/// </summary>
public class MediaSourceService : IMediaSourceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MediaSourceService> _logger;
    private readonly IConfiguration _configuration;

    public MediaSourceService(AppDbContext context, ILogger<MediaSourceService> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
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
            WebmUrl = x.WebmUrl,
            Status = x.Status,
            HasWebm = x.HasWebm,
            HasThumbnail = x.HasThumbnail,
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
            WebmUrl = dto.WebmUrl,
            Status = dto.Status ?? "PROCESSING",
            HasWebm = dto.HasWebm,
            HasThumbnail = dto.HasThumbnail,
            FileSize = dto.FileSize,
            SortOrder = dto.SortOrder,
            Remark = dto.Remark,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<MediaSource>().Add(source);
        await _context.SaveChangesAsync();

        // 비디오 소스인 경우 업로드된 파일 ID를 추출하여 FileServer에 트랜스코딩 작업 트리거
        if (dto.SourceType == "VIDEO" && !string.IsNullOrEmpty(dto.Url))
        {
            var match = Regex.Match(dto.Url, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            if (match.Success && Guid.TryParse(match.Value, out var fileId))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var fileServerUrl = _configuration["FileServer:BaseUrl"] ?? "http://localhost:5350";
                        using var client = new HttpClient();
                        var triggerUrl = $"{fileServerUrl.TrimEnd('/')}/transcode/{fileId}";
                        _logger.LogInformation("Triggering video transcoding at FileServer: {Url}", triggerUrl);
                        
                        var response = await client.PostAsync(triggerUrl, null);
                        _logger.LogInformation("FileServer transcoding trigger response: {StatusCode}", response.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to trigger video transcoding on FileServer for fileId: {FileId}", fileId);
                    }
                });
            }
        }

        return new MediaSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            ShortName = source.ShortName,
            SourceType = source.SourceType,
            Url = source.Url,
            ThumbnailUrl = source.ThumbnailUrl,
            WebmUrl = source.WebmUrl,
            Status = source.Status,
            HasWebm = source.HasWebm,
            HasThumbnail = source.HasThumbnail,
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

    /// <inheritdoc />
    public async Task<MediaSourceDto?> UpdateMediaSourceStatusAsync(string id, MediaSourceStatusUpdateDto dto)
    {
        _logger.LogInformation("Updating media source status. Id: {Id}, Status: {Status}", id, dto.Status);
        
        // id는 URL의 다운로드 식별 UUID(36자리) 또는 MediaSource 자체의 고유키(N32자리) 둘 중 하나를 유연하게 찾아 처리
        var source = await _context.Set<MediaSource>()
            .FirstOrDefaultAsync(x => (x.Id == id || x.Url.Contains(id)) && !x.IsDeleted);

        if (source == null) return null;

        source.Status = dto.Status;
        source.HasWebm = dto.HasWebm;
        source.HasThumbnail = dto.HasThumbnail;
        
        if (dto.ThumbnailUrl != null)
        {
            source.ThumbnailUrl = dto.ThumbnailUrl;
        }
        if (dto.WebmUrl != null)
        {
            source.WebmUrl = dto.WebmUrl;
        }

        source.UpdatedAt = DateTime.UtcNow;
        source.UpdatedBy = "System";

        await _context.SaveChangesAsync();

        return new MediaSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            ShortName = source.ShortName,
            SourceType = source.SourceType,
            Url = source.Url,
            ThumbnailUrl = source.ThumbnailUrl,
            WebmUrl = source.WebmUrl,
            Status = source.Status,
            HasWebm = source.HasWebm,
            HasThumbnail = source.HasThumbnail,
            FileSize = source.FileSize,
            SortOrder = source.SortOrder,
            Remark = source.Remark
        };
    }
}
