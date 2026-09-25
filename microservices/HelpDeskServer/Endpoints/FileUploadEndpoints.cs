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
///
/// <para>
/// [길이 둘이다 — 첨부(<c>/upload</c>)와 본문 그림(<c>/image</c>)]
/// </para>
///
/// <para>
/// 둘 다 바이트를 FileServer 로 보내는 것은 같지만 <b>남기는 흔적이 다르다.</b>
/// 첨부는 <c>attachment</c> 에 줄을 남겨 목록에 뜨고, 본문 그림은 남기지 않는다 —
/// 그 그림의 자리는 글 안이고, 첨부 목록에까지 또 뜨면 같은 그림이 두 번 보인다.
/// </para>
/// </remarks>
public static class FileUploadEndpoints
{
    /// <summary>
    /// 본문에 붙이는 그림 한 장의 상한.
    /// </summary>
    /// <remarks>
    /// 화면 캡처 한 장이 이보다 큰 일은 없다. 상한을 두는 이유는 사람이 실수로
    /// 영상 파일을 붙여넣었을 때 <b>FileServer 까지 갔다가 거절당하는 대신</b>
    /// 여기서 바로 돌려보내기 위해서다.
    /// </remarks>
    private const long MaxImageBytes = 20L * 1024 * 1024;

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

            var attachments = new List<Attachment>();
            var failures = new List<string>();

            var client = CreateClient(httpFactory);
            var uploadUrl = ResolveUploadUrl(configuration);
            var bizType = ResolveBizType(configuration);

            // 이 요청의 토큰을 그대로 넘긴다. FileServer 업로드는 익명으로 열려 있지 않다
            // (예전에는 아무나 올릴 수 있었고 그 구멍은 닫혔다).
            var auth = http.Request.Headers.Authorization.ToString();

            foreach (var file in files)
            {
                if (file.Length <= 0) continue;

                var (fileId, error) = await SendToFileServerAsync(
                    client, uploadUrl, bizType, auth, file, logger);

                if (fileId is null)
                {
                    // 조용히 넘기지 않는다. 올라간 줄 알았는데 없는 것이 가장 나쁘다.
                    failures.Add($"{file.FileName} ({error})");
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

            // **봉투에 담아 돌려준다.** 예전에는 배열을 맨몸으로 돌려줬는데,
            // 포털의 헬프데스크 클라이언트는 어떤 응답이든 `{success, data}` 로
            // 읽는다(HelpDeskEnvelope). 배열이 오면 해석에 실패해 예외가 되고,
            // 그 예외를 등록 화면이 「첨부를 올리지 못했습니다」로 받는다 —
            // **파일은 멀쩡히 올라갔는데** 사용자에게는 늘 실패로 보였다.
            // 실패 쪽(위 502)은 처음부터 봉투 모양이었으므로 성공만 맞춰 준다.
            return Results.Ok(new
            {
                success = true,
                message = $"{attachments.Count}개 파일을 올렸습니다.",
                data = attachments
            });
        })
        .DisableAntiforgery();

