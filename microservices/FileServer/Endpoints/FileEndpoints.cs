using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FileServer.Services;
using Funeralv2.Shared.DTOs;

namespace FileServer.Endpoints;

public static class FileEndpoints
{
    public static void MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/");

        // 1. 파일 업로드
        group.MapPost("/upload", async Task<IResult> (IFormFile file, [FromServices] IFileService fileService, UserContext? userContext) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_FILE_UPLOAD", "업로드할 파일이 존재하지 않거나 빈 파일입니다."));
            }

            try
            {
                var userId = userContext?.UserId;
                var metadata = await fileService.UploadFileAsync(file, userId);
                var isVideo = metadata.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || 
                              metadata.OriginalName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                              metadata.OriginalName.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
                              metadata.OriginalName.EndsWith(".avi", StringComparison.OrdinalIgnoreCase);

                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    id = metadata.Id,
                    originalName = metadata.OriginalName,
                    size = metadata.Size,
                    contentType = metadata.ContentType,
                    isImage = metadata.IsImage,
                    isVideo = isVideo,
                    createdAt = metadata.CreatedAt,
                    downloadUrl = $"/api/file/download/{metadata.Id}",
                    thumbnailLink = isVideo ? $"/api/file/download/{metadata.Id}.jpg" : null
                }));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_FILE_UPLOAD_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("UploadFile")
        .WithOpenApi()
        .DisableAntiforgery();

        // 2. 파일 다운로드
        group.MapGet("/download/{fileName}", async Task<IResult> (string fileName, [FromServices] IFileService fileService, [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        {
            try
            {
                // .jpg 썸네일 요청인 경우 (예: {guid}.jpg)
                if (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(fileName.Substring(0, fileName.Length - 4), out var mediaId))
                {
                    var metadata = await fileService.GetMetadataAsync(mediaId);
                    if (metadata != null)
                    {
                        var localPath = configuration["Storage:LocalPath"] ?? Path.Combine(AppContext.BaseDirectory, "Uploads");
                        var folderName = metadata.Path.StartsWith("Video", StringComparison.OrdinalIgnoreCase) ? "Video" : "Original";
                        var thumbFile = Path.Combine(localPath, folderName, $"{mediaId}.jpg");
                        if (File.Exists(thumbFile))
                        {
                            var thumbStream = new FileStream(thumbFile, FileMode.Open, FileAccess.Read);
                            return Results.File(thumbStream, "image/jpeg");
                        }
                    }
                }

                if (Guid.TryParse(fileName, out var id))
                {
                    var (fileStream, contentType, originalName) = await fileService.DownloadFileAsync(id);
                    return Results.File(fileStream, contentType, fileDownloadName: originalName, enableRangeProcessing: true);
                }

                return Results.BadRequest(ApiResponse<object>.Fail("ERR_INVALID_FILE_ID", "유효하지 않은 파일 식별자입니다."));
            }
            catch (FileNotFoundException ex)
            {
                var fallbackUrl = configuration["Storage:FallbackUrl"];
                if (!string.IsNullOrEmpty(fallbackUrl) && Guid.TryParse(fileName.Replace(".jpg", ""), out var parseId))
                {
                    return Results.Redirect($"{fallbackUrl.TrimEnd('/')}/api/file/download/{fileName}");
                }
                return Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_DOWNLOAD_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("DownloadFile")
        .WithOpenApi();

        // 3. 이미지 썸네일 조회 (기본 150x150)
        group.MapGet("/thumbnail/{id:guid}", async Task<IResult> (Guid id, [FromServices] IFileService fileService, [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        {
            try
            {
                var (fileStream, contentType) = await fileService.GetThumbnailAsync(id);
                return Results.File(fileStream, contentType);
            }
            catch (FileNotFoundException ex)
            {
                var fallbackUrl = configuration["Storage:FallbackUrl"];
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    return Results.Redirect($"{fallbackUrl.TrimEnd('/')}/api/file/thumbnail/{id}");
                }
                return Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_NOT_IMAGE", ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_THUMBNAIL_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetThumbnail")
        .WithOpenApi();

        // 3-1. 이미지 중간 크기 조회 (기본 600x600)
        group.MapGet("/medium/{id:guid}", async Task<IResult> (Guid id, [FromServices] IFileService fileService, [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        {
            try
            {
                var (fileStream, contentType) = await fileService.GetMediumImageAsync(id);
                return Results.File(fileStream, contentType);
            }
            catch (FileNotFoundException ex)
            {
                var fallbackUrl = configuration["Storage:FallbackUrl"];
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    return Results.Redirect($"{fallbackUrl.TrimEnd('/')}/api/file/medium/{id}");
                }
                return Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_NOT_IMAGE", ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_MEDIUM_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetMediumImage")
        .WithOpenApi();

        // 3-2. 이미지 큰 크기 조회 (기본 1200x1200)
        group.MapGet("/large/{id:guid}", async Task<IResult> (Guid id, [FromServices] IFileService fileService, [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        {
            try
            {
                var (fileStream, contentType) = await fileService.GetLargeImageAsync(id);
                return Results.File(fileStream, contentType);
            }
            catch (FileNotFoundException ex)
            {
                var fallbackUrl = configuration["Storage:FallbackUrl"];
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    return Results.Redirect($"{fallbackUrl.TrimEnd('/')}/api/file/large/{id}");
                }
                return Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_NOT_IMAGE", ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_LARGE_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetLargeImage")
        .WithOpenApi();

        // 4. 이미지 크기 조정 후 조회
        group.MapGet("/resize/{id:guid}", async Task<IResult> (Guid id, [FromQuery] int width, [FromQuery] int height, [FromServices] IFileService fileService, [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        {
            if (width <= 0 || height <= 0)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_INVALID_SIZE", "가로(width)와 세로(height) 크기는 0보다 커야 합니다."));
            }

            try
            {
                var (fileStream, contentType) = await fileService.GetResizedImageAsync(id, width, height);
                return Results.File(fileStream, contentType);
            }
            catch (FileNotFoundException ex)
            {
                var fallbackUrl = configuration["Storage:FallbackUrl"];
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    return Results.Redirect($"{fallbackUrl.TrimEnd('/')}/api/file/resize/{id}?width={width}&height={height}");
                }
                return Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_NOT_IMAGE", ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_RESIZE_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetResizedImage")
        .WithOpenApi();

        // 5. 파일 메타데이터 조회
        group.MapGet("/metadata/{id:guid}", async Task<IResult> (Guid id, [FromServices] IFileService fileService) =>
        {
            try
            {
                var metadata = await fileService.GetMetadataAsync(id);
                if (metadata == null)
                {
                    return Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", "파일 정보를 찾을 수 없습니다."));
                }

                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    id = metadata.Id,
                    originalName = metadata.OriginalName,
                    size = metadata.Size,
                    contentType = metadata.ContentType,
                    isImage = metadata.IsImage,
                    createdAt = metadata.CreatedAt,
                    createdBy = metadata.CreatedBy
                }));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_METADATA_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetFileMetadata")
        .WithOpenApi();

        // 6. 파일 삭제
        group.MapDelete("/{id:guid}", async Task<IResult> (Guid id, [FromServices] IFileService fileService) =>
        {
            try
            {
                var success = await fileService.DeleteFileAsync(id);
                if (!success)
                {
                    return Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", "삭제 대상 파일을 찾을 수 없거나 이미 삭제되었습니다."));
                }

                return Results.Ok(ApiResponse<object>.Ok(new { message = "성공적으로 파일이 삭제되었습니다." }));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_DELETE_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("DeleteFile")
        .WithOpenApi();

        // 7. 파일 그룹 내 다중 파일 업로드
        group.MapPost("/group/upload", async Task<IResult> (
            HttpContext context, 
            [FromServices] IFileService fileService, 
            UserContext? userContext) =>
        {
            var request = context.Request;
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_INVALID_CONTENT_TYPE", "Multipart Form-Data 형식이어야 합니다."));
            }

            var form = await request.ReadFormAsync();
            var files = form.Files.GetFiles("files");
            
            if (files == null || files.Count == 0)
            {
                files = form.Files;
            }

            if (files == null || files.Count == 0)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_FILE_UPLOAD", "업로드할 파일이 존재하지 않습니다."));
            }

            Guid? groupId = null;
            if (form.TryGetValue("groupId", out var groupIdStr) && Guid.TryParse(groupIdStr, out var parsedGroupId))
            {
                groupId = parsedGroupId;
            }

            string bizType = "GENERAL";
            if (form.TryGetValue("bizType", out var bizTypeStr))
            {
                bizType = bizTypeStr.ToString();
            }

            try
            {
                var userId = userContext?.UserId;
                var metadataList = await fileService.UploadGroupFilesAsync(files.ToList(), groupId, bizType, userId);
                
                var actualGroupId = metadataList.FirstOrDefault()?.FileGroupId ?? Guid.Empty;

                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    groupId = actualGroupId,
                    files = metadataList.Select(m => new
                    {
                        id = m.Id,
                        originalName = m.OriginalName,
                        size = m.Size,
                        contentType = m.ContentType,
                        isImage = m.IsImage,
                        isRepresentative = m.IsRepresentative,
                        sortOrder = m.SortOrder,
                        createdAt = m.CreatedAt,
                        downloadUrl = $"/api/file/download/{m.Id}"
                    }).ToList()
                }));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_GROUP_UPLOAD_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("UploadGroupFiles")
        .WithOpenApi()
        .DisableAntiforgery();

        // 8. 파일 그룹 내 대표 파일 지정
        group.MapPut("/group/{groupId:guid}/representative/{fileId:guid}", async Task<IResult> (
            Guid groupId, 
            Guid fileId, 
            [FromServices] IFileService fileService) =>
        {
            try
            {
                var success = await fileService.SetRepresentativeFileAsync(groupId, fileId);
                if (!success)
                {
                    return Results.NotFound(ApiResponse<object>.Fail("ERR_SET_REPRESENTATIVE_FAILED", "지정된 그룹이나 파일을 찾을 수 없습니다."));
                }
                return Results.Ok(ApiResponse<object>.Ok(new { message = "성공적으로 대표 파일이 설정되었습니다." }));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_SET_REPRESENTATIVE_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("SetRepresentativeFile")
        .WithOpenApi();

        // 9. 파일 그룹 내 파일 목록 조회
        group.MapGet("/group/{groupId:guid}", async Task<IResult> (
            Guid groupId, 
            [FromServices] IFileService fileService) =>
        {
            try
            {
                var metadataList = await fileService.GetGroupFilesAsync(groupId);
                return Results.Ok(ApiResponse<object>.Ok(metadataList.Select(m => new
                {
                    id = m.Id,
                    originalName = m.OriginalName,
                    size = m.Size,
                    contentType = m.ContentType,
                    isImage = m.IsImage,
                    isRepresentative = m.IsRepresentative,
                    sortOrder = m.SortOrder,
                    createdAt = m.CreatedAt,
                    downloadUrl = $"/api/file/download/{m.Id}"
                }).ToList()));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_GET_GROUP_FILES_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetGroupFiles")
        .WithOpenApi();

        // 10. 비디오 트랜스코딩 트리거 API
        group.MapPost("/transcode/{id:guid}", async Task<IResult> (Guid id, [FromServices] IFileService fileService) =>
        {
            try
            {
                await fileService.StartVideoTranscodingAsync(id);
                return Results.Ok(ApiResponse<object>.Ok(new { message = "비디오 트랜스코딩 작업이 성공적으로 트리거되었습니다." }));
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("ERR_NOT_VIDEO", ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail("ERR_TRANSCODE_TRIGGER_FAILED", ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("TriggerVideoTranscoding")
        .WithOpenApi();
    }
}
