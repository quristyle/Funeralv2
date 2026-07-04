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
            ThumbnailFileId = x.ThumbnailFileId,
            WebmUrl = x.WebmUrl,
            WebmFileId = x.WebmFileId,
            OggUrl = x.OggUrl,
            OggFileId = x.OggFileId,
            AacUrl = x.AacUrl,
            AacFileId = x.AacFileId,
            OriginalFileId = x.OriginalFileId,
            Status = x.Status,
            ErrorMessage = x.ErrorMessage,
            ConversionStartedAt = x.ConversionStartedAt,
            ConversionCompletedAt = x.ConversionCompletedAt,
            ConversionCommand = x.ConversionCommand,
            HasWebm = x.HasWebm,
            HasThumbnail = x.HasThumbnail,
            HasOgg = x.HasOgg,
            HasAac = x.HasAac,
            FileSize = x.FileSize,
            SortOrder = x.SortOrder,
            Remark = x.Remark
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<MediaSourceDto?> GetMediaSourceByIdAsync(string id)
    {
        _logger.LogInformation("Retrieving media source for ID: {Id}", id);
        var source = await _context.Set<MediaSource>()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (source == null)
        {
            return null;
        }

        return new MediaSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            ShortName = source.ShortName,
            SourceType = source.SourceType,
            Url = source.Url,
            ThumbnailUrl = source.ThumbnailUrl,
            ThumbnailFileId = source.ThumbnailFileId,
            WebmUrl = source.WebmUrl,
            WebmFileId = source.WebmFileId,
            OggUrl = source.OggUrl,
            OggFileId = source.OggFileId,
            AacUrl = source.AacUrl,
            AacFileId = source.AacFileId,
            OriginalFileId = source.OriginalFileId,
            Status = source.Status,
            ErrorMessage = source.ErrorMessage,
            ConversionStartedAt = source.ConversionStartedAt,
            ConversionCompletedAt = source.ConversionCompletedAt,
            ConversionCommand = source.ConversionCommand,
            HasWebm = source.HasWebm,
            HasThumbnail = source.HasThumbnail,
            HasOgg = source.HasOgg,
            HasAac = source.HasAac,
            FileSize = source.FileSize,
            SortOrder = source.SortOrder,
            Remark = source.Remark
        };
    }

    /// <inheritdoc />
    public async Task<MediaSourceDto> CreateMediaSourceAsync(MediaSourceCreateDto dto)
    {
        _logger.LogInformation("Creating new media source. Name: {Name}, Type: {Type}", dto.Name, dto.SourceType);
        
        Guid? parsedOriginalFileId = null;
        if (!string.IsNullOrEmpty(dto.Url))
        {
            var match = Regex.Match(dto.Url, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            if (match.Success && Guid.TryParse(match.Value, out var fileId))
            {
                parsedOriginalFileId = fileId;
            }
        }

        var source = new MediaSource
        {
            Name = dto.Name,
            ShortName = dto.ShortName,
            SourceType = dto.SourceType,
            Url = dto.Url,
            ThumbnailUrl = dto.ThumbnailUrl,
            ThumbnailFileId = dto.ThumbnailFileId,
            WebmUrl = dto.WebmUrl,
            WebmFileId = dto.WebmFileId,
            OggUrl = dto.OggUrl,
            OggFileId = dto.OggFileId,
            AacUrl = dto.AacUrl,
            AacFileId = dto.AacFileId,
            OriginalFileId = parsedOriginalFileId ?? dto.OriginalFileId,
            Status = dto.Status ?? "PROCESSING",
            ErrorMessage = dto.ErrorMessage,
            ConversionStartedAt = dto.ConversionStartedAt,
            ConversionCompletedAt = dto.ConversionCompletedAt,
            ConversionCommand = dto.ConversionCommand,
            HasWebm = dto.HasWebm,
            HasThumbnail = dto.ThumbnailFileId.HasValue || !string.IsNullOrEmpty(dto.ThumbnailUrl) || dto.HasThumbnail,
            HasOgg = dto.HasOgg,
            HasAac = dto.HasAac,
            FileSize = dto.FileSize,
            SortOrder = dto.SortOrder,
            Remark = dto.Remark,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<MediaSource>().Add(source);
        await _context.SaveChangesAsync();

        // 비디오 혹은 오디오 소스인 경우 업로드된 파일 ID를 추출하여 FileServer에 트랜스코딩 작업 트리거
        if ((dto.SourceType == "VIDEO" || dto.SourceType == "AUDIO") && parsedOriginalFileId.HasValue)
        {
            var fileId = parsedOriginalFileId.Value;
            _ = Task.Run(async () =>
            {
                try
                {
                    var fileServerUrl = _configuration["FileServer:BaseUrl"] ?? "http://localhost:5350";
                    using var client = new HttpClient();
                    
                    // 비디오 소스인 경우 썸네일은 이미 업로드 시점에 생성되었으므로 WebM 변환만 트리거
                    // 오디오 소스인 경우 앨범아트는 이미 업로드 시점에 생성되었으므로 오디오 단독 인코딩만 트리거
                    var triggerUrl = dto.SourceType == "VIDEO"
                        ? $"{fileServerUrl.TrimEnd('/')}/transcode/webm/{fileId}"
                        : dto.SourceType == "AUDIO"
                            ? $"{fileServerUrl.TrimEnd('/')}/transcode/audio-only/{fileId}"
                            : $"{fileServerUrl.TrimEnd('/')}/transcode/{fileId}";
                        
                    _logger.LogInformation("Triggering media transcoding at FileServer: {Url}", triggerUrl);
                    
                    var response = await client.PostAsync(triggerUrl, null);
                    _logger.LogInformation("FileServer transcoding trigger response: {StatusCode}", response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to trigger media transcoding on FileServer for fileId: {FileId}", fileId);
                }
            });
        }

        return new MediaSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            ShortName = source.ShortName,
            SourceType = source.SourceType,
            Url = source.Url,
            ThumbnailUrl = source.ThumbnailUrl,
            ThumbnailFileId = source.ThumbnailFileId,
            WebmUrl = source.WebmUrl,
            WebmFileId = source.WebmFileId,
            OggUrl = source.OggUrl,
            OggFileId = source.OggFileId,
            AacUrl = source.AacUrl,
            AacFileId = source.AacFileId,
            OriginalFileId = source.OriginalFileId,
            Status = source.Status,
            ErrorMessage = source.ErrorMessage,
            ConversionStartedAt = source.ConversionStartedAt,
            ConversionCompletedAt = source.ConversionCompletedAt,
            ConversionCommand = source.ConversionCommand,
            HasWebm = source.HasWebm,
            HasThumbnail = source.HasThumbnail,
            HasOgg = source.HasOgg,
            HasAac = source.HasAac,
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

        if (dto.Status != null)
        {
            source.Status = dto.Status;
        }
        
        // 에러메시지는 상태가 변경되거나 값이 넘어왔을 때 업데이트
        source.ErrorMessage = dto.ErrorMessage;

        if (dto.ConversionStartedAt.HasValue)
        {
            source.ConversionStartedAt = dto.ConversionStartedAt.Value;
        }
        if (dto.ConversionCompletedAt.HasValue)
        {
            source.ConversionCompletedAt = dto.ConversionCompletedAt.Value;
        }
        if (dto.ConversionCommand != null)
        {
            source.ConversionCommand = dto.ConversionCommand;
        }
        
        if (dto.HasWebm.HasValue)
        {
            source.HasWebm = dto.HasWebm.Value;
        }
        if (dto.HasThumbnail.HasValue)
        {
            source.HasThumbnail = dto.HasThumbnail.Value;
        }
        if (dto.HasOgg.HasValue)
        {
            source.HasOgg = dto.HasOgg.Value;
        }
        if (dto.HasAac.HasValue)
        {
            source.HasAac = dto.HasAac.Value;
        }
        
        if (dto.ThumbnailUrl != null)
        {
            source.ThumbnailUrl = dto.ThumbnailUrl;
        }
        if (dto.ThumbnailFileId.HasValue)
        {
            source.ThumbnailFileId = dto.ThumbnailFileId.Value;
        }
        if (dto.WebmUrl != null)
        {
            source.WebmUrl = dto.WebmUrl;
        }
        if (dto.WebmFileId.HasValue)
        {
            source.WebmFileId = dto.WebmFileId.Value;
        }
        if (dto.OggUrl != null)
        {
            source.OggUrl = dto.OggUrl;
        }
        if (dto.OggFileId.HasValue)
        {
            source.OggFileId = dto.OggFileId.Value;
        }
        if (dto.AacUrl != null)
        {
            source.AacUrl = dto.AacUrl;
        }
        if (dto.AacFileId.HasValue)
        {
            source.AacFileId = dto.AacFileId.Value;
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
            ThumbnailFileId = source.ThumbnailFileId,
            WebmUrl = source.WebmUrl,
            WebmFileId = source.WebmFileId,
            OggUrl = source.OggUrl,
            OggFileId = source.OggFileId,
            AacUrl = source.AacUrl,
            AacFileId = source.AacFileId,
            OriginalFileId = source.OriginalFileId,
            Status = source.Status,
            ErrorMessage = source.ErrorMessage,
            ConversionStartedAt = source.ConversionStartedAt,
            ConversionCompletedAt = source.ConversionCompletedAt,
            ConversionCommand = source.ConversionCommand,
            HasWebm = source.HasWebm,
            HasThumbnail = source.HasThumbnail,
            HasOgg = source.HasOgg,
            HasAac = source.HasAac,
            FileSize = source.FileSize,
            SortOrder = source.SortOrder,
            Remark = source.Remark
        };
    }

    /// <inheritdoc />
    public async Task<bool> RetryThumbnailAsync(string id)
    {
        _logger.LogInformation("Retrying media thumbnail extraction. Id: {Id}", id);
        
        var source = await _context.Set<MediaSource>()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (source == null || !source.OriginalFileId.HasValue)
        {
            _logger.LogWarning("Media source not found or has no OriginalFileId. Id: {Id}", id);
            return false;
        }

        // 썸네일 정보 리셋 (상태 status는 유지)
        source.HasThumbnail = false;
        source.ThumbnailUrl = null;
        source.ThumbnailFileId = null;

        source.UpdatedAt = DateTime.UtcNow;
        source.UpdatedBy = "System";

        await _context.SaveChangesAsync();

        // FileServer에 썸네일 재추출 작업 요청 트리거
        var fileId = source.OriginalFileId.Value;
        _ = Task.Run(async () =>
        {
            try
            {
                var fileServerUrl = _configuration["FileServer:BaseUrl"] ?? "http://localhost:5350";
                using var client = new HttpClient();
                var triggerUrl = $"{fileServerUrl.TrimEnd('/')}/transcode/thumbnail/{fileId}";
                _logger.LogInformation("Triggering media thumbnail extraction at FileServer: {Url}", triggerUrl);
                
                var response = await client.PostAsync(triggerUrl, null);
                _logger.LogInformation("FileServer thumbnail trigger response: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger media thumbnail extraction on FileServer for fileId: {FileId}", fileId);
            }
        });

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RetryWebmAsync(string id)
    {
        _logger.LogInformation("Retrying media webm transcoding. Id: {Id}", id);
        
        var source = await _context.Set<MediaSource>()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (source == null || !source.OriginalFileId.HasValue)
        {
            _logger.LogWarning("Media source not found or has no OriginalFileId. Id: {Id}", id);
            return false;
        }

        // webm 변환 재시도 상태로 설정
        source.Status = "PROCESSING";
        source.ErrorMessage = null;
        source.HasWebm = false;

        source.UpdatedAt = DateTime.UtcNow;
        source.UpdatedBy = "System";

        await _context.SaveChangesAsync();

        // FileServer에 webm 변환 작업 요청 트리거
        var fileId = source.OriginalFileId.Value;
        _ = Task.Run(async () =>
        {
            try
            {
                var fileServerUrl = _configuration["FileServer:BaseUrl"] ?? "http://localhost:5350";
                using var client = new HttpClient();
                var triggerUrl = $"{fileServerUrl.TrimEnd('/')}/transcode/webm/{fileId}";
                _logger.LogInformation("Triggering media webm transcoding at FileServer: {Url}", triggerUrl);
                
                var response = await client.PostAsync(triggerUrl, null);
                _logger.LogInformation("FileServer webm trigger response: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger media webm transcoding on FileServer for fileId: {FileId}", fileId);
            }
        });

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RetryAudioAsync(string id)
    {
        _logger.LogInformation("Retrying media audio transcoding. Id: {Id}", id);
        
        var source = await _context.Set<MediaSource>()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (source == null || !source.OriginalFileId.HasValue)
        {
            _logger.LogWarning("Media source not found or has no OriginalFileId. Id: {Id}", id);
            return false;
        }

        // audio 변환 재시도 상태로 설정
        source.Status = "PROCESSING";
        source.ErrorMessage = null;
        source.HasOgg = false;
        source.HasAac = false;

        source.UpdatedAt = DateTime.UtcNow;
        source.UpdatedBy = "System";

        await _context.SaveChangesAsync();

        // FileServer에 audio 변환 작업 요청 트리거
        var fileId = source.OriginalFileId.Value;
        _ = Task.Run(async () =>
        {
            try
            {
                var fileServerUrl = _configuration["FileServer:BaseUrl"] ?? "http://localhost:5350";
                using var client = new HttpClient();
                var triggerUrl = $"{fileServerUrl.TrimEnd('/')}/transcode/{fileId}";
                _logger.LogInformation("Triggering media audio transcoding at FileServer: {Url}", triggerUrl);
                
                var response = await client.PostAsync(triggerUrl, null);
                _logger.LogInformation("FileServer audio trigger response: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger media audio transcoding on FileServer for fileId: {FileId}", fileId);
            }
        });

        return true;
    }

    /// <inheritdoc />
    public async Task<MediaSourceDto?> UpdateMediaSourceAsync(string id, MediaSourceUpdateDto dto)
    {
        _logger.LogInformation("Updating media source info. Id: {Id}", id);

        var source = await _context.Set<MediaSource>()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (source == null) return null;

        source.Name = dto.Name;
        source.ShortName = dto.ShortName;
        source.SortOrder = dto.SortOrder;
        source.Remark = dto.Remark;

        // 이미지 교체 시: Url/ThumbnailUrl/ThumbnailFileId/OriginalFileId 업데이트
        if (!string.IsNullOrEmpty(dto.Url))
        {
            source.Url = dto.Url;
        }
        if (dto.ThumbnailUrl != null)
        {
            source.ThumbnailUrl = dto.ThumbnailUrl;
            source.HasThumbnail = !string.IsNullOrEmpty(dto.ThumbnailUrl);
        }
        if (dto.ThumbnailFileId.HasValue)
        {
            source.ThumbnailFileId = dto.ThumbnailFileId.Value;
        }
        if (dto.OriginalFileId.HasValue)
        {
            source.OriginalFileId = dto.OriginalFileId.Value;
            // 이미지 교체 시 파일 연결이 변경되었으므로 상태 초기화
            if (source.SourceType == "IMAGE")
            {
                source.Status = "COMPLETED";
            }
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
            ThumbnailFileId = source.ThumbnailFileId,
            WebmUrl = source.WebmUrl,
            WebmFileId = source.WebmFileId,
            OggUrl = source.OggUrl,
            OggFileId = source.OggFileId,
            AacUrl = source.AacUrl,
            AacFileId = source.AacFileId,
            OriginalFileId = source.OriginalFileId,
            Status = source.Status,
            ErrorMessage = source.ErrorMessage,
            ConversionStartedAt = source.ConversionStartedAt,
            ConversionCompletedAt = source.ConversionCompletedAt,
            ConversionCommand = source.ConversionCommand,
            HasWebm = source.HasWebm,
            HasThumbnail = source.HasThumbnail,
            HasOgg = source.HasOgg,
            HasAac = source.HasAac,
            FileSize = source.FileSize,
            SortOrder = source.SortOrder,
            Remark = source.Remark
        };
    }
}
