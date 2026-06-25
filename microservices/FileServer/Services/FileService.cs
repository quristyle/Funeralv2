using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FileServer.Data;
using FileServer.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace FileServer.Services;

/// <summary>
/// 파일 처리 비즈니스 로직을 구현한 서비스 클래스
/// </summary>
public class FileService : IFileService
{
    private readonly FileDbContext _dbContext;
    private readonly string _baseUploadPath;
    private readonly string _thumbnailPath;
    private readonly string _mediumPath;
    private readonly string _largePath;
    private readonly string _cachePath;

    public FileService(FileDbContext dbContext, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _dbContext = dbContext;
        
        // 설정에서 파일 저장소 경로 획득, 없으면 실행 경로 기준 Uploads로 폴백
        var localPath = configuration["Storage:LocalPath"] ?? Path.Combine(AppContext.BaseDirectory, "Uploads");
        _baseUploadPath = Path.Combine(localPath, "Original");
        _thumbnailPath = Path.Combine(localPath, "Thumbnail");
        _mediumPath = Path.Combine(localPath, "Medium");
        _largePath = Path.Combine(localPath, "Large");
        _cachePath = Path.Combine(localPath, "Cache");

        // 각 규격별 폴더 자동 생성
        if (!Directory.Exists(_baseUploadPath)) Directory.CreateDirectory(_baseUploadPath);
        if (!Directory.Exists(_thumbnailPath)) Directory.CreateDirectory(_thumbnailPath);
        if (!Directory.Exists(_mediumPath)) Directory.CreateDirectory(_mediumPath);
        if (!Directory.Exists(_largePath)) Directory.CreateDirectory(_largePath);
        if (!Directory.Exists(_cachePath)) Directory.CreateDirectory(_cachePath);
    }

    /// <inheritdoc />
    public async Task<FileMetadata> UploadFileAsync(IFormFile file, string? userId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("파일이 유효하지 않습니다.");
        }

        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(file.FileName);
        var storedName = $"{fileId}{extension}";
        var physicalPath = Path.Combine(_baseUploadPath, storedName);

        // 로컬 디스크에 물리적 파일 저장 (원본 보존)
        using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 이미지 파일 여부 판단
        var contentType = file.ContentType;
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        // DB에 메타데이터 저장
        var metadata = new FileMetadata
        {
            Id = fileId,
            OriginalName = file.FileName,
            StoredName = storedName,
            Path = Path.Combine("Original", storedName),
            Size = file.Length,
            ContentType = contentType,
            IsImage = isImage,
            CreatedBy = userId ?? "System",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _dbContext.FileMetadatas.Add(metadata);
        await _dbContext.SaveChangesAsync();

        return metadata;
    }

