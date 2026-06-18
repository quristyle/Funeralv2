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

        group.MapGet("/suggest-code", async ([FromQuery] string word, [FromServices] ILLMService llmService) =>
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("입력값이 없습니다.", "C400"));
            }

            var suggestedCode = await llmService.SuggestCommonCodeAsync(word);
            return Results.Ok(ApiResponse<string>.Ok(suggestedCode));
        })
        .WithName("SuggestCommonCode")
        .WithOpenApi();
    }
}
