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
            [FromQuery] string? companyId,
            UserContext? user,
            [FromServices] RagLibrary lib, [FromServices] RagStore store, CancellationToken ct) =>
        {
            // 비관리자는 본인 소속 회사 카테고리만 조회할 수 있다.
            var filterCompanyId = (user is not null && !user.IsAdmin)
                ? user.CompanyId
                : companyId;

            var rows = await lib.CategoriesAsync(filterCompanyId, ct);

            // 문서 수는 표의 칸이 아니라 지금 세어 보낸다. 그 칸은 색인을
            // 만들 때 찍히는 사진이라, 올린 직후에는 0 이다.
            var tally = await lib.CategoryTalliesAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(rows.Select(c =>
            {
                var t = tally.GetValueOrDefault(c.Id);
                return new
                {
                    c.Id, c.Name, c.Description, c.IndexedAt,
                    c.CompanyId,
                    companyName = c.Company?.Name,
                    docCount = c.DocCount,
                    docTotal = t.Total,
                    docIndexed = t.Indexed,
                    docPending = t.Pending,
                    docSkipped = t.Skipped,
                    // 표의 숫자가 아니라 지금 메모리에 올라와 있는 것을 보여 준다.
                    chunkCount = store.CountOf(c.Id),
                };
            })));
        }).WithSummary("RAG 카테고리 목록");

        group.MapPost("/categories", async (
            [FromBody] CreateCategoryRequest body,
            UserContext? user,
            [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                // 비관리자는 본인 소속 회사로만 카테고리를 만들 수 있다.
                string? targetCompanyId;
                if (user is not null && !user.IsAdmin)
                {
                    if (string.IsNullOrWhiteSpace(user.CompanyId))
                    {
                        return Results.Ok(ApiResponse<object>.Fail("소속 회사 정보가 없어 카테고리를 만들 수 없습니다.", "C403"));
                    }
                    targetCompanyId = user.CompanyId;
                }
                else
                {
                    targetCompanyId = !string.IsNullOrWhiteSpace(body.CompanyId)
                        ? body.CompanyId
                        : user?.CompanyId;
                }

                var row = await lib.CreateCategoryAsync(body.Id, body.Name, body.Description, targetCompanyId, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { row.Id, row.Name, row.CompanyId }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("RAG 카테고리 만들기");

        group.MapDelete("/categories/{id}", async (
            string id, UserContext? user, [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureCategoryAccessAsync(id, user, ct);
                await lib.DeleteCategoryAsync(id, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { deleted = id }));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("RAG 카테고리 지우기 (문서까지 함께)");

        // ── 분류 (트리) ─────────────────────────────────────
        group.MapGet("/categories/{id}/folders", async (
            string id, UserContext? user, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureCategoryAccessAsync(id, user, ct);
                var rows = await lib.FoldersAsync(id, ct);

                // 분류마다 그 분류에 바로 든 문서 수. 하위 분류의 것은 화면이
                // 더한다 — 어느 분류에 실제로 들었는지는 여기서만 알 수 있다.
                var tally = await lib.FolderTalliesAsync(id, ct);

                return Results.Ok(ApiResponse<object>.Ok(rows.Select(f => new
                {
                    f.Id, f.ParentId, f.Name, f.SortNo,
                    docCount = tally.GetValueOrDefault(f.Id).Total,
                    docPending = tally.GetValueOrDefault(f.Id).Pending,
                })));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
        }).WithSummary("분류 트리");

        group.MapPost("/categories/{id}/folders", async (
            string id, [FromBody] CreateFolderRequest body,
            UserContext? user, [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureCategoryAccessAsync(id, user, ct);
                var row = await lib.CreateFolderAsync(id, body.ParentId, body.Name, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { row.Id, row.ParentId, row.Name }));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("분류 만들기");

        group.MapPut("/folders/{folderId:long}", async (
            long folderId, [FromBody] RenameFolderRequest body,
            UserContext? user, [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureFolderAccessAsync(folderId, user, ct);
                await lib.RenameFolderAsync(folderId, body.Name, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { folderId, body.Name }));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("분류 이름 바꾸기");

        // 끌어다 옮기기. 색인은 건드리지 않는다 — 분류는 사람이 보려고
        // 있는 것이고 색인은 카테고리 단위다.
        group.MapPut("/folders/{folderId:long}/move", async (
            long folderId, [FromBody] MoveFolderRequest body,
            UserContext? user, [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureFolderAccessAsync(folderId, user, ct);
                await lib.MoveFolderAsync(folderId, body.ParentId, body.BeforeId, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { folderId, body.ParentId, body.BeforeId }));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("분류 옮기기 (부모·차례)");

        group.MapDelete("/folders/{folderId:long}", async (
            long folderId, UserContext? user, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureFolderAccessAsync(folderId, user, ct);
                await lib.DeleteFolderAsync(folderId, ct);
                return Results.Ok(ApiResponse<object>.Ok(new { deleted = folderId }));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("분류 지우기 (아래 분류와 문서까지)");

        // ── 문서 ────────────────────────────────────────────
        group.MapGet("/categories/{id}/documents", async (
            string id, [FromQuery] long? folderId,
            UserContext? user, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureCategoryAccessAsync(id, user, ct);
                var rows = await lib.DocumentsAsync(id, folderId, ct);
                return Results.Ok(ApiResponse<object>.Ok(rows.Select(d => new
                {
                    d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Status,
                    d.ChunkCount, d.Error, d.IndexedAt, d.CreatedAt, d.CreatedBy,
                    // 파일 서버의 아이디. 화면이 내려받기 주소를 짓는 열쇠다
                    d.FileId,
                })));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
        }).WithSummary("분류 안의 문서 목록");

        // 올리기. 여러 개를 한 번에 받는다 — 한 건씩 고르게 하면
        // 폴더째 올리려는 사람이 수십 번 같은 창을 연다.
        group.MapPost("/categories/{id}/documents", async (
            string id, [FromQuery] long? folderId,
            HttpRequest request, UserContext? user, [FromServices] RagLibrary lib,
            [FromServices] RagBackgroundIndexer indexer,
            HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureCategoryAccessAsync(id, user, ct);
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }

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
            var anyPending = false;
            foreach (var f in form.Files)
            {
                await using var s = f.OpenReadStream();
                var row = await lib.UploadAsync(id, folderId, f.FileName, f.ContentType, s, Who(http), ct);
                saved.Add(new { row.Id, row.FileName, row.SizeBytes, row.Status, row.Error });
                if (row.Status == "pending") anyPending = true;
            }

            // 파일이 정상적으로 등록되면 백그라운드에서 색인 처리가 자동으로 진행된다.
            if (anyPending)
            {
                indexer.QueueReindex(id);
            }

            return Results.Ok(ApiResponse<object>.Ok(saved));
        })
        .DisableAntiforgery()
        .WithSummary("문서 올리기 (여러 개)");

        group.MapGet("/documents/{docId:long}/download", async (
            long docId, UserContext? user, [FromServices] RagLibrary lib, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureDocumentAccessAsync(docId, user, ct);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }

            // 실물은 파일 서버에 있다. 길이 아니라 흐름으로 받아 그대로
            // 흘려보낸다.
            var f = await lib.OpenAsync(docId, ct);
            return f is null
                ? Results.NotFound()
                : Results.Stream(f.Value.Content, "application/octet-stream", f.Value.Name);
        }).WithSummary("문서 내려받기");

        // 디스크에 있던 옛 문서를 파일 서버로 옮긴다. 여러 번 돌려도 된다.
        group.MapPost("/documents/migrate-to-fileserver", async (
            [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                var (moved, missing, failed) = await lib.MigrateToFileServerAsync(Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { moved, missing, failed }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("RAG 문서를 파일 서버로 옮기기 (한 번만 쓰는 길)");

        group.MapPut("/documents/{docId:long}/folder", async (
            long docId, [FromBody] MoveDocumentRequest body,
            UserContext? user, [FromServices] RagLibrary lib, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureDocumentAccessAsync(docId, user, ct);
                await lib.MoveDocumentAsync(docId, body.FolderId, Who(http), ct);
                return Results.Ok(ApiResponse<object>.Ok(new { moved = docId, body.FolderId }));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("문서를 다른 분류로 옮기기 (색인에는 영향 없음)");

        group.MapDelete("/documents/{docId:long}", async (
            long docId, UserContext? user, [FromServices] RagLibrary lib,
            [FromServices] RagBackgroundIndexer indexer,
            CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureDocumentAccessAsync(docId, user, ct);
                var categoryId = await lib.DeleteDocumentAsync(docId, ct);
                if (!string.IsNullOrEmpty(categoryId))
                {
                    // 문서 삭제 시에도 벡터 스토어와 통계 동기화를 위해 백그라운드로 색인을 자동 갱신한다.
                    indexer.QueueReindex(categoryId);
                }
                return Results.Ok(ApiResponse<object>.Ok(new { deleted = docId }));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }
        }).WithSummary("문서 지우기");

        // ── 색인 ────────────────────────────────────────────
        group.MapPost("/categories/{id}/reindex", async (
            string id, UserContext? user, [FromServices] RagLibrary lib,
            [FromServices] RagBackgroundIndexer indexer,
            CancellationToken ct) =>
        {
            try
            {
                await lib.EnsureCategoryAccessAsync(id, user, ct);
                var index = await indexer.ReindexDirectAsync(id, lib, ct);
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    category = id,
                    chunks = index.Chunks.Count,
                    dimensions = index.Dimensions,
                    builtAt = index.BuiltAt,
                }));
            }
            catch (UnauthorizedAccessException uex)
            {
                return Results.Ok(ApiResponse<object>.Fail(uex.Message, "C403"));
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

public sealed record CreateCategoryRequest(string Id, string Name, string? Description, string? CompanyId = null);
public sealed record CreateFolderRequest(long? ParentId, string Name);
public sealed record RenameFolderRequest(string Name);
public sealed record MoveFolderRequest(long? ParentId, long? BeforeId);

/// <summary>문서를 옮길 분류. <c>null</c> 이면 분류 미지정으로 올린다.</summary>
public sealed record MoveDocumentRequest(long? FolderId);
