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
    private readonly string _videoUploadPath;
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
        _videoUploadPath = Path.Combine(localPath, "Video");
        _thumbnailPath = Path.Combine(localPath, "Thumbnail");
        _mediumPath = Path.Combine(localPath, "Medium");
        _largePath = Path.Combine(localPath, "Large");
        _cachePath = Path.Combine(localPath, "Cache");

        // 각 규격별 폴더 자동 생성
        if (!Directory.Exists(_baseUploadPath)) Directory.CreateDirectory(_baseUploadPath);
        if (!Directory.Exists(_videoUploadPath)) Directory.CreateDirectory(_videoUploadPath);
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

        // 비디오 파일 여부 선제 판단
        var contentType = file.ContentType;
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || 
                      extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".avi", StringComparison.OrdinalIgnoreCase);

        // 업로드 저장 대상 폴더 동적 변경 (비디오는 Video 폴더, 나머지는 Original)
        var targetFolder = isVideo ? _videoUploadPath : _baseUploadPath;
        var physicalPath = Path.Combine(targetFolder, storedName);

        // 로컬 디스크에 물리적 파일 저장
        using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 동영상인 경우 WebM 파일로 변환 및 첫 프레임 이미지(썸네일) 추출 처리
        if (isVideo)
        {
            try
            {
                var webmStoredName = $"{fileId}.webm";
                var webmPath = Path.Combine(_videoUploadPath, webmStoredName);
                
                var thumbnailImageName = $"{fileId}.jpg";
                var thumbnailImagePath = Path.Combine(_videoUploadPath, thumbnailImageName);
                
                // ffmpeg 및 ffprobe CLI 명령이 운영체제 PATH에 노출되어 있어야 정상 작동
                _ = Task.Run(() =>
                {
                    try
                    {
                        // 1. WebM 트랜스코딩 프로세스 시작
                        var webmProcessInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-i \"{physicalPath}\" -c:v libvpx-vp9 -crf 30 -b:v 0 -c:a libopus \"{webmPath}\" -y",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var process = System.Diagnostics.Process.Start(webmProcessInfo))
                        {
                            process?.WaitForExit();
                        }

                        // 2. 영상 첫 프레임 추출 프로세스 시작 (빠르게 첫 프레임 한 장만 캡처)
                        var thumbProcessInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-ss 00:00:00 -i \"{physicalPath}\" -vframes 1 -q:v 2 \"{thumbnailImagePath}\" -y",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var process = System.Diagnostics.Process.Start(thumbProcessInfo))
                        {
                            process?.WaitForExit();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Exception] FFmpeg process execution failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to initiate video processing: {ex.Message}");
            }
        }

        // DB에 메타데이터 저장
        var metadata = new FileMetadata
        {
            Id = fileId,
            OriginalName = file.FileName,
            StoredName = storedName,
            Path = Path.Combine(isVideo ? "Video" : "Original", storedName),
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

        // DB에 기록된 경로 타입에 따라 로드 디렉토리 분기
        var targetFolder = metadata.Path.StartsWith("Video", StringComparison.OrdinalIgnoreCase) ? _videoUploadPath : _baseUploadPath;
        var physicalPath = Path.Combine(targetFolder, metadata.StoredName);

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

    /// <inheritdoc />
    public async Task<List<FileMetadata>> UploadGroupFilesAsync(List<IFormFile> files, Guid? groupId, string bizType, string? userId)
    {
        if (files == null || files.Count == 0)
        {
            throw new ArgumentException("업로드할 파일이 존재하지 않습니다.");
        }

        // 1. 그룹 ID가 제공되지 않은 경우 생성 및 저장
        FileGroup? group = null;
        if (groupId == null || groupId == Guid.Empty)
        {
            groupId = Guid.NewGuid();
            group = new FileGroup
            {
                Id = groupId.Value,
                BizType = bizType,
                CreatedBy = userId ?? "System",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            _dbContext.FileGroups.Add(group);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            group = await _dbContext.FileGroups.FirstOrDefaultAsync(x => x.Id == groupId && !x.IsDeleted);
            if (group == null)
            {
                throw new InvalidOperationException("지정된 파일 그룹을 찾을 수 없습니다.");
            }
        }

        var uploadedFiles = new List<FileMetadata>();
        
        // 현재 그룹에 이미 존재하는 대표 파일이 있는지 검사
        var hasRepresentative = await _dbContext.FileMetadatas
            .AnyAsync(x => x.FileGroupId == groupId && x.IsRepresentative && !x.IsDeleted);

        // 현재 그룹의 최대 SortOrder 획득
        var maxSortOrder = 0;
        var existingFiles = await _dbContext.FileMetadatas
            .Where(x => x.FileGroupId == groupId && !x.IsDeleted)
            .ToListAsync();
        if (existingFiles.Any())
        {
            maxSortOrder = existingFiles.Max(x => x.SortOrder);
        }

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var fileId = Guid.NewGuid();
            var extension = Path.GetExtension(file.FileName);
            var storedName = $"{fileId}{extension}";
            var physicalPath = Path.Combine(_baseUploadPath, storedName);

            // 로컬 파일 디스크 저장
            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var contentType = file.ContentType;
            var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            // 이 파일이 대표 사진이 되어야 하는가?
            // 그룹에 아직 대표 사진이 없고, 업로드하려는 파일 중 첫 번째 파일인 경우 대표 사진으로 지정
            var isRepresentative = !hasRepresentative && i == 0;

            var metadata = new FileMetadata
            {
                Id = fileId,
                FileGroupId = groupId,
                OriginalName = file.FileName,
                StoredName = storedName,
                Path = Path.Combine("Original", storedName),
                Size = file.Length,
                ContentType = contentType,
                IsImage = isImage,
                IsRepresentative = isRepresentative,
                SortOrder = maxSortOrder + i + 1,
                CreatedBy = userId ?? "System",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _dbContext.FileMetadatas.Add(metadata);
            uploadedFiles.Add(metadata);
        }

        await _dbContext.SaveChangesAsync();
        return uploadedFiles;
    }

    /// <inheritdoc />
    public async Task<List<FileMetadata>> GetGroupFilesAsync(Guid groupId)
    {
        return await _dbContext.FileMetadatas
            .Where(x => x.FileGroupId == groupId && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<bool> SetRepresentativeFileAsync(Guid groupId, Guid fileId)
    {
        var groupExists = await _dbContext.FileGroups.AnyAsync(x => x.Id == groupId && !x.IsDeleted);
        if (!groupExists) return false;

        var files = await _dbContext.FileMetadatas
            .Where(x => x.FileGroupId == groupId && !x.IsDeleted)
            .ToListAsync();

        var targetFile = files.FirstOrDefault(x => x.Id == fileId);
        if (targetFile == null) return false;

        foreach (var file in files)
        {
            file.IsRepresentative = (file.Id == fileId);
            file.UpdatedAt = DateTime.UtcNow;
            file.UpdatedBy = "System";
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }
}

