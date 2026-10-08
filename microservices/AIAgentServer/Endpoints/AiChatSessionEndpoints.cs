using AIAgentServer.DTOs;
using AIAgentServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentServer.Endpoints;

/// <summary>
/// 내 AI쳇 대화를 열고 닫는 길. 화면(<c>/site/ai/chat</c>)과 헤더 🤖 서랍이 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// [권한을 따지지 않는다 — <b>내 것만 보인다</b>]
/// </para>
///
/// <para>
/// 사용량 조회(<see cref="AiUsageEndpoints"/>)는 <b>남의 것을 보는</b> 길이라
/// 메뉴 권한을 물었다. 이쪽은 반대다 — 쪽지나 알림 설정처럼 누구나 제 것을
/// 다루는 일이고, 모든 조회가 <c>userId</c> 를 조건에 넣으므로 남의 대화는
/// 애초에 손에 잡히지 않는다(<see cref="AiChatStore"/> 머리말).
/// </para>
///
/// <para>
/// 다만 <b>로그인은 반드시 해야 한다.</b> 게이트웨이의 <c>ai-route</c> 가
/// 이 묶음을 익명으로 열어 두므로, 사람이 없으면 여기서 막는다 — 안 막으면
/// <c>user_id</c> 가 빈 대화가 한 뭉치 생기고 그것을 아무나 공유하게 된다.
/// </para>
/// </remarks>
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
