using AIAgentServer.Data;
using AIAgentServer.Services.Rag;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentServer.Endpoints;

/// <summary>
/// RAG 문서함 — 카테고리 · 분류 · 파일.
/// </summary>
/// <remarks>
/// 화면: <c>web/src/Apps/JSini.Web.Admin/Components/Pages/RagDocuments.razor</c>
/// <para>
/// <b>파일은 운영시스템에 남는다.</b> 올리는 것도 내려받는 것도 여기를 지나고,
/// LLM 서버는 임베딩만 맡는다.
/// </para>
/// </remarks>
public static class RagLibraryEndpoints
{
    public static void MapRagLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rag").WithTags("RAG Library");

        // ── 카테고리 ────────────────────────────────────────
        group.MapGet("/categories", async (
            [FromServices] RagLibrary lib, [FromServices] RagStore store, CancellationToken ct) =>
        {
            var rows = await lib.CategoriesAsync(ct);
            return Results.Ok(ApiResponse<object>.Ok(rows.Select(c => new
            {
                c.Id, c.Name, c.Description, c.IndexedAt, c.DocCount,
                // 표의 숫자가 아니라 **지금 메모리에 올라와 있는 것**을 보여 준다.
                // 둘이 다르면 색인을 만든 뒤 서비스가 다시 뜨지 않은 것이다.
                chunkCount = store.CountOf(c.Id),
            })));
        }).WithSummary("RAG 카테고리 목록");

        group.MapPost("/categories", async (
            [FromBody] CreateCategoryRequest body,
            [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                var row = await lib.CreateCategoryAsync(body.Id, body.Name, body.Description, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { row.Id, row.Name }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("RAG 카테고리 만들기");

        group.MapDelete("/categories/{id}", async (
            string id, [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.DeleteCategoryAsync(id, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { deleted = id }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("RAG 카테고리 지우기 (문서까지 함께)");

        // ── 분류 (트리) ─────────────────────────────────────
        group.MapGet("/categories/{id}/folders", async (
            string id, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            var rows = await lib.FoldersAsync(id, ct);
            return Results.Ok(ApiResponse<object>.Ok(
                rows.Select(f => new { f.Id, f.ParentId, f.Name, f.SortNo })));
        }).WithSummary("분류 트리");

        group.MapPost("/categories/{id}/folders", async (
            string id, [FromBody] CreateFolderRequest body,
            [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                var row = await lib.CreateFolderAsync(id, body.ParentId, body.Name, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { row.Id, row.ParentId, row.Name }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("분류 만들기");

        group.MapPut("/folders/{folderId:long}", async (
            long folderId, [FromBody] RenameFolderRequest body,
            [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.RenameFolderAsync(folderId, body.Name, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { folderId, body.Name }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("분류 이름 바꾸기");

        // 끌어다 옮기기. **색인은 건드리지 않는다** — 분류는 사람이 보려고
        // 있는 것이고 색인은 카테고리 단위다.
        group.MapPut("/folders/{folderId:long}/move", async (
            long folderId, [FromBody] MoveFolderRequest body,
            [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.MoveFolderAsync(folderId, body.ParentId, body.BeforeId, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { folderId, body.ParentId, body.BeforeId }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("분류 옮기기 (부모·차례)");

        group.MapDelete("/folders/{folderId:long}", async (
            long folderId, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            try
            {
                await lib.DeleteFolderAsync(folderId, ct);
                return Results.Ok(ApiResponse<object>.Ok(new { deleted = folderId }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("분류 지우기 (아래 분류와 문서까지)");

        // ── 문서 ────────────────────────────────────────────
        group.MapGet("/categories/{id}/documents", async (
            string id, [FromQuery] long? folderId,
            [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            var rows = await lib.DocumentsAsync(id, folderId, ct);
            return Results.Ok(ApiResponse<object>.Ok(rows.Select(d => new
            {
                d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Status,
                d.ChunkCount, d.Error, d.IndexedAt, d.CreatedAt, d.CreatedBy,
            })));
        }).WithSummary("분류 안의 문서 목록");

        // 올리기. **여러 개를 한 번에 받는다** — 한 건씩 고르게 하면
        // 폴더째 올리려는 사람이 수십 번 같은 창을 연다.
        group.MapPost("/categories/{id}/documents", async (
            string id, [FromQuery] long? folderId,
            HttpRequest request, [FromServices] RagLibrary lib,
            HttpContext http, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("파일이 없습니다.", "C400"));
            }

            var form = await request.ReadFormAsync(ct);
            if (form.Files.Count == 0)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("파일이 없습니다.", "C400"));
            }

            var saved = new List<object>();
            foreach (var f in form.Files)
            {
                await using var s = f.OpenReadStream();
                var row = await lib.UploadAsync(id, folderId, f.FileName, f.ContentType, s, Who(http), ct);
                saved.Add(new { row.Id, row.FileName, row.SizeBytes, row.Status, row.Error });
            }

            return Results.Ok(ApiResponse<object>.Ok(saved));
        })
        .DisableAntiforgery()
        .WithSummary("문서 올리기 (여러 개)");

        group.MapGet("/documents/{docId:long}/download", async (
            long docId, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            var f = await lib.OpenAsync(docId, ct);
            return f is null
                ? Results.NotFound()
                : Results.File(f.Value.Path, "application/octet-stream", f.Value.Name);
        }).WithSummary("문서 내려받기");

        group.MapDelete("/documents/{docId:long}", async (
            long docId, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            try
            {
                await lib.DeleteDocumentAsync(docId, ct);
                return Results.Ok(ApiResponse<object>.Ok(new { deleted = docId }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("문서 지우기");

        // ── 색인 ────────────────────────────────────────────
        group.MapPost("/categories/{id}/reindex", async (
            string id, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            try
            {
                var index = await lib.ReindexAsync(id, ct);
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    category = id,
                    chunks = index.Chunks.Count,
                    dimensions = index.Dimensions,
                    builtAt = index.BuiltAt,
                }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail($"색인을 만들지 못했습니다 — {ex.Message}", "C500"));
            }
        }).WithSummary("카테고리 색인 다시 만들기");
    }

    /// <summary>누가 했는지. 게이트웨이가 실어 보내는 로그인 아이디다.</summary>
    private static string? Who(HttpContext http)
        => http.Request.Headers["X-User-Id"].FirstOrDefault()
           ?? http.User.Identity?.Name;
}

public sealed record CreateCategoryRequest(string Id, string Name, string? Description);
public sealed record CreateFolderRequest(long? ParentId, string Name);
public sealed record RenameFolderRequest(string Name);
public sealed record MoveFolderRequest(long? ParentId, long? BeforeId);
