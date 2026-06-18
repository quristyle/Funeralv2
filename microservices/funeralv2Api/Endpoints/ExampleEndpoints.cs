using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;

namespace funeralv2Api.Endpoints;

public static class ExampleEndpoints
{
    public static void MapExampleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/");

        // 테이블 목록 예제
        group.MapGet("/table/list", async ([FromQuery] int page, [FromQuery] int pageSize, [FromServices] IDemoService demoService) =>
        {
            var result = await demoService.GetDemoTableListAsync(page, pageSize);
            return Results.Ok(ApiResponse<PagedResultDto<DemoTableDto>>.Ok(result));
        })
        .WithName("GetDemoTableList")
        .WithOpenApi();

        // 파일 업로드 예제
        group.MapPost("/upload", async (IFormFile file) =>
        {
            var result = new UploadResultDto
            {
                Url = $"https://example.com/uploads/{file.FileName}",
                Filename = file.FileName,
                Size = file.Length
            };
            return Results.Ok(ApiResponse<UploadResultDto>.Ok(result));
        })
        .WithName("UploadFile")
        .WithOpenApi()
        .DisableAntiforgery();
    }
}
