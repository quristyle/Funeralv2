using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 파일 업로드 엔드포인트
/// </summary>
/// <remarks>
/// <b>파일 바이트는 이제 이 서비스가 갖지 않는다.</b> FileServer 로 보내고
/// 돌려받은 파일 아이디만 <see cref="Attachment.FileId"/> 에 적는다 (결정 D5-B).
///
/// <para>
/// 예전에는 여기서 직접 디스크에 썼다. FileServer 라는 전용 서비스가 있는데도
/// 같은 일을 두 곳에서 다르게 하고 있었고, 백업 대상도 용량 관리도 둘이었다.
/// </para>
///
/// <para>
/// 저장 경로가 환경변수 <c>FileStorage_BasePath</c> 였고 기본값이
/// <c>/home/lee/jinAttachment</c> 였다. 그래서 실제 데이터가 두 경로로 갈려 있다
/// (35건 <c>/home/lee</c>, 2건 <c>/home/quri</c>). 이제 새 파일은 그 어느 쪽으로도 가지 않는다.
/// </para>
///
/// <para>
/// 기존 37건은 <c>deploy/attachment-migration/migrate.py</c> 가 옮긴다 —
/// 파일 바이트가 배포 장비 디스크에만 있어서 그 장비에서 돌려야 한다.
/// </para>
/// </remarks>
public static class FileUploadEndpoints
{
    /// <summary>
    /// 파일 업로드 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapFileUploadEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/files");

        group.MapPost("/upload", async (
            [FromForm] IFormFileCollection files,
            [FromForm] string entityType,
            [FromForm] int entityId,
            AppDbContext db,
            HttpContext http,
            IHttpClientFactory httpFactory,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("FileUploadEndpoints");

            // FileServer 로 보낼 주소. 게이트웨이를 거친다.
            var uploadUrl = configuration.GetValue<string>("FileServer:UploadUrl")
                            ?? Environment.GetEnvironmentVariable("FileServer_UploadUrl")
                            ?? "http://localhost:5265/api/file/upload";

            var bizType = configuration.GetValue<string>("FileServer:BizType")
                          ?? "helpdesk-improvement";

            var attachments = new List<Attachment>();
            var failures = new List<string>();

            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            // 이 요청의 토큰을 그대로 넘긴다. FileServer 업로드는 익명으로 열려 있지 않다
            // (예전에는 아무나 올릴 수 있었고 그 구멍은 닫혔다).
            var auth = http.Request.Headers.Authorization.ToString();

            foreach (var file in files)
            {
                if (file.Length <= 0) continue;

                try
                {
                    using var form = new MultipartFormDataContent();
                    await using var stream = file.OpenReadStream();
                    var part = new StreamContent(stream);

                    if (!string.IsNullOrWhiteSpace(file.ContentType))
                    {
                        part.Headers.ContentType =
                            new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    }

                    form.Add(part, "file", file.FileName);
                    form.Add(new StringContent(bizType), "bizType");

                    using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
                    {
                        Content = form
                    };
                    if (!string.IsNullOrWhiteSpace(auth))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", auth);
                    }

                    using var response = await client.SendAsync(request);
                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        // 조용히 넘기지 않는다. 올라간 줄 알았는데 없는 것이 가장 나쁘다.
                        logger.LogError(
                            "FileServer 업로드 실패. file={File} status={Status} body={Body}",
                            file.FileName, (int)response.StatusCode, body);
                        failures.Add($"{file.FileName} (HTTP {(int)response.StatusCode})");
                        continue;
                    }

                    var fileId = ExtractFileId(body);
                    if (fileId is null)
                    {
                        logger.LogError(
                            "FileServer 응답에서 파일 아이디를 찾지 못했습니다. file={File} body={Body}",
                            file.FileName, body);
                        failures.Add($"{file.FileName} (응답에 파일 아이디가 없음)");
                        continue;
                    }

                    attachments.Add(new Attachment
                    {
                        OriginalFileName = file.FileName,
                        FileType = file.ContentType ?? "application/octet-stream",
                        FileSize = file.Length,
                        EntityType = entityType,
                        EntityId = entityId,
                        UploadedAt = DateTime.UtcNow,
                        // 파일은 FileServer 가 갖는다. 로컬 경로는 더 이상 쓰지 않는다.
                        FileId = fileId,
                        MigratedAt = DateTime.UtcNow,
                        StoredFileName = string.Empty,
                        FilePath = string.Empty
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "FileServer 업로드 중 오류. file={File}", file.FileName);
                    failures.Add($"{file.FileName} ({ex.Message})");
                }
            }

            if (attachments.Count > 0)
            {
                db.Attachments.AddRange(attachments);
                await db.SaveChangesAsync();
            }

            // 하나라도 실패했으면 알려 준다. 200 으로 덮으면 사용자는 다 올라간 줄 안다.
            if (failures.Count > 0)
            {
                return Results.Json(new
                {
                    success = false,
                    message = $"{failures.Count}개 파일을 올리지 못했습니다: {string.Join(", ", failures)}",
                    data = attachments
                }, statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(attachments);
        })
        .DisableAntiforgery();
    }

    /// <summary>
    /// FileServer 응답에서 파일 아이디를 꺼낸다.
    /// </summary>
    /// <remarks>
    /// 봉투가 <c>{ data: { result: [ { id } ] } }</c> 다. 배열로 올 수도 객체로 올 수도 있어
    /// 둘을 모두 받아 준다.
    /// </remarks>
    private static string? ExtractFileId(string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("result", out var result)) return null;

            var item = result.ValueKind == System.Text.Json.JsonValueKind.Array
                ? (result.GetArrayLength() > 0 ? result[0] : default)
                : result;

            if (item.ValueKind != System.Text.Json.JsonValueKind.Object) return null;

            return item.TryGetProperty("id", out var id) ? id.ToString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
