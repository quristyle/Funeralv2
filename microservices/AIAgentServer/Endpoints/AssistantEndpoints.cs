using System.Text.Json;

using AIAgentServer.Data;
using AIAgentServer.DTOs;
using AIAgentServer.Services.Portal;
using AIAgentServer.Services.Rag;

using JSini.Shared.DTOs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Endpoints;

/// <summary>업무 비서 화면(포털관리 안, 슈퍼관리자·임원 전용)이 쓰는 길. 그 화면은 메뉴·권한 쪽에서 이미
/// 사람을 좁혀 두므로, 여기서는 <see cref="RagLibrary.EnsureCategoryAccessAsync"/> 같은 카테고리별
/// 권한 검사를 다시 묻지 않는다 — AI쳇의 <c>/rag/chat-scopes</c>·<c>/ai/chat/stream</c> 과는 다른 전제다.</summary>
public static class AssistantEndpoints
{
    private const int DefaultAuditTake = 30;
    private const int MaxAuditTake = 100;

    public static void MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/assistant").WithTags("Assistant");

        // ── 대화 (SSE) ──────────────────────────────────────
        //
        // 모양은 AIEndpoints.cs 의 StreamChat 과 같다 — 화면이 두 길을 같은 파서로 읽는다.
        group.MapPost("/chat", async (
            HttpContext context,
            [FromBody] AssistantChatRequest request,
            [FromServices] RagLibrary ragLibrary,
            [FromServices] AssistantAgent agent,
            CancellationToken ct) =>
        {
            // 헤더를 아직 안 보냈을 때만 상태 코드로 거부할 수 있다 — 그래서 가장 먼저 본다.
            var askedBy = context.Request.Headers["X-User-Id"].ToString();
            if (string.IsNullOrWhiteSpace(askedBy))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("로그인이 필요합니다.", "C401"));
                return;
            }

