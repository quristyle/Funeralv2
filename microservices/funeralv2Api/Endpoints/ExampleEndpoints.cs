using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;
using Funeralv2.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

public static class ExampleEndpoints
{
    public static void MapExampleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/").AddApiResponseWrapper();

        // 테이블 목록 예제
        group.MapGet("/table/list", async ([FromQuery] int page, [FromQuery] int pageSize, [FromServices] IDemoService demoService) =>
        {
            return await demoService.GetDemoTableListAsync(page, pageSize);
        })
        .WithName("GetDemoTableList")
        .WithOpenApi();

        // 파일 업로드 예제
        group.MapPost("/upload", async ([FromForm] IFormFile file, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ExampleEndpoints");
            logger.LogInformation("[Upload] Received upload request. FileName: {FileName}, Length: {Length} bytes, ContentType: {ContentType}", 
                file?.FileName, file?.Length, file?.ContentType);

            try
            {
                if (file == null || file.Length == 0)
                {
                    logger.LogWarning("[Upload] Upload failed. File is null or empty.");
                    return Results.BadRequest(ApiResponse<object>.Fail("파일이 유효하지 않습니다."));
                }

                var result = new UploadResultDto
                {
                    Url = $"https://example.com/uploads/{file.FileName}",
                    Filename = file.FileName,
                    Size = file.Length
                };
                
                logger.LogInformation("[Upload] Upload success. Returning result URL: {Url}", result.Url);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Upload] Exception occurred during file upload processing.");
                return Results.Json(ApiResponse<object>.Fail($"업로드 중 오류 발생: {ex.Message}"), statusCode: 500);
            }
        })
        .WithName("UploadFile")
        .WithOpenApi()
        .DisableAntiforgery()
        .WithMetadata(new Microsoft.AspNetCore.Mvc.DisableRequestSizeLimitAttribute());
    }
}
