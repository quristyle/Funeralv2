using AIAgentServer.DTOs;
using AIAgentServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentServer.Endpoints;

/// <summary>내 AI쳇 대화를 열고 닫는 길. 화면(<c>/site/ai/chat</c>)과 헤더 🤖 서랍이 쓴다. 사용량 조회()는 남의 것을 보는 길이라 메뉴 권한을 물었다.</summary>
public static class AiChatSessionEndpoints
{
    public static void MapAiChatSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/chat/sessions");

        // ── 내 대화 목록 ────────────────────────────────────
        //
        // 최근에 말이 오간 순이다. 화면은 맨 앞엣것을 기본으로 연다 —
        // 「새로고침하면 보던 것이 그대로」가 대개 그 한 줄로 끝난다.
        group.MapGet("/", async (
            UserContext? user,
            [FromServices] AiChatStore store,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var rows = await store.ListAsync(user.UserId, ct);

            return Results.Ok(ApiResponse<List<AiChatSessionDto>>.Ok(
                [.. rows.Select(ToDto)]));
        })
        .WithName("ListAiChatSessions");

        // ── 새 대화 ─────────────────────────────────────────
        //
        // 제목 없이 연다. **첫 질문이 제목이 된다**(AiChatStore) —
        // 물어보려고 연 창에서 제목부터 지으라고 하면 그 자리에서 막힌다.
        group.MapPost("/", async (
            UserContext? user,
            [FromServices] AiChatStore store,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var row = await store.CreateAsync(user.UserId, ct);
            return Results.Ok(ApiResponse<AiChatSessionDto>.Ok(ToDto(row)));
        })
        .WithName("CreateAiChatSession");

        // ── 대화 열기 ───────────────────────────────────────
        //
        // 없는 열쇠와 남의 열쇠가 **똑같이 404** 다. 가르면 「그 대화가 있긴
        // 하다」가 새어 나간다.
        group.MapGet("/{id}", async (
            string id,
            UserContext? user,
            [FromServices] AiChatStore store,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var rows = await store.MessagesAsync(user.UserId, id, ct);

            if (rows is null)
            {
                return Results.NotFound(ApiResponse<object>.Fail("대화를 찾을 수 없습니다.", "404"));
            }

            return Results.Ok(ApiResponse<List<AiChatMessageDto>>.Ok(
                [.. rows.Select(m => new AiChatMessageDto
                {
                    Role = m.Role,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                })]));
        })
        .WithName("GetAiChatSession");

        // ── 이름 바꾸기 ─────────────────────────────────────
        //
        // 비워 보내면 제목을 **지운다**. 그때 화면은 「새 대화」로 그린다 —
        // 첫 질문에서 다시 따오지는 않는다(사람이 일부러 지운 것이므로).
        group.MapPut("/{id}", async (
            string id,
            [FromBody] RenameAiChatSessionDto request,
            UserContext? user,
            [FromServices] AiChatStore store,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var ok = await store.RenameAsync(user.UserId, id, request.Title, ct);

            return ok
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<object>.Fail("대화를 찾을 수 없습니다.", "404"));
        })
        .WithName("RenameAiChatSession");

        // ── 지우기 ──────────────────────────────────────────
        //
        // **표시만 해 두지 않는다.** 본인이 물어본 말이라, 지웠는데 어딘가
        // 남아 있으면 그 기대를 어기는 것이다. 마디도 함께 사라진다.
        group.MapDelete("/{id}", async (
            string id,
            UserContext? user,
            [FromServices] AiChatStore store,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var ok = await store.DeleteAsync(user.UserId, id, ct);

            return ok
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<object>.Fail("대화를 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteAiChatSession");
    }

    private static AiChatSessionDto ToDto(Data.AiChatSessionRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt,
    };
}
