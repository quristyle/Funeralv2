using AIAgentServer.Data;
using AIAgentServer.Services.Gmail;

using JSini.Shared.DTOs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Endpoints;

/// <summary>Gmail 보관함을 채우고 들여다보는 길.
///
/// <para><b>파일 이름만 받는다.</b> 적재는 서버 디스크의 mbox 를 읽는데, 주소로 아무 경로나 받으면
/// 그대로 서버 파일 읽기 구멍이 된다. 그래서 설정(<c>Gmail:ImportRoot</c>)에 적힌 한 칸 안의
/// <b>파일 이름</b>만 받고, 설정이 비어 있으면 적재 자체를 막는다.</para></summary>
public static class GmailEndpoints
{
    public static void MapGmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/gmail").WithTags("Gmail");

        // ── 계정 ────────────────────────────────────────────
        group.MapGet("/accounts", async (
            [FromServices] AiUsageDbContext db,
            CancellationToken ct) =>
        {
            var rows = await db.GmailAccounts.AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(rows.Select(a => new
            {
                a.Id,
                a.Email,
                a.OwnerLoginId,
                a.Kind,
                a.Enabled,
                a.LastSyncAt,
                a.LastError,
            })));
        })
        .WithName("GmailAccounts")
        .WithSummary("메일을 받아 오는 계정 목록");

        group.MapPost("/accounts", async (
            [FromBody] GmailAccountUpsert body,
            HttpContext http,
            [FromServices] AiUsageDbContext db,
            CancellationToken ct) =>
        {
            var who = http.Request.Headers["X-User-Id"].ToString();
            if (string.IsNullOrWhiteSpace(who))
                return Results.Ok(ApiResponse<object>.Fail("로그인이 필요합니다.", "C401"));

            if (string.IsNullOrWhiteSpace(body.Id) || string.IsNullOrWhiteSpace(body.Email))
                return Results.Ok(ApiResponse<object>.Fail("계정 열쇠와 메일 주소가 필요합니다.", "C400"));

            if (body.Kind is not ("api" or "imap"))
                return Results.Ok(ApiResponse<object>.Fail("받는 길은 api 또는 imap 입니다.", "C400"));

            var row = await db.GmailAccounts.FirstOrDefaultAsync(x => x.Id == body.Id, ct);
            if (row is null)
            {
                row = new GmailAccountRow { Id = body.Id, CreatedAt = DateTime.UtcNow };
                db.GmailAccounts.Add(row);
            }

            row.Email = body.Email;

            // 주인을 안 적으면 등록한 사람이 주인이다. 업무 비서가 남의 메일을 못 보게 좁히는 열쇠라
            // 비워 둔 채로 두지 않는다.
            row.OwnerLoginId = string.IsNullOrWhiteSpace(body.OwnerLoginId) ? who : body.OwnerLoginId;
            row.Kind = body.Kind;
            row.Enabled = body.Enabled;

            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<object>.Ok(new { row.Id, row.Email, row.OwnerLoginId, row.Kind, row.Enabled }));
        })
        .WithName("GmailAccountUpsert")
        .WithSummary("메일 계정을 등록하거나 고친다");

        // ── 적재할 수 있는 파일 ──────────────────────────────
        group.MapGet("/import/files", (
            [FromServices] IConfiguration config) =>
        {
            var root = ImportRoot(config);
            if (root is null)
                return Results.Ok(ApiResponse<object>.Fail("Gmail:ImportRoot 가 설정되지 않았습니다.", "C400"));

            if (!Directory.Exists(root))
                return Results.Ok(ApiResponse<object>.Fail($"적재 폴더가 없습니다: {root}", "C400"));

            var files = new DirectoryInfo(root)
                .EnumerateFiles("*.mbox", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.Name)
                .Select(f => new { name = f.Name, sizeBytes = f.Length, modifiedAt = f.LastWriteTimeUtc })
                .ToList();

            return Results.Ok(ApiResponse<object>.Ok(files));
        })
        .WithName("GmailImportFiles")
        .WithSummary("적재 폴더 안의 mbox 파일 목록");

        // ── mbox 올리기 ─────────────────────────────────────
        //
        // Takeout 은 GB 단위로 쪼개 내려 주므로 **디스크로 바로 흘려보낸다** —
        // 메모리에 받으면 파일 하나가 서비스를 넘어뜨린다.
        group.MapPost("/import/upload", async (
            HttpContext http,
            [FromServices] IConfiguration config,
            CancellationToken ct) =>
        {
            var who = http.Request.Headers["X-User-Id"].ToString();
            if (string.IsNullOrWhiteSpace(who))
                return Results.Ok(ApiResponse<object>.Fail("로그인이 필요합니다.", "C401"));

            var root = ImportRoot(config);
            if (root is null)
                return Results.Ok(ApiResponse<object>.Fail("Gmail:ImportRoot 가 설정되지 않았습니다.", "C400"));

            Directory.CreateDirectory(root);

            if (!http.Request.HasFormContentType)
                return Results.Ok(ApiResponse<object>.Fail("파일을 보내 주세요.", "C400"));

            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.Ok(ApiResponse<object>.Fail("빈 파일입니다.", "C400"));

            // 이름은 끝자리만 쓴다 — 경로가 섞여 들어오면 폴더를 벗어난다.
            var name = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(name))
                return Results.Ok(ApiResponse<object>.Fail("파일 이름이 없습니다.", "C400"));

            if (!name.EndsWith(".mbox", StringComparison.OrdinalIgnoreCase))
                name += ".mbox";

            var full = Path.Combine(root, name);
            if (!Path.GetFullPath(full).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return Results.Ok(ApiResponse<object>.Fail("적재 폴더 밖입니다.", "C400"));

            // 받다 끊기면 반쪽 파일이 남아 적재가 엉킨다 — 임시 이름으로 받고 끝에 바꾼다.
            var temp = full + ".part";
            try
            {
                await using (var fs = File.Create(temp))
                await using (var src = file.OpenReadStream())
                {
                    await src.CopyToAsync(fs, ct);
                }

                File.Move(temp, full, overwrite: true);
            }
            catch
            {
                if (File.Exists(temp)) File.Delete(temp);
                throw;
            }

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                name,
                sizeBytes = new FileInfo(full).Length,
            }));
        })
        .DisableAntiforgery()
        .WithName("GmailImportUpload")
        .WithSummary("mbox 파일을 적재 폴더에 올린다");

        // ── 적재 시작 ───────────────────────────────────────
        group.MapPost("/import/takeout", async (
            [FromBody] TakeoutImportRequest body,
            HttpContext http,
            [FromServices] AiUsageDbContext db,
            [FromServices] GmailImportStatus status,
            [FromServices] IServiceScopeFactory scopes,
            [FromServices] IHostApplicationLifetime lifetime,
            [FromServices] IConfiguration config,
            [FromServices] ILogger<TakeoutImporter> logger,
            CancellationToken ct) =>
        {
            var who = http.Request.Headers["X-User-Id"].ToString();
            if (string.IsNullOrWhiteSpace(who))
                return Results.Ok(ApiResponse<object>.Fail("로그인이 필요합니다.", "C401"));

            var root = ImportRoot(config);
            if (root is null)
                return Results.Ok(ApiResponse<object>.Fail("Gmail:ImportRoot 가 설정되지 않았습니다.", "C400"));

            if (!TryResolve(root, body.File, out var fullPath, out var pathError))
                return Results.Ok(ApiResponse<object>.Fail(pathError!, "C400"));

            var account = await db.GmailAccounts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == body.AccountId, ct);

            if (account is null)
                return Results.Ok(ApiResponse<object>.Fail($"계정을 찾지 못했습니다: {body.AccountId}", "C404"));

            // 남의 메일함에 남의 mbox 를 부어 넣지 못하게 한다.
            if (!string.Equals(account.OwnerLoginId, who, StringComparison.OrdinalIgnoreCase))
                return Results.Ok(ApiResponse<object>.Fail("이 메일함의 주인이 아닙니다.", "C403"));

            if (!status.TryBegin(account.Id, Path.GetFileName(fullPath)))
                return Results.Ok(ApiResponse<object>.Fail("이미 적재가 돌고 있습니다.", "C409"));

            // 요청이 끝나면 요청 토큰은 바로 취소된다 — 수십 분 도는 일을 거기 매달면 안 된다.
            var stopping = lifetime.ApplicationStopping;
            _ = Task.Run(async () =>
            {
                using var scope = scopes.CreateScope();
                var importer = scope.ServiceProvider.GetRequiredService<TakeoutImporter>();
                try
                {
                    await importer.ImportAsync(account.Id, fullPath, stopping);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Takeout 적재가 백그라운드에서 끝나지 못했습니다");
                    if (status.Running) status.Finish(ex.Message);
                }
            }, CancellationToken.None);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                started = true,
                accountId = account.Id,
                file = Path.GetFileName(fullPath),
            }));
        })
        .WithName("GmailImportTakeout")
        .WithSummary("Takeout mbox 적재를 시작한다");

        // ── 적재 상태 ───────────────────────────────────────
        group.MapGet("/import/status", ([FromServices] GmailImportStatus status) =>
            Results.Ok(ApiResponse<object>.Ok(new
            {
                status.Running,
                status.AccountId,
                status.Path,
                status.Parsed,
                status.Inserted,
                duplicate = status.Parsed - status.Inserted,
                status.Failed,
                status.StartedAt,
                status.FinishedAt,
                status.Error,
            })))
        .WithName("GmailImportStatus")
        .WithSummary("적재가 어디까지 갔나");

        // ── 증분 수집 ───────────────────────────────────────
        //
        // 계정의 길(api·imap)에 따라 수집기가 갈린다. 자격이 없으면 터뜨리지 않고
        // 까닭을 note 에 담아 돌려준다 — 설정이 덜 된 것과 고장난 것은 다르다.
        group.MapPost("/sync", async (
            [FromBody] GmailSyncRequest body,
            HttpContext http,
            [FromServices] AiUsageDbContext db,
            [FromServices] Services.Gmail.GmailApiCollector api,
            [FromServices] Services.Gmail.ImapCollector imap,
            CancellationToken ct) =>
        {
            var who = http.Request.Headers["X-User-Id"].ToString();
            if (string.IsNullOrWhiteSpace(who))
                return Results.Ok(ApiResponse<object>.Fail("로그인이 필요합니다.", "C401"));

            var account = await db.GmailAccounts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == body.AccountId, ct);

            if (account is null)
                return Results.Ok(ApiResponse<object>.Fail($"계정을 찾지 못했습니다: {body.AccountId}", "C404"));

            if (!string.Equals(account.OwnerLoginId, who, StringComparison.OrdinalIgnoreCase))
                return Results.Ok(ApiResponse<object>.Fail("이 메일함의 주인이 아닙니다.", "C403"));

            if (!account.Enabled)
                return Results.Ok(ApiResponse<object>.Fail("꺼 둔 계정입니다.", "C400"));

            var max = Math.Clamp(body.MaxMessages ?? 500, 1, 5000);

            try
            {
                if (account.Kind == "imap")
                {
                    var r = await imap.SyncAsync(account, max, ct);
                    return Results.Ok(ApiResponse<object>.Ok(new
                    {
                        kind = "imap", fetched = r.Fetched, inserted = r.Inserted,
                        duplicate = r.Fetched - r.Inserted, lastUid = r.LastUid, note = r.Note,
                    }));
                }

                var a = await api.SyncAsync(account, max, ct);
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    kind = "api", fetched = a.Fetched, inserted = a.Inserted,
                    duplicate = a.Fetched - a.Inserted, historyId = a.HistoryId, note = a.Note,
                }));
            }
            catch (Exception ex)
            {
                // 다음에 볼 수 있게 계정에 까닭을 남긴다 — 로그만으로는 화면에서 안 보인다.
                await db.GmailAccounts.Where(x => x.Id == account.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastError, ex.Message), ct);
                throw;
            }
        })
        .WithName("GmailSync")
        .WithSummary("Gmail API · IMAP 으로 증분을 받아 온다");

        // ── 오래된 메일을 RAG 로 ─────────────────────────────
        //
        // 한 번에 maxMessages 만큼만 하고 끝난다. 0 이 돌아올 때까지 다시 부른다.
        group.MapPost("/archive", async (
            [FromBody] GmailArchiveRequest body,
            HttpContext http,
            [FromServices] Services.Gmail.GmailRagArchiver archiver,
            CancellationToken ct) =>
        {
            var who = http.Request.Headers["X-User-Id"].ToString();
            if (string.IsNullOrWhiteSpace(who))
                return Results.Ok(ApiResponse<object>.Fail("로그인이 필요합니다.", "C401"));

            var max = Math.Clamp(body.MaxMessages ?? 1000, 1, 20000);
            var result = await archiver.RunAsync(body.CategoryId, max, who, ct);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                documents = result.Documents,
                messages = result.Messages,
                note = result.Note,
            }));
        })
        .WithName("GmailArchive")
        .WithSummary("오래된 메일을 묶어 RAG 문서함에 올린다");

        // ── 쌓인 양 ─────────────────────────────────────────
        group.MapGet("/stats", async (
            [FromServices] AiUsageDbContext db,
            CancellationToken ct) =>
        {
            var rows = await db.GmailMessages.AsNoTracking()
                .GroupBy(x => new { x.AccountId, x.Source })
                .Select(g => new
                {
                    g.Key.AccountId,
                    g.Key.Source,
                    count = g.Count(),
                    oldest = g.Min(x => x.SentAt),
                    newest = g.Max(x => x.SentAt),
                })
                .OrderBy(x => x.AccountId).ThenBy(x => x.Source)
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(rows));
        })
        .WithName("GmailStats")
        .WithSummary("계정·길별로 몇 통이나 쌓였나");
    }

    private static string? ImportRoot(IConfiguration config)
    {
        var root = config["Gmail:ImportRoot"];
        return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root);
    }

    /// <summary>적재 폴더 안의 파일 이름 하나를 실제 경로로 푼다. 폴더 밖으로 나가는 이름은 거절한다.</summary>
    private static bool TryResolve(string root, string? file, out string fullPath, out string? error)
    {
        fullPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(file))
        {
            error = "파일 이름이 필요합니다.";
            return false;
        }

        // 폴더를 건너뛰는 이름(../ · 절대경로)은 애초에 받지 않는다.
        var name = Path.GetFileName(file);
        if (!string.Equals(name, file, StringComparison.Ordinal))
        {
            error = "적재 폴더 안의 파일 이름만 받습니다.";
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(root, name));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            error = "적재 폴더 밖의 파일입니다.";
            return false;
        }

        if (!File.Exists(candidate))
        {
            error = $"파일이 없습니다: {name}";
            return false;
        }

        fullPath = candidate;
        return true;
    }
}

public sealed record GmailAccountUpsert(
    string Id,
    string Email,
    string? OwnerLoginId,
    string Kind,
    bool Enabled = true);

public sealed record TakeoutImportRequest(string AccountId, string File);

public sealed record GmailArchiveRequest(string? CategoryId, int? MaxMessages);

public sealed record GmailSyncRequest(string AccountId, int? MaxMessages);