    /// <inheritdoc />
    public async Task<(Stream FileStream, string ContentType, string OriginalName)> DownloadFileAsync(Guid id)
    {
        var metadata = await _dbContext.FileMetadatas
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (metadata == null)
        {
            throw new FileNotFoundException("파일 메타데이터를 찾을 수 없습니다.");
        }

        var physicalPath = Path.Combine(_baseUploadPath, metadata.StoredName);
        if (!File.Exists(physicalPath))
        {
            throw new FileNotFoundException("서버 물리 스토리지에 파일이 존재하지 않습니다.");
        }

        var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read);
        return (stream, metadata.ContentType, metadata.OriginalName);
    }

    /// <inheritdoc />
    public async Task<(Stream FileStream, string ContentType)> GetThumbnailAsync(Guid id)
    {
        // 썸네일 규격: 150x150
        return await GetPresetImageAsync(id, _thumbnailPath, 150, 150);
    }

    /// <inheritdoc />
    public async Task<(Stream FileStream, string ContentType)> GetMediumImageAsync(Guid id)
    {
        // 미디움 규격: 600x600
        return await GetPresetImageAsync(id, _mediumPath, 600, 600);
    }

    /// <inheritdoc />
    public async Task<(Stream FileStream, string ContentType)> GetLargeImageAsync(Guid id)
    {
        // 라지 규격: 1200x1200
        return await GetPresetImageAsync(id, _largePath, 1200, 1200);
    }

    /// <summary>
    /// 지정된 크기 규격(Preset)의 이미지를 조회 혹은 Lazy Generation 방식으로 WebP 포맷 생성하여 반환
    /// </summary>
    private async Task<(Stream FileStream, string ContentType)> GetPresetImageAsync(Guid id, string targetFolderPath, int width, int height)
    {
        var metadata = await _dbContext.FileMetadatas
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (metadata == null)
        {
            throw new FileNotFoundException("파일 메타데이터를 찾을 수 없습니다.");
        }

        if (!metadata.IsImage)
        {
            throw new InvalidOperationException("이미지 파일만 크기를 변환할 수 있습니다.");
        }

        var targetFileName = $"{id}.webp";
        var targetFilePath = Path.Combine(targetFolderPath, targetFileName);

        // 1. 이미 존재 시 (연산 없음) 즉시 스트림 반환
        if (File.Exists(targetFilePath))
        {
            var existingStream = new FileStream(targetFilePath, FileMode.Open, FileAccess.Read);
            return (existingStream, "image/webp");
        }

        // 2. 미존재 시 Lazy Generation (원본 기준 썸네일/미디움/라지 WebP 생성)
        var originalPath = Path.Combine(_baseUploadPath, metadata.StoredName);
        if (!File.Exists(originalPath))
        {
            throw new FileNotFoundException("서버 물리 스토리지에 원본 파일이 존재하지 않습니다.");
        }

        using (var image = await Image.LoadAsync(originalPath))
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));

            // WebP 포맷으로 저장
            await image.SaveAsWebpAsync(targetFilePath);
        }

        var stream = new FileStream(targetFilePath, FileMode.Open, FileAccess.Read);
        return (stream, "image/webp");
    }

    /// <inheritdoc />
    public async Task<(Stream FileStream, string ContentType)> GetResizedImageAsync(Guid id, int width, int height)
    {
        var metadata = await _dbContext.FileMetadatas
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (metadata == null)
        {
            throw new FileNotFoundException("파일 메타데이터를 찾을 수 없습니다.");
        }

        if (!metadata.IsImage)
        {
            throw new InvalidOperationException("이미지 파일만 크기를 변환할 수 있습니다.");
        }

        var originalPath = Path.Combine(_baseUploadPath, metadata.StoredName);
        if (!File.Exists(originalPath))
        {
            throw new FileNotFoundException("서버 물리 스토리지에 원본 파일이 존재하지 않습니다.");
        }

        // 임의 크기도 WebP 캐시로 관리
        var cacheFileName = $"{id}_{width}_{height}.webp";
        var cachePath = Path.Combine(_cachePath, cacheFileName);

        // 캐시가 있으면 즉시 반환
        if (File.Exists(cachePath))
        {
            var cachedStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read);
            return (cachedStream, "image/webp");
        }

        // 없으면 WebP로 리사이즈 및 캐싱
        using (var image = await Image.LoadAsync(originalPath))
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));

            await image.SaveAsWebpAsync(cachePath);
        }

        var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read);
        return (stream, "image/webp");
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(Guid id)
    {
        var metadata = await _dbContext.FileMetadatas
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (metadata == null)
        {
            return false;
        }

        // DB Soft Delete 처리
        metadata.IsDeleted = true;
        metadata.UpdatedAt = DateTime.UtcNow;
        metadata.UpdatedBy = "System";

        await _dbContext.SaveChangesAsync();

        // 1. 원본 파일 삭제 시도
        var originalPath = Path.Combine(_baseUploadPath, metadata.StoredName);
        if (File.Exists(originalPath))
        {
            try
            {
                File.Delete(originalPath);
            }
            catch (Exception) { }
        }

        // 2. 각 규격 폴더 내 WebP 파일들 삭제 시도
        var webpFileName = $"{id}.webp";
        
        var thumbnailFilePath = Path.Combine(_thumbnailPath, webpFileName);
        if (File.Exists(thumbnailFilePath)) try { File.Delete(thumbnailFilePath); } catch {}

        var mediumFilePath = Path.Combine(_mediumPath, webpFileName);
        if (File.Exists(mediumFilePath)) try { File.Delete(mediumFilePath); } catch {}

        var largeFilePath = Path.Combine(_largePath, webpFileName);
        if (File.Exists(largeFilePath)) try { File.Delete(largeFilePath); } catch {}

        // 3. 임의 캐시 삭제 시도
        if (Directory.Exists(_cachePath))
        {
            var cachedFiles = Directory.GetFiles(_cachePath, $"{id}_*");
            foreach (var cachedFile in cachedFiles)
            {
                try { File.Delete(cachedFile); } catch { }
            }
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<FileMetadata?> GetMetadataAsync(Guid id)
    {
        return await _dbContext.FileMetadatas
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }
}

