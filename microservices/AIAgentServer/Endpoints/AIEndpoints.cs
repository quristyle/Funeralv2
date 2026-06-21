using System.Text.Json;
using AIAgentServer.Services;
using Funeralv2.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentServer.Endpoints;

public static class AIEndpoints
{
    public static void MapAIEndpoints(this IEndpointRouteBuilder app)
    {
        // Gateway 설정에서 PathRemovePrefix: "/api/ai" 가 설정되어 있으므로, 
        // 클라이언트가 /api/ai/suggest-code 호출 시, 이 백엔드로는 /suggest-code 가 전달됩니다.
        var group = app.MapGroup("/").WithTags("AI Agent Services");

        group.MapPost("/chat", async ([FromBody] AIAgentServer.DTOs.ChatRequestDto request, [FromServices] ILLMService llmService) =>
        {
            if (request.Messages == null || request.Messages.Count == 0)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("메시지 내역이 없습니다.", "C400"));
            }

            var reply = await llmService.ChatAsync(request.Messages);
            return Results.Ok(ApiResponse<string>.Ok(reply));
        })
        .WithName("GeneralChat")
        .WithOpenApi();

        group.MapPost("/chat/stream", async (HttpContext context, [FromBody] AIAgentServer.DTOs.ChatRequestDto request, [FromServices] ILLMService llmService) =>
        {
            if (request.Messages == null || request.Messages.Count == 0)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("메시지 내역이 없습니다.", "C400"));
                return;
            }

            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no"; // Nginx 등의 리버스 프록시 버퍼링 방지

            // Kestrel 및 YARP ApiGateway의 응답 버퍼링 완전 비활성화
            var responseBodyFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
            responseBodyFeature?.DisableBuffering();

            // 응답 스트림이 즉시 시작됨을 브라우저에 알림
            await context.Response.Body.FlushAsync();

            await foreach (var content in llmService.StreamChatAsync(request.Messages))
            {
                // SSE 형식에 맞춰 데이터 전송 (data: {content}\n\n)
                await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(content)}\n\n");
                await context.Response.Body.FlushAsync();
            }
        })
        .WithName("StreamChat")
        .WithOpenApi();

        group.MapGet("/suggest-code", async ([FromQuery] string word, [FromQuery] bool? natural, [FromServices] ILLMService llmService) =>
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("입력값이 없습니다.", "C400"));
            }

            var suggestedCode = await llmService.SuggestCommonCodeAsync(word, natural ?? false);
            return Results.Ok(ApiResponse<string>.Ok(suggestedCode));
        })
        .WithName("SuggestCommonCode")
        .WithOpenApi();

        group.MapGet("/suggest-i18n", async ([FromQuery] string key, [FromQuery] string targetLang, [FromServices] ILLMService llmService) =>
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(targetLang))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("필수 파라미터가 누락되었습니다.", "C400"));
            }

            var suggested = await llmService.SuggestI18nTranslationAsync(key, targetLang);
            return Results.Ok(ApiResponse<string>.Ok(suggested));
        })
        .WithName("SuggestI18nTranslation")
        .WithOpenApi();
    }
}
