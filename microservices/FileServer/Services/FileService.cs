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
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace FileServer.Services;

/// <summary>
/// 파일 처리 비즈니스 로직을 구현한 서비스 클래스
/// </summary>
public class FileService : IFileService
{
    private readonly FileDbContext _dbContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _localPathRoot;

    public FileService(
        FileDbContext dbContext, 
        Microsoft.Extensions.Configuration.IConfiguration configuration, 
        IServiceScopeFactory scopeFactory)
    {
        _dbContext = dbContext;
        _scopeFactory = scopeFactory;
        // 설정에서 파일 저장소 경로 획득, 없으면 실행 경로 기준 Uploads로 폴백
        _localPathRoot = configuration["Storage:LocalPath"] ?? Path.Combine(AppContext.BaseDirectory, "Uploads");
    }

    private string GetBizFolder(string? bizType)
    {
        if (string.IsNullOrWhiteSpace(bizType))
        {
            return "basecom";
        }

        var trimmed = bizType.Trim();
        if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) || 
            trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase) || 
            trimmed.Equals("general", StringComparison.OrdinalIgnoreCase))
        {
            return "basecom";
        }

        if (trimmed.Equals("DECEASED", StringComparison.OrdinalIgnoreCase))
        {
            return "funeralv2/deceased";
        }

        // 경로 탐색(..) 등의 비정상 문자 제거 및 소문자 정화 (하위 비즈니스 폴더 슬래시는 허용)
        var safeFolder = trimmed.Replace("..", "").Trim('/', '\\');
        return string.IsNullOrEmpty(safeFolder) ? "basecom" : safeFolder.ToLower();
    }

    private string GetPath(string? bizType, string folderName)
    {
        var bizFolder = GetBizFolder(bizType);
        var targetDir = Path.Combine(_localPathRoot, bizFolder, folderName);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        return targetDir;
    }

    /// <inheritdoc />
    public async Task<FileMetadata> UploadFileAsync(IFormFile file, string? userId, string? bizType = null)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("파일이 유효하지 않습니다.");
        }

        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(file.FileName);
        var storedName = $"{fileId}{extension}";

        // 비디오/오디오 여부 선제 판단
        var contentType = file.ContentType;
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || 
                      extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".avi", StringComparison.OrdinalIgnoreCase);

        var isAudio = contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".mpeg", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".aac", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase);

        // 업무 영역(bizType) 획득
        var bizFolder = GetBizFolder(bizType);
        
        // 업로드 저장 대상 폴더 동적 변경 (비디오는 Video 폴더, 오디오는 Audio 폴더, 나머지는 Original)
        var folderName = isVideo ? "Video" : (isAudio ? "Audio" : "Original");
        var physicalPath = Path.Combine(GetPath(bizType, folderName), storedName);

        // 로컬 디스크에 물리적 파일 저장
        using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // DB에 메타데이터 저장 (웹 URL 형태 슬래시 호환)
        var metadata = new FileMetadata
        {
            Id = fileId,
            OriginalName = file.FileName,
            StoredName = storedName,
            Path = $"{bizFolder}/{folderName}/{storedName}",
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
    public async Task StartVideoTranscodingAsync(Guid fileId)
    {
        var metadata = await _dbContext.FileMetadatas.FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted);
        if (metadata == null)
        {
            throw new FileNotFoundException("변환할 비디오 파일의 메타데이터를 찾을 수 없습니다.");
        }

        var extension = Path.GetExtension(metadata.OriginalName);
        var isVideo = metadata.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || 
                      extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".avi", StringComparison.OrdinalIgnoreCase);

        if (!isVideo)
        {
            throw new InvalidOperationException("비디오 파일만 트랜스코딩 처리가 가능합니다.");
        }

        var physicalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";
        var videoFolder = GetPath(bizType, "Video");

        try
        {
            var thumbFileId = Guid.NewGuid();
            var thumbStoredName = $"{thumbFileId}.jpg";
            var thumbnailImagePath = Path.Combine(videoFolder, thumbStoredName);

            var webmFileId = Guid.NewGuid();
            var webmStoredName = $"{webmFileId}.webm";
            var webmPath = Path.Combine(videoFolder, webmStoredName);

            //--------------------------------------------------
            // Thumbnail
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                try
                {
                    var args = $"-ss 00:00:00 -i \"{physicalPath}\" -vframes 1 -q:v 2 \"{thumbnailImagePath}\" -y";
                    Console.WriteLine("[Thumbnail] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromSeconds(30));
                    Console.WriteLine($"[Thumbnail] Success={result.Success}");

                    if (!result.Success)
                    {
                        Console.WriteLine(result.StdErr);
                        return;
                    }

                    var fileInfo = new FileInfo(thumbnailImagePath);
                    var thumbMetadata = new FileMetadata
                    {
                        Id = thumbFileId,
                        OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + "_thumb.jpg",
                        StoredName = thumbStoredName,
                        Path = $"{GetBizFolder(bizType)}/Video/{thumbStoredName}",
                        Size = fileInfo.Exists ? fileInfo.Length : 0,
                        ContentType = "image/jpeg",
                        CreatedBy = "System",
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                        dbContext.FileMetadatas.Add(thumbMetadata);
                        await dbContext.SaveChangesAsync();
                    }

                    await NotifyStatusAsync(fileId, "PROCESSING", true, false, 
                        thumbnailUrl: $"/api/file/download/{thumbFileId}", 
                        thumbnailFileId: thumbFileId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });

            //--------------------------------------------------
            // WEBM
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                var conversionStartedAt = DateTime.UtcNow;
                var args = $"-i \"{physicalPath}\" " +
                           "-vf \"scale='min(1920,iw)':'min(1080,ih)':force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2\" " +
                           "-c:v libvpx-vp9 " +
                           "-crf 30 " +
                           "-b:v 0 " +
                           "-row-mt 1 " +
                           "-cpu-used 4 " +
                           "-threads 2 " +
                           "-c:a libopus " +
                           $"\"{webmPath}\" -y";
                try
                {
                    Console.WriteLine("[WebM] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromMinutes(30));
                    Console.WriteLine($"[WebM] Success={result.Success}");
                    var conversionCompletedAt = DateTime.UtcNow;

                    if (!result.Success)
                    {
                        Console.WriteLine(result.StdErr);
                    }

                    if (result.Success)
                    {
                        var fileInfo = new FileInfo(webmPath);
                        var webmMetadata = new FileMetadata
                        {
                            Id = webmFileId,
                            OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + ".webm",
                            StoredName = webmStoredName,
                            Path = $"{GetBizFolder(bizType)}/Video/{webmStoredName}",
                            Size = fileInfo.Exists ? fileInfo.Length : 0,
                            ContentType = "video/webm",
                            CreatedBy = "System",
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                            dbContext.FileMetadatas.Add(webmMetadata);
                            await dbContext.SaveChangesAsync();
                        }
                    }

                    await NotifyStatusAsync(fileId, result.Success ? "COMPLETED" : "FAILED", true, result.Success, 
                        webmUrl: result.Success ? $"/api/file/download/{webmFileId}" : null, 
                        webmFileId: result.Success ? webmFileId : null,
                        errorMessage: result.Success ? null : result.StdErr,
                        conversionStartedAt: conversionStartedAt,
                        conversionCompletedAt: conversionCompletedAt,
                        conversionCommand: args);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    try
                    {
                        await NotifyStatusAsync(fileId, "FAILED", errorMessage: ex.Message,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: DateTime.UtcNow,
                            conversionCommand: args);
                    }
                    catch (Exception notifyEx)
                    {
                        Console.WriteLine($"Failed to notify FAILED status in catch block: {notifyEx.Message}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StartVideoThumbnailExtractionAsync(Guid fileId)
    {
        var metadata = await _dbContext.FileMetadatas.FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted);
        if (metadata == null)
        {
            throw new FileNotFoundException("변환할 비디오 파일의 메타데이터를 찾을 수 없습니다.");
        }

        var physicalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";
        var videoFolder = GetPath(bizType, "Video");

        try
        {
            var thumbFileId = Guid.NewGuid();
            var thumbStoredName = $"{thumbFileId}.jpg";
            var thumbnailImagePath = Path.Combine(videoFolder, thumbStoredName);

            //--------------------------------------------------
            // Thumbnail Extraction
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                try
                {
                    var args = $"-ss 00:00:00 -i \"{physicalPath}\" -vframes 1 -q:v 2 \"{thumbnailImagePath}\" -y";
                    Console.WriteLine("[Thumbnail Extraction] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromSeconds(30));
                    Console.WriteLine($"[Thumbnail Extraction] Success={result.Success}");

                    if (!result.Success)
                    {
                        Console.WriteLine(result.StdErr);
                        return;
                    }

                    var fileInfo = new FileInfo(thumbnailImagePath);
                    var thumbMetadata = new FileMetadata
                    {
                        Id = thumbFileId,
                        OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + "_thumb.jpg",
                        StoredName = thumbStoredName,
                        Path = $"{GetBizFolder(bizType)}/Video/{thumbStoredName}",
                        Size = fileInfo.Exists ? fileInfo.Length : 0,
                        ContentType = "image/jpeg",
                        CreatedBy = "System",
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                        dbContext.FileMetadatas.Add(thumbMetadata);
                        await dbContext.SaveChangesAsync();
                    }

                    await NotifyStatusAsync(fileId, null!, hasThumbnail: true, 
                        thumbnailUrl: $"/api/file/download/{thumbFileId}", 
                        thumbnailFileId: thumbFileId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StartVideoWebmTranscodingAsync(Guid fileId)
    {
        var metadata = await _dbContext.FileMetadatas.FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted);
        if (metadata == null)
        {
            throw new FileNotFoundException("변환할 비디오 파일의 메타데이터를 찾을 수 없습니다.");
        }

        var physicalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";
        var videoFolder = GetPath(bizType, "Video");

        try
        {
            var webmFileId = Guid.NewGuid();
            var webmStoredName = $"{webmFileId}.webm";
            var webmPath = Path.Combine(videoFolder, webmStoredName);

            //--------------------------------------------------
            // WEBM Transcoding
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                var conversionStartedAt = DateTime.UtcNow;
                var args = $"-i \"{physicalPath}\" " +
                           "-vf \"scale='min(1920,iw)':'min(1080,ih)':force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2\" " +
                           "-c:v libvpx-vp9 " +
                           "-crf 30 " +
                           "-b:v 0 " +
                           "-row-mt 1 " +
                           "-cpu-used 4 " +
                           "-threads 2 " +
                           "-c:a libopus " +
                           $"\"{webmPath}\" -y";
                try
                {
                    Console.WriteLine("[WebM Transcoding] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromMinutes(30));
                    Console.WriteLine($"[WebM Transcoding] Success={result.Success}");
                    var conversionCompletedAt = DateTime.UtcNow;

                    if (!result.Success)
                    {
                        Console.WriteLine(result.StdErr);
                    }

                    if (result.Success)
                    {
                        var fileInfo = new FileInfo(webmPath);
                        var webmMetadata = new FileMetadata
                        {
                            Id = webmFileId,
                            OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + ".webm",
                            StoredName = webmStoredName,
                            Path = $"{GetBizFolder(bizType)}/Video/{webmStoredName}",
                            Size = fileInfo.Exists ? fileInfo.Length : 0,
                            ContentType = "video/webm",
                            CreatedBy = "System",
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                            dbContext.FileMetadatas.Add(webmMetadata);
                            await dbContext.SaveChangesAsync();
                        }
                    }

                    await NotifyStatusAsync(fileId, result.Success ? "COMPLETED" : "FAILED", 
                        hasWebm: result.Success, 
                        webmUrl: result.Success ? $"/api/file/download/{webmFileId}" : null, 
                        webmFileId: result.Success ? webmFileId : null,
                        errorMessage: result.Success ? null : result.StdErr,
                        conversionStartedAt: conversionStartedAt,
                        conversionCompletedAt: conversionCompletedAt,
                        conversionCommand: args);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    try
                    {
                        await NotifyStatusAsync(fileId, "FAILED", errorMessage: ex.Message,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: DateTime.UtcNow,
                            conversionCommand: args);
                    }
                    catch (Exception notifyEx)
                    {
                        Console.WriteLine($"Failed to notify FAILED status in catch block: {notifyEx.Message}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }


    /// <inheritdoc />
    public async Task StartAudioTranscodingAsync(Guid fileId)
    {
        var metadata = await _dbContext.FileMetadatas.FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted);
        if (metadata == null)
        {
            throw new FileNotFoundException("변환할 오디오 파일의 메타데이터를 찾을 수 없습니다.");
        }

        var extension = Path.GetExtension(metadata.OriginalName);
        var isAudio = metadata.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) || 
                      extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".mpeg", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase);

        if (!isAudio)
        {
            throw new InvalidOperationException("오디오 파일만 트랜스코딩 처리가 가능합니다.");
        }

        var physicalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";
        var audioFolder = GetPath(bizType, "Audio");

        try
        {
            var oggFileId = Guid.NewGuid();
            var oggStoredName = $"{oggFileId}.ogg";
            var oggPath = Path.Combine(audioFolder, oggStoredName);

            var aacFileId = Guid.NewGuid();
            var aacStoredName = $"{aacFileId}.aac";
            var aacPath = Path.Combine(audioFolder, aacStoredName);

            var thumbFileId = Guid.NewGuid();
            var thumbStoredName = $"{thumbFileId}.jpg";
            var thumbnailImagePath = Path.Combine(audioFolder, thumbStoredName);

            //--------------------------------------------------
            // 앨범 아트 추출 시도
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                try
                {
                    // 오디오 파일에서 앨범 아트를 단독 추출해 jpeg 파일로 변환 저장 시도
                    var args = $"-i \"{physicalPath}\" -an -vcodec copy \"{thumbnailImagePath}\" -y";
                    Console.WriteLine("[Audio AlbumArt Extract] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromSeconds(20));
                    Console.WriteLine($"[Audio AlbumArt Extract] Success={result.Success}");

                    if (result.Success && File.Exists(thumbnailImagePath))
                    {
                        var fileInfo = new FileInfo(thumbnailImagePath);
                        
                        // 크기가 0바이트인 빈 파일인 경우는 제외
                        if (fileInfo.Exists && fileInfo.Length > 0)
                        {
                            var thumbMetadata = new FileMetadata
                            {
                                Id = thumbFileId,
                                OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + "_thumb.jpg",
                                StoredName = thumbStoredName,
                                Path = $"{GetBizFolder(bizType)}/Audio/{thumbStoredName}",
                                Size = fileInfo.Length,
                                ContentType = "image/jpeg",
                                CreatedBy = "System",
                                CreatedAt = DateTime.UtcNow,
                                IsDeleted = false
                            };

                            using (var scope = _scopeFactory.CreateScope())
                            {
                                var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                                dbContext.FileMetadatas.Add(thumbMetadata);
                                await dbContext.SaveChangesAsync();
                            }

                            await NotifyStatusAsync(fileId, "PROCESSING", 
                                hasThumbnail: true, 
                                thumbnailUrl: $"/api/file/download/{thumbFileId}", 
                                thumbnailFileId: thumbFileId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"앨범 아트 추출 건너뜀 (이미지 미포함 음원 가능성): {ex.Message}");
                }
            });

            //--------------------------------------------------
            // OGG 변환 (Libopus 코덱 사용, 96k, 앨범아트 비디오 스트림을 배제하기 위해 -vn 추가)
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                var conversionStartedAt = DateTime.UtcNow;
                var args = $"-i \"{physicalPath}\" -c:a libopus -b:a 96k -vn \"{oggPath}\" -y";
                try
                {
                    Console.WriteLine("[OGG Audio] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromMinutes(3));
                    Console.WriteLine($"[OGG Audio] Success={result.Success}");
                    var conversionCompletedAt = DateTime.UtcNow;

                    if (result.Success)
                    {
                        var fileInfo = new FileInfo(oggPath);
                        var oggMetadata = new FileMetadata
                        {
                            Id = oggFileId,
                            OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + ".ogg",
                            StoredName = oggStoredName,
                            Path = $"{GetBizFolder(bizType)}/Audio/{oggStoredName}",
                            Size = fileInfo.Exists ? fileInfo.Length : 0,
                            ContentType = "audio/ogg",
                            CreatedBy = "System",
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                            dbContext.FileMetadatas.Add(oggMetadata);
                            await dbContext.SaveChangesAsync();
                        }

                        await NotifyStatusAsync(fileId, "PROCESSING", false, false, hasOgg: true, 
                            oggUrl: $"/api/file/download/{oggFileId}", 
                            oggFileId: oggFileId,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: conversionCompletedAt,
                            conversionCommand: args);
                    }
                    else
                    {
                        Console.WriteLine(result.StdErr);
                        await NotifyStatusAsync(fileId, "FAILED", errorMessage: result.StdErr,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: conversionCompletedAt,
                            conversionCommand: args);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    try
                    {
                        await NotifyStatusAsync(fileId, "FAILED", errorMessage: ex.Message,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: DateTime.UtcNow,
                            conversionCommand: args);
                    }
                    catch (Exception notifyEx)
                    {
                        Console.WriteLine($"Failed to notify FAILED status in catch block: {notifyEx.Message}");
                    }
                }
            });

            //--------------------------------------------------
            // AAC 변환 (128k, 앨범아트 비디오 스트림을 배제하기 위해 -vn 추가)
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                var conversionStartedAt = DateTime.UtcNow;
                var args = $"-i \"{physicalPath}\" -c:a aac -b:a 128k -vn \"{aacPath}\" -y";
                try
                {
                    Console.WriteLine("[AAC Audio] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromMinutes(3));
                    Console.WriteLine($"[AAC Audio] Success={result.Success}");
                    var conversionCompletedAt = DateTime.UtcNow;

                    if (result.Success)
                    {
                        var fileInfo = new FileInfo(aacPath);
                        var aacMetadata = new FileMetadata
                        {
                            Id = aacFileId,
                            OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + ".aac",
                            StoredName = aacStoredName,
                            Path = $"{GetBizFolder(bizType)}/Audio/{aacStoredName}",
                            Size = fileInfo.Exists ? fileInfo.Length : 0,
                            ContentType = "audio/aac",
                            CreatedBy = "System",
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                            dbContext.FileMetadatas.Add(aacMetadata);
                            await dbContext.SaveChangesAsync();
                        }

                        await NotifyStatusAsync(fileId, "COMPLETED", false, false, hasAac: true, 
                            aacUrl: $"/api/file/download/{aacFileId}", 
                            aacFileId: aacFileId,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: conversionCompletedAt,
                            conversionCommand: args);
                    }
                    else
                    {
                        Console.WriteLine(result.StdErr);
                        await NotifyStatusAsync(fileId, "FAILED", false, false, errorMessage: result.StdErr,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: conversionCompletedAt,
                            conversionCommand: args);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    try
                    {
                        await NotifyStatusAsync(fileId, "FAILED", errorMessage: ex.Message,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: DateTime.UtcNow,
                            conversionCommand: args);
                    }
                    catch (Exception notifyEx)
                    {
                        Console.WriteLine($"Failed to notify FAILED status in catch block: {notifyEx.Message}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }


    /// <inheritdoc />
    public async Task StartAudioEncodingOnlyAsync(Guid fileId)
    {
        var metadata = await _dbContext.FileMetadatas.FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted);
        if (metadata == null)
        {
            throw new FileNotFoundException("변환할 오디오 파일의 메타데이터를 찾을 수 없습니다.");
        }

        var extension = Path.GetExtension(metadata.OriginalName);
        var isAudio = metadata.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) || 
                      extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".mpeg", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase);

        if (!isAudio)
        {
            throw new InvalidOperationException("오디오 파일만 트랜스코딩 처리가 가능합니다.");
        }

        var physicalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";
        var audioFolder = GetPath(bizType, "Audio");

        try
        {
            var oggFileId = Guid.NewGuid();
            var oggStoredName = $"{oggFileId}.ogg";
            var oggPath = Path.Combine(audioFolder, oggStoredName);

            var aacFileId = Guid.NewGuid();
            var aacStoredName = $"{aacFileId}.aac";
            var aacPath = Path.Combine(audioFolder, aacStoredName);

            //--------------------------------------------------
            // OGG 변환 (Libopus 코덱 사용, 96k, 앨범아트 비디오 스트림을 배제하기 위해 -vn 추가)
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                var conversionStartedAt = DateTime.UtcNow;
                var args = $"-i \"{physicalPath}\" -c:a libopus -b:a 96k -vn \"{oggPath}\" -y";
                try
                {
                    Console.WriteLine("[OGG Audio Only] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromMinutes(3));
                    Console.WriteLine($"[OGG Audio Only] Success={result.Success}");
                    var conversionCompletedAt = DateTime.UtcNow;

                    if (result.Success)
                    {
                        var fileInfo = new FileInfo(oggPath);
                        var oggMetadata = new FileMetadata
                        {
                            Id = oggFileId,
                            OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + ".ogg",
                            StoredName = oggStoredName,
                            Path = $"{GetBizFolder(bizType)}/Audio/{oggStoredName}",
                            Size = fileInfo.Exists ? fileInfo.Length : 0,
                            ContentType = "audio/ogg",
                            CreatedBy = "System",
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                            dbContext.FileMetadatas.Add(oggMetadata);
                            await dbContext.SaveChangesAsync();
                        }

                        await NotifyStatusAsync(fileId, "PROCESSING", false, false, hasOgg: true, 
                            oggUrl: $"/api/file/download/{oggFileId}", 
                            oggFileId: oggFileId,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: conversionCompletedAt,
                            conversionCommand: args);
                    }
                    else
                    {
                        Console.WriteLine(result.StdErr);
                        await NotifyStatusAsync(fileId, "FAILED", errorMessage: result.StdErr,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: conversionCompletedAt,
                            conversionCommand: args);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    try
                    {
                        await NotifyStatusAsync(fileId, "FAILED", errorMessage: ex.Message,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: DateTime.UtcNow,
                            conversionCommand: args);
                    }
                    catch (Exception notifyEx)
                    {
                        Console.WriteLine($"Failed to notify FAILED status in catch block: {notifyEx.Message}");
                    }
                }
            });

            //--------------------------------------------------
            // AAC 변환 (128k, 앨범아트 비디오 스트림을 배제하기 위해 -vn 추가)
            //--------------------------------------------------
            _ = Task.Run(async () =>
            {
                var conversionStartedAt = DateTime.UtcNow;
                var args = $"-i \"{physicalPath}\" -c:a aac -b:a 128k -vn \"{aacPath}\" -y";
                try
                {
                    Console.WriteLine("[AAC Audio Only] Start");
                    var result = await RunFfmpegAsync(args, TimeSpan.FromMinutes(3));
                    Console.WriteLine($"[AAC Audio Only] Success={result.Success}");
                    var conversionCompletedAt = DateTime.UtcNow;

                    if (result.Success)
                    {
                        var fileInfo = new FileInfo(aacPath);
                        var aacMetadata = new FileMetadata
                        {
                            Id = aacFileId,
                            OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + ".aac",
                            StoredName = aacStoredName,
                            Path = $"{GetBizFolder(bizType)}/Audio/{aacStoredName}",
                            Size = fileInfo.Exists ? fileInfo.Length : 0,
                            ContentType = "audio/aac",
                            CreatedBy = "System",
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
                            dbContext.FileMetadatas.Add(aacMetadata);
                            await dbContext.SaveChangesAsync();
                        }

                        await NotifyStatusAsync(fileId, "COMPLETED", false, false, hasAac: true, 
                            aacUrl: $"/api/file/download/{aacFileId}", 
                            aacFileId: aacFileId,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: conversionCompletedAt,
                            conversionCommand: args);
                    }
                    else
                    {
                        Console.WriteLine(result.StdErr);
                        await NotifyStatusAsync(fileId, "FAILED", false, false, errorMessage: result.StdErr,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: conversionCompletedAt,
                            conversionCommand: args);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    try
                    {
                        await NotifyStatusAsync(fileId, "FAILED", errorMessage: ex.Message,
                            conversionStartedAt: conversionStartedAt,
                            conversionCompletedAt: DateTime.UtcNow,
                            conversionCommand: args);
                    }
                    catch (Exception notifyEx)
                    {
                        Console.WriteLine($"Failed to notify FAILED status in catch block: {notifyEx.Message}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }


private async Task<(bool Success, string StdOut, string StdErr)> RunFfmpegAsync(
    string arguments,
    TimeSpan timeout)
{
    var psi = new ProcessStartInfo
    {
        FileName = "ffmpeg",
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi);

    if (process == null)
    {
        return (false, "", "Failed to start ffmpeg process.");
    }

    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();

    var waitTask = process.WaitForExitAsync();

    var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout));

    if (completedTask != waitTask)
    {
        try
        {
            process.Kill(true);
        }
        catch
        {
        }

        await process.WaitForExitAsync();

        return (false,
            await stdoutTask,
            "FFmpeg timed out.");
    }

    await Task.WhenAll(stdoutTask, stderrTask);

    return
    (
        process.ExitCode == 0,
        await stdoutTask,
        await stderrTask
    );
}


private async Task NotifyStatusAsync(
    Guid fileId,
    string? status,
    bool? hasThumbnail = null,
    bool? hasWebm = null,
    string? thumbnailUrl = null,
    string? webmUrl = null,
    bool? hasOgg = null,
    bool? hasAac = null,
    string? oggUrl = null,
    string? aacUrl = null,
    Guid? thumbnailFileId = null,
    Guid? webmFileId = null,
    Guid? oggFileId = null,
    Guid? aacFileId = null,
    string? errorMessage = null,
    DateTime? conversionStartedAt = null,
    DateTime? conversionCompletedAt = null,
    string? conversionCommand = null)
{
    using var client = new HttpClient();

    var jsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    var content = new StringContent(
        System.Text.Json.JsonSerializer.Serialize(new
        {
            status,
            errorMessage,
            conversionStartedAt,
            conversionCompletedAt,
            conversionCommand,
            hasThumbnail,
            hasWebm,
            hasOgg,
            hasAac,
            thumbnailUrl,
            webmUrl,
            oggUrl,
            aacUrl,
            thumbnailFileId,
            webmFileId,
            oggFileId,
            aacFileId
        }, jsonOptions),
        Encoding.UTF8,
        "application/json");

    using var response = await client.PatchAsync(
        $"http://localhost:5320/building/source/{fileId}/status",
        content);

    Console.WriteLine($"Notify Status : {response.StatusCode}");
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

        var physicalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));

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
        return await GetPresetImageAsync(id, "Thumbnail", 150, 150);
    }

    /// <inheritdoc />
    public async Task<(Stream FileStream, string ContentType)> GetMediumImageAsync(Guid id)
    {
        // 미디움 규격: 600x600
        return await GetPresetImageAsync(id, "Medium", 600, 600);
    }

    /// <inheritdoc />
    public async Task<(Stream FileStream, string ContentType)> GetLargeImageAsync(Guid id)
    {
        // 라지 규격: 1200x1200
        return await GetPresetImageAsync(id, "Large", 1200, 1200);
    }

    /// <summary>
    /// 지정된 크기 규격(Preset)의 이미지를 조회 혹은 Lazy Generation 방식으로 WebP 포맷 생성하여 반환
    /// </summary>
    private async Task<(Stream FileStream, string ContentType)> GetPresetImageAsync(Guid id, string folderName, int width, int height)
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

        var pathSegments = metadata.Path.Split('/');
        string bizType = "basecom";
        if (pathSegments.Length >= 3)
        {
            bizType = string.Join("/", pathSegments.Take(pathSegments.Length - 2));
        }
        else if (pathSegments.Length > 0)
        {
            bizType = pathSegments[0];
        }
        var targetFolderPath = GetPath(bizType, folderName);

        var targetFileName = $"{id}.webp";
        var targetFilePath = Path.Combine(targetFolderPath, targetFileName);

        // 1. 이미 존재 시 (연산 없음) 즉시 스트림 반환
        if (File.Exists(targetFilePath))
        {
            var existingStream = new FileStream(targetFilePath, FileMode.Open, FileAccess.Read);
            return (existingStream, "image/webp");
        }

        // 2. 미존재 시 Lazy Generation (원본 기준 WebP 생성)
        var originalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
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

        var originalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(originalPath))
        {
            throw new FileNotFoundException("서버 물리 스토리지에 원본 파일이 존재하지 않습니다.");
        }

        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";
        var cacheFolderPath = GetPath(bizType, "Cache");

        // 임의 크기도 WebP 캐시로 관리
        var cacheFileName = $"{id}_{width}_{height}.webp";
        var cachePath = Path.Combine(cacheFolderPath, cacheFileName);

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

        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";

        // 1. 원본 파일 삭제 시도
        var originalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
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
        
        var thumbnailFilePath = Path.Combine(GetPath(bizType, "Thumbnail"), webpFileName);
        if (File.Exists(thumbnailFilePath)) try { File.Delete(thumbnailFilePath); } catch {}

        var mediumFilePath = Path.Combine(GetPath(bizType, "Medium"), webpFileName);
        if (File.Exists(mediumFilePath)) try { File.Delete(mediumFilePath); } catch {}

        var largeFilePath = Path.Combine(GetPath(bizType, "Large"), webpFileName);
        if (File.Exists(largeFilePath)) try { File.Delete(largeFilePath); } catch {}

        // 3. 임의 캐시 삭제 시도
        var cacheFolderPath = GetPath(bizType, "Cache");
        if (Directory.Exists(cacheFolderPath))
        {
            var cachedFiles = Directory.GetFiles(cacheFolderPath, $"{id}_*");
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

        var bizFolder = GetBizFolder(bizType);
        var originalFolderPath = GetPath(bizType, "Original");

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var fileId = Guid.NewGuid();
            var extension = Path.GetExtension(file.FileName);
            var storedName = $"{fileId}{extension}";
            var physicalPath = Path.Combine(originalFolderPath, storedName);

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
                Path = $"{bizFolder}/Original/{storedName}",
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

    /// <inheritdoc />
    public async Task<FileMetadata?> ExtractVideoThumbnailAsync(Guid fileId)
    {
        var metadata = await _dbContext.FileMetadatas.FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted);
        if (metadata == null) return null;

        var physicalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";
        var videoFolder = GetPath(bizType, "Video");

        var thumbFileId = Guid.NewGuid();
        var thumbStoredName = $"{thumbFileId}.jpg";
        var thumbnailImagePath = Path.Combine(videoFolder, thumbStoredName);

        var args = $"-ss 00:00:00 -i \"{physicalPath}\" -vframes 1 -q:v 2 \"{thumbnailImagePath}\" -y";
        Console.WriteLine("[Direct Thumbnail Extraction] Start");
        var result = await RunFfmpegAsync(args, TimeSpan.FromSeconds(30));
        Console.WriteLine($"[Direct Thumbnail Extraction] Success={result.Success}");

        if (!result.Success)
        {
            Console.WriteLine(result.StdErr);
            return null;
        }

        var fileInfo = new FileInfo(thumbnailImagePath);
        var thumbMetadata = new FileMetadata
        {
            Id = thumbFileId,
            OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + "_thumb.jpg",
            StoredName = thumbStoredName,
            Path = $"{GetBizFolder(bizType)}/Video/{thumbStoredName}",
            Size = fileInfo.Exists ? fileInfo.Length : 0,
            ContentType = "image/jpeg",
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _dbContext.FileMetadatas.Add(thumbMetadata);
        await _dbContext.SaveChangesAsync();

        return thumbMetadata;
    }

    /// <inheritdoc />
    public async Task<FileMetadata?> ExtractAudioAlbumArtAsync(Guid fileId)
    {
        var metadata = await _dbContext.FileMetadatas.FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted);
        if (metadata == null) return null;

        var physicalPath = Path.Combine(_localPathRoot, metadata.Path.Replace('/', Path.DirectorySeparatorChar));
        var pathSegments = metadata.Path.Split('/');
        var bizType = pathSegments.Length > 0 ? pathSegments[0] : "basecom";
        var audioFolder = GetPath(bizType, "Audio");

        var thumbFileId = Guid.NewGuid();
        var thumbStoredName = $"{thumbFileId}.jpg";
        var thumbnailImagePath = Path.Combine(audioFolder, thumbStoredName);

        var args = $"-i \"{physicalPath}\" -an -vcodec copy \"{thumbnailImagePath}\" -y";
        Console.WriteLine("[Direct Audio AlbumArt Extract] Start");
        var result = await RunFfmpegAsync(args, TimeSpan.FromSeconds(20));
        Console.WriteLine($"[Direct Audio AlbumArt Extract] Success={result.Success}");

        if (result.Success && File.Exists(thumbnailImagePath))
        {
            var fileInfo = new FileInfo(thumbnailImagePath);
            if (fileInfo.Exists && fileInfo.Length > 0)
            {
                var thumbMetadata = new FileMetadata
                {
                    Id = thumbFileId,
                    OriginalName = Path.GetFileNameWithoutExtension(metadata.OriginalName) + "_thumb.jpg",
                    StoredName = thumbStoredName,
                    Path = $"{GetBizFolder(bizType)}/Audio/{thumbStoredName}",
                    Size = fileInfo.Length,
                    ContentType = "image/jpeg",
                    CreatedBy = "System",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _dbContext.FileMetadatas.Add(thumbMetadata);
                await _dbContext.SaveChangesAsync();
                return thumbMetadata;
            }
        }

        return null;
    }
}