        // ── 본문에 붙이는 그림 한 장 ──────────────────────────
        //
        // 요청 글 편집기에서 **클립보드 그림을 붙여넣는 순간** 불린다.
        // 요청이 아직 만들어지기 전에도 불리므로(등록 화면) 어느 글에 속하는지를
        // 받지 않고, 그래서 `attachment` 에 줄도 남기지 않는다. 글이 저장될 때
        // 본문 HTML 의 `<img src>` 가 그 파일을 가리키는 것이 유일한 연결이다.
        //
        // **base64 를 본문에 그대로 두지 않으려고 만든 길이다.** 예전에는 붙여넣은
        // 그림이 data URI 로 본문에 박힌 채 저장으로 갔고, 서버가 그것을 받아
        // `FileUtil.SaveImageToFile` 로 **배포 장비 디스크**에 떨궜다(결정 D5-B 가
        // 그만두기로 한 바로 그 방식이고, 컨테이너 안에서는 그 경로가 날아간다).
        // 화면 캡처 한 장에 수 MB 라 회로(SignalR) 수신 한도(4MB)도 위태로웠다.
        group.MapPost("/image", async (
            HttpRequest httpRequest,
            IHttpClientFactory httpFactory,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("FileUploadEndpoints");

            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "폼으로 보내야 합니다."
                });
            }

            var form = await httpRequest.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

            if (file is null || file.Length <= 0)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "보낼 그림이 없습니다."
                });
            }

            if (file.Length > MaxImageBytes)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = $"그림 한 장은 {MaxImageBytes / 1024 / 1024}MB 까지 붙일 수 있습니다."
                });
            }

            // **그림만 받는다.** 이 길은 본문에 `<img>` 로 박으려고 여는 것이라,
            // 다른 종류를 받아 주면 화면에 깨진 네모만 남는다. 파일을 첨부하려는
            // 것이면 첨부 칸(`/upload`)이 따로 있다.
            if (!IsImage(file.ContentType))
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "그림 파일만 본문에 붙일 수 있습니다. 다른 파일은 첨부로 올려 주십시오."
                });
            }

            var (fileId, error) = await SendToFileServerAsync(
                CreateClient(httpFactory),
                ResolveUploadUrl(configuration),
                ResolveBizType(configuration),
                httpRequest.Headers.Authorization.ToString(),
                file,
                logger);

            if (fileId is null)
            {
                return Results.Json(new
                {
                    success = false,
                    message = $"그림을 올리지 못했습니다 ({error})."
                }, statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(new
            {
                success = true,
                message = "그림을 올렸습니다.",
                data = new
                {
                    fileId,
                    // 본문에 박아 둘 주소. **저장되는 값이 이것**이므로 오리진을
                    // 붙이지 않는다 — 환경마다 다르고, 보는 쪽이 옮긴다
                    // (포털은 JSini.Web.Components 의 FileDownload.RelayUrl).
                    url = $"/api/file/download/id/{fileId}",
                    fileName = file.FileName,
                    contentType = file.ContentType,
                    fileSize = file.Length
                }
            });
        })
        .DisableAntiforgery();
    }

    /// <summary>FileServer 로 보낼 주소. 게이트웨이를 거친다.</summary>
    private static string ResolveUploadUrl(IConfiguration configuration) =>
        configuration.GetValue<string>("FileServer:UploadUrl")
        ?? Environment.GetEnvironmentVariable("FileServer_UploadUrl")
        ?? "http://localhost:5265/api/file/upload";

    /// <summary>
    /// FileServer 의 업무 구분. <b>그대로 저장 폴더 이름이 된다.</b>
    /// </summary>
    /// <remarks>
    /// 익명 열람 판정(<c>FileServer/Endpoints/PublicFileAccessFilter</c>)이
    /// 폴더 앞머리를 보는데 헬프데스크 폴더는 <b>일부러 목록에 없다.</b>
    /// 아무거나로 바꾸면 요청 본문의 그림이 아이디를 아는 사람에게 열린다.
    /// </remarks>
    private static string ResolveBizType(IConfiguration configuration) =>
        configuration.GetValue<string>("FileServer:BizType") ?? "helpdesk-improvement";

    private static HttpClient CreateClient(IHttpClientFactory httpFactory)
    {
        var client = httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }

    /// <summary>본문에 박아도 되는 종류인가.</summary>
    private static bool IsImage(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 파일 하나를 FileServer 로 보낸다. 성공하면 파일 아이디, 실패하면 까닭.
    /// </summary>
    /// <remarks>
    /// 첨부와 본문 그림이 <b>같은 길로 간다.</b> 두 벌로 두면 한쪽만 고쳐져
    /// 「첨부는 되는데 본문 그림은 안 된다」 같은 것이 생긴다.
    /// </remarks>
    private static async Task<(string? FileId, string? Error)> SendToFileServerAsync(
        HttpClient client,
        string uploadUrl,
        string bizType,
        string? auth,
        IFormFile file,
        ILogger logger)
    {
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
                logger.LogError(
                    "FileServer 업로드 실패. file={File} status={Status} body={Body}",
                    file.FileName, (int)response.StatusCode, body);
                return (null, $"HTTP {(int)response.StatusCode}");
            }

            var fileId = ExtractFileId(body);
            if (fileId is null)
            {
                logger.LogError(
                    "FileServer 응답에서 파일 아이디를 찾지 못했습니다. file={File} body={Body}",
                    file.FileName, body);
                return (null, "응답에 파일 아이디가 없음");
            }

            return (fileId, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FileServer 업로드 중 오류. file={File}", file.FileName);
            return (null, ex.Message);
        }
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