            if (request.Messages is not { Count: > 0 })
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("메시지 내역이 없습니다.", "C400"));
                return;
            }

            // 범위를 고른 뒤 실패하면 스트리밍이 시작되기 전이라 여기서는 아직 상태 코드로 걸러도 된다.
            RagScope? scope = null;
            string? scopeError = null;
            try
            {
                // **빈 목록과 없는 것을 가른다.** 빈 목록은 「프로젝트를 골랐는데 거기
                // 매인 카테고리가 없다」라 찾을 문서가 없다는 뜻이고, 없는 것은
                // 「범위를 안 정했다」라 사내 자료 전부를 본다. 둘을 같게 보면
                // 범위를 좁힌 줄 알았는데 조용히 전부를 뒤지게 된다.
                if (request.RagCategories is { } many)
                {
                    var label = string.IsNullOrWhiteSpace(request.RagScopeLabel)
                        ? $"카테고리 {many.Count}개"
                        : request.RagScopeLabel.Trim();

                    // 하나도 살아 있지 않아도 대화를 끊지 않는다 — 문서 검색만 빈손이
                    // 되고 요청글·일정·메일 도구는 그대로 돈다. 빈 카테고리 목록을 받은
                    // RagStore.Search 가 아무 샤드도 안 본다.
                    scope = await ragLibrary.ResolveScopeAsync(many, label, ct)
                            ?? new RagScope([], null, label);
                }
                else if (!string.IsNullOrWhiteSpace(request.RagCategory))
                {
                    scope = await ragLibrary.ResolveChatScopeAsync(
                        request.RagCategory.Trim(), request.RagFolderId, ct);
                }
            }
            catch (InvalidOperationException ex)
            {
                scopeError = ex.Message;
            }

            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no"; // Nginx 등의 리버스 프록시 버퍼링 방지

            // Kestrel 및 YARP ApiGateway의 응답 버퍼링 완전 비활성화
            var responseBodyFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
            responseBodyFeature?.DisableBuffering();

            // 응답 스트림이 즉시 시작됨을 브라우저에 알림
            await context.Response.Body.FlushAsync(ct);

            // 헤더를 보낸 뒤라 상태 코드로는 더 이상 실패를 알릴 수 없다 — 답 자리에 글로 흘린다.
            if (scopeError is not null)
            {
                await context.Response.WriteAsync($"data: {JsonSerializer.Serialize("⚠️ " + scopeError)}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
                return;
            }

            var messages = request.Messages
                .Select(m => new Message { role = m.role, content = m.content })
                .ToList();

            // AssistantAgent.StreamAsync 는 실패도 삼켜 답 글로 바꾸고 감사까지 남긴다 — 여기서는
            // 조각을 모양대로 실어 보내기만 한다.
            await foreach (var part in agent.StreamAsync(messages, scope, askedBy, ct))
            {
                var payload = part.Notice is null
                    ? JsonSerializer.Serialize(part.Text ?? string.Empty)
                    : JsonSerializer.Serialize(new { notice = part.Notice, kind = part.Kind });

                await context.Response.WriteAsync($"data: {payload}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        })
        .WithName("AssistantChat")
        .WithSummary("업무 비서 대화 (SSE)");

        // ── 자료 범위 고르개 ─────────────────────────────────
        //
        // 이 화면은 메뉴 권한으로 이미 좁혀진 사람(슈퍼관리자·임원)만 열 수 있어, AI쳇의
        // `/rag/chat-scopes` 와 달리 소속 회사로 다시 거르지 않고 전체 카테고리를 보여 준다.
        group.MapGet("/scopes", async (
            [FromServices] RagLibrary ragLibrary,
            CancellationToken ct) =>
        {
            var rows = await ragLibrary.CategoriesAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(rows.Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.CompanyId,
                companyName = c.Company?.Name,
            })));
        })
        .WithName("AssistantScopes")
        .WithSummary("업무 비서가 찾아볼 수 있는 전체 자료 범위");

        // ── 감사 기록 ───────────────────────────────────────
        group.MapGet("/audit", async (
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] string? askedBy,
            [FromQuery] int? take,
            [FromServices] AiUsageDbContext db,
            CancellationToken ct) =>
        {
            var query = db.AssistantAudits.AsNoTracking().AsQueryable();

            try
            {
                if (!string.IsNullOrWhiteSpace(from))
                {
                    var startUtc = PortalDay.StartUtc(from);
                    query = query.Where(x => x.AskedAt >= startUtc);
                }

                if (!string.IsNullOrWhiteSpace(to))
                {
                    var endExclusiveUtc = PortalDay.EndExclusiveUtc(to);
                    query = query.Where(x => x.AskedAt < endExclusiveUtc);
                }
            }
            catch (ArgumentException ex)
            {
                return Results.Ok(ApiResponse<object>.Fail(ex.Message, "C400"));
            }

            if (!string.IsNullOrWhiteSpace(askedBy))
            {
                query = query.Where(x => x.AskedBy == askedBy);
            }

            // 자르기 앞에서 센다 — 그래야 화면이 "몇 건 중 몇 건을 보는지" 를 말할 수 있다.
            var total = await query.CountAsync(ct);

            var takeCount = take is { } t ? Math.Clamp(t, 1, MaxAuditTake) : DefaultAuditTake;

            var rows = await query
                .OrderByDescending(x => x.AskedAt)
                .Take(takeCount)
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                total,
                items = rows.Select(r => new
                {
                    r.Id,
                    r.AskedBy,
                    r.AskedAt,
                    r.Question,
                    r.ScopeCategory,
                    toolCalls = ParseJsonArray(r.ToolCalls),
                    sources = ParseJsonArray(r.Sources),
                    r.Answer,
                    r.ElapsedMs,
                    r.Error,
                }),
            }));
        })
        .WithName("AssistantAudit")
        .WithSummary("업무 비서 감사 기록 — 권한 밖 자료를 넘겨다본 흔적");
    }

    private static List<string> ParseJsonArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>업무 비서 대화 요청. 지난 대화까지 전부 담아 보낸다 — 서버는 대화 상태를 저장하지 않는다.</summary>
public sealed class AssistantChatRequest
{
    public List<Message> Messages { get; set; } = [];

    /// <summary>사내 자료를 찾을 카테고리. 비우면 범위를 좁히지 않는다.</summary>
    /// <see cref="RagCategories"/> 가 오면 그쪽이 이긴다.
    public string? RagCategory { get; set; }

    /// <summary>
    /// 찾을 카테고리 <b>여럿</b>. 화면이 고른 프로젝트에 매인 카테고리를 모아 보낸다.
    /// </summary>
    /// 빈 목록과 없는 것은 다르다 — 빈 목록은 「고른 프로젝트에 매인 카테고리가 하나도
    /// 없다」라 범위를 좁힐 수 없다는 뜻이고, 없는 것은 「범위를 안 정했다」다.
    public List<string>? RagCategories { get; set; }

    /// <summary>그 카테고리 안의 분류. 카테고리 없이 오면 무시한다. <see cref="RagCategories"/> 와는 같이 못 쓴다.</summary>
    public long? RagFolderId { get; set; }

    /// <summary>안내에 보여 줄 범위 이름(고른 프로젝트). 비우면 서버가 카테고리 수로 적는다.</summary>
    public string? RagScopeLabel { get; set; }

    /// <summary>화면이 대화창을 구분하려고 쓰는 자리. 업무 비서는 기록을 세션 단위로 묶지 않으므로
    /// (감사표에 세션 칸이 없다) 지금은 받기만 하고 쓰지 않는다.</summary>
    public string? SessionId { get; set; }
}
