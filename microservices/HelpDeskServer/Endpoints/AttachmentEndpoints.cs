using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 첨부파일 관련 엔드포인트
/// </summary>
public static class AttachmentEndpoints
{
    /// <summary>
    /// 첨부파일 관련 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/attachments");

        group.MapGet("/", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
            () => db.Attachments.ToListAsync()
        ));

        group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
            () => db.Attachments.FirstOrDefaultAsync(a => a.Id == id)
        ));

        group.MapPost("/", (AppDbContext db, Attachment attachment) => ApiResponseBuilder.CreateAsync(async () =>
        {
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync();
            return attachment;
        }, "Attachment created successfully.", 201));

        group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var attachment = await db.Attachments.FindAsync(id);
            if (attachment is null) return null;

            db.Attachments.Remove(attachment);
            await db.SaveChangesAsync();
            return new { DeletedId = id };
        }, "Attachment deleted successfully."));

        group.MapGet("/download/{id}", async (AppDbContext db, int id) =>
        {
            var attachment = await db.Attachments.FindAsync(id);
            if (attachment == null)
            {
                return Results.NotFound("Attachment not found.");
            }

            var filePath = Path.Combine(attachment.FilePath, attachment.StoredFileName);


            Console.WriteLine($"filePath : {filePath}");

            if (!File.Exists(filePath))
            {
                return Results.NotFound("File not found.");
            }

            var memory = new MemoryStream();
            await using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return Results.File(memory, attachment.FileType, attachment.OriginalFileName);
        });
    }
}
