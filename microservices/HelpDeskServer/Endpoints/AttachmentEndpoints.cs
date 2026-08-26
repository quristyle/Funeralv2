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

        // ── 내려받기 ────────────────────────────────────────
        //
        // 옮긴 첨부(fileid 가 있는 것)는 FileServer 로 넘긴다. 파일을 이 서비스가
        // 들고 있지 않으므로 스트림을 읽을 수도 없다.
        //
        // 아직 옮기지 않은 행은 예전처럼 로컬 경로에서 읽는다. 37건을 옮기는 일은
        // 배포 장비에서 도구를 돌려야 하므로(파일 바이트가 그 장비에만 있다),
        // 옮기기 전에 이 경로를 끊으면 기존 첨부를 못 받는다.
        //
        // **옮기기가 끝나면 아래 legacy 분기를 지운다** (결정 D5-B 의 마지막 단계).
        group.MapGet("/download/{id}", async (AppDbContext db, int id) =>
        {
            var attachment = await db.Attachments.FindAsync(id);
            if (attachment == null)
            {
                return Results.NotFound("Attachment not found.");
            }

            // 옮겨진 첨부 → FileServer 가 내려준다.
            if (!string.IsNullOrWhiteSpace(attachment.FileId))
            {
                return Results.Redirect($"/api/file/download/id/{attachment.FileId}");
            }

            // ── 여기부터는 옮기기 전의 경로다 (없어질 코드) ──
            if (string.IsNullOrWhiteSpace(attachment.FilePath) ||
                string.IsNullOrWhiteSpace(attachment.StoredFileName))
            {
                return Results.NotFound("File not found.");
            }

            var filePath = Path.Combine(attachment.FilePath, attachment.StoredFileName);

            if (!File.Exists(filePath))
            {
                return Results.NotFound("File not found.");
            }

            // 파일 전체를 메모리에 담지 않는다. 첨부에 6.6MB mp4 가 있고,
            // 큰 파일이 늘면 동시 요청에서 메모리가 그만큼 곱해진다.
            var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);

            return Results.File(stream, attachment.FileType, attachment.OriginalFileName);
        });
    }
}
