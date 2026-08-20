
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 파일 업로드 엔드포인트
/// </summary>
public static class FileUploadEndpoints
{
    /// <summary>
    /// 파일 업로드 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapFileUploadEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/files");

        group.MapPost("/upload", async (
            [FromForm] IFormFileCollection files,
            [FromForm] string entityType,
            [FromForm] int entityId,
            AppDbContext db,
            ILoggerFactory loggerFactory,
            IConfiguration configuration) =>
        {


                var logger = loggerFactory.CreateLogger("MapFileUploadEndpoints");


            var attachments = new List<Attachment>();
            //var storagePath = configuration.GetValue<string>("FileStorage:BasePath") ?? "/home/lee/jinAttachment";
            logger.LogInformation($"xxxxxxxxxxxxxxxxxxx{Environment.GetEnvironmentVariable("FileStorage_BasePath")}");

            var storagePath = Environment.GetEnvironmentVariable("FileStorage_BasePath") ?? "/home/lee/jinAttachment";

            logger.LogInformation($"cccccccccccccccccccc{storagePath}");

            Directory.CreateDirectory(storagePath);

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var extension = Path.GetExtension(file.FileName);
                    var storedFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(storagePath, storedFileName);

                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var attachment = new Attachment
                    {
                        OriginalFileName = file.FileName,
                        StoredFileName = storedFileName,
                        FilePath = storagePath,
                        FileType = file.ContentType,
                        FileSize = file.Length,
                        EntityType = entityType,
                        EntityId = entityId,
                        UploadedAt = DateTime.UtcNow
                    };
                    attachments.Add(attachment);
                }
            }

            if (attachments.Any())
            {
                db.Attachments.AddRange(attachments);
                await db.SaveChangesAsync();
            }

            return Results.Ok(attachments);
        })
        .DisableAntiforgery();
    }
}
