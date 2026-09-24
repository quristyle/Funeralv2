using System.Data;
using System.Text;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// AI 작업 첨부 — <c>projmng.ai_task_file</c>.
/// </summary>
/// <remarks>
/// <para>
/// 설계는 <c>docs/ai-task-runner.md</c> 의 「함께 보내는 파일」.
/// </para>
/// <para>
/// <b>바이트를 DB 에 담는다.</b> 왜 파일 시스템도 FileServer 도 아닌지는
/// <c>deploy/sql/projmng-ai-task-file-2026-09-25.sql</c> 머리말에 적었다 —
/// 요지는 이 컨테이너에 디스크가 없고, 이 바이트를 실제로 읽어야 하는 것은
/// 게이트웨이를 못 지나는 <b>실행기</b>라는 것이다.
/// </para>
/// <para>
/// 상한은 한 장 <see cref="MaxBytes"/> · 한 건 <see cref="MaxCount"/> 장이다.
/// </para>
/// <para>
/// <b>처음에는 한 장 10MB 였다.</b> 「이 화면이 이렇게 나온다」를 보여 주는
/// 사진 몇 장만 생각한 값이었는데, 실제로 붙는 것은 그것만이 아니었다 —
/// 로그 묶음 · 화면 녹화 · 내려받은 자료가 함께 온다. 10MB 는 휴대폰으로
/// 찍은 사진 <b>한 장</b>에도 걸리는 크기라, 붙이려던 것이 자주 거절당했다.
/// </para>
/// <para>
/// 그래서 100MB 로 올렸다. 이 값은 <b>혼자 서 있지 않다</b> — 게이트웨이의
/// 본문 상한(<c>ApiGateway/Program.cs</c>)과 이 창구의
/// <c>RequestSizeLimit</c>·<c>RequestFormLimits</c>
/// (<c>AiTaskFilesController.StageAsync</c>), 그리고 화면의
/// <c>AiAskPanel.MaxFileBytes</c> 가 같이 올라가야 한다. 한 군데만 올리면
/// <b>고르기는 되는데 올리다 끊긴다.</b>
/// </para>
/// </remarks>
public sealed class AiTaskFileService(IConfiguration configuration, ILogger<AiTaskFileService> logger)
{
    /// <summary>파일 한 개의 최대 크기.</summary>
    public const long MaxBytes = 100L * 1024 * 1024;

    /// <summary>작업 한 건에 붙일 수 있는 개수.</summary>
    public const int MaxCount = 5;

    /// <summary>
    /// 묶이지 못한 첨부를 얼마나 두고 치우나.
    /// </summary>
    /// <remarks>
    /// <b>바로 치우지 않는다.</b> 사진을 붙여 두고 글을 쓰다 화면을 떠났다가
    /// 잠시 뒤 돌아오는 일이 흔한데, 그때 붙여 둔 것이 없어져 있으면 다시
    /// 찍어야 한다. 하루면 「그 자리에서 이어 쓰는 것」은 전부 살아남고
    /// 「잊힌 것」은 전부 걸린다.
    /// </remarks>
    private static readonly TimeSpan LooseLife = TimeSpan.FromDays(1);

    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    private const string Columns = """
        file_key     AS FileKey,
        task_key     AS TaskKey,
        file_nm      AS FileNm,
        content_type AS ContentType,
        byte_size    AS ByteSize,
        is_image     AS IsImage,
        cre_id       AS CreId,
        cre_dt       AS CreDt
        """;

    /// <summary>작업 하나에 붙은 첨부. 올린 순서대로다.</summary>
    public async Task<List<AiTaskFile>> ListAsync(long taskKey)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiTaskFile>($"""
            SELECT {Columns}
              FROM projmng.ai_task_file
             WHERE task_key = @taskKey
             ORDER BY file_key
            """, new { taskKey });

        return [.. rows];
    }

    /// <summary>
    /// <b>내가 붙여 두고 아직 안 보낸 것.</b> 화면을 다시 열었을 때 붙여 둔
    /// 것이 그대로 보이게 하는 값이다.
    /// </summary>
    /// <remarks>
    /// 번호를 브라우저에 적어 두지 않는다 — 그러면 「브라우저가 기억하는 것」과
    /// 「서버에 실제로 남은 것」이 갈리고, 갈리는 쪽은 <b>있지도 않은 첨부가
    /// 붙어 보이는</b> 쪽이다(하루 지난 것은 서버가 치운다).
    /// </remarks>
    public async Task<List<AiTaskFile>> ListLooseAsync(string? userId)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiTaskFile>($"""
            SELECT {Columns}
              FROM projmng.ai_task_file
             WHERE task_key IS NULL
               AND cre_id   = @userId
             ORDER BY file_key
            """, new { userId = userId ?? string.Empty });

        return [.. rows];
    }

    /// <summary>
    /// 고른 파일을 <b>작업 번호 없이</b> 담아 둔다. 「빠른 지시」가 고르는
    /// 순간 부르는 자리다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 번호가 없는 채로 담기는 이유는 그 시점에 작업이 아직 없기 때문이다
    /// (SQL 파일 머리말). 보낼 때 <see cref="BindAsync"/> 가 묶는다.
    /// </para>
    /// <para>
    /// <b>담기 전에 먼저 치운다.</b> 묶이지 못하고 남은 것을 치우는 자리가
    /// 여기 하나뿐이라(따로 도는 일꾼을 두지 않았다), 이 자리를 건너뛰면
    /// 적다 만 사람의 사진이 영영 남는다.
    /// </para>
    /// </remarks>
    /// <param name="tooBig">크기를 넘은 파일이 있으면 그 이름으로 불린다.</param>
    public async Task<List<AiTaskFile>> StageAsync(
        IFormFileCollection files, string? userId, Action<string> tooBig, CancellationToken ct = default)
    {
        if (files.FirstOrDefault(f => f.Length > MaxBytes) is { } over)
        {
            tooBig(over.FileName);
            return [];
        }

        await SweepLooseAsync(userId);

        using var db = Open();

        var saved = new List<AiTaskFile>();

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            // **메모리에 한 장씩만 올린다.** Npgsql 에 `bytea` 로 넘기려면
            // 어차피 배열 하나가 통째로 필요해서, 스트림으로 흘려도 여기서
            // 다시 모으게 된다.
            //
            // **담을 크기를 미리 잡아 둔다.** 안 잡으면 MemoryStream 이 두 배씩
            // 늘리며 옮겨 담아, 100MB 한 장에 그 세 배 가까운 메모리가 잠깐씩
            // 뜬다. 길이는 IFormFile 이 이미 알고 있다 — 상한을 넘는 것은
            // 이 반복문에 닿기 전에 걸러졌다.
            using var buffer = new MemoryStream(checked((int)file.Length));
            await file.CopyToAsync(buffer, ct);

            // 잡아 둔 만큼 정확히 찼으면 그 배열을 그대로 넘긴다 — ToArray() 는
            // 같은 것을 한 벌 더 만든다(100MB 한 장에 100MB 를 더 쓴다).
            var bytes = buffer.TryGetBuffer(out var seg) && seg.Offset == 0 && seg.Count == seg.Array!.Length
                ? seg.Array!
                : buffer.ToArray();
            var name = SafeName(file.FileName);
            var type = SafeType(file.ContentType);

            var key = await db.ExecuteScalarAsync<long>("""
                INSERT INTO projmng.ai_task_file
                     ( task_key, file_nm, content_type, byte_size, content, is_image, cre_id, cre_dt )
                VALUES ( NULL, @name, @type, @size, @bytes, @image, @userId, now() )
                RETURNING file_key
                """, new
            {
                name,
                type,
                size = (long)bytes.Length,
                bytes,
                image = LooksLikeImage(type, name),
                userId,
            });

            saved.Add(new AiTaskFile
            {
                FileKey = key,
                FileNm = name,
                ContentType = type,
                ByteSize = bytes.Length,
                IsImage = LooksLikeImage(type, name),
                CreId = userId,
                CreDt = DateTime.Now,
            });
        }

        return saved;
    }

    /// <summary>
    /// 떠 있던 첨부를 작업에 묶는다. <b>올린 사람 것만 묶인다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 조건이 셋이다 — 그 번호일 것 · <b>올린 사람이 나일 것</b> · 아직 아무
    /// 작업에도 안 묶였을 것. 가운데 조건이 없으면 번호만 바꿔 보내서
    /// <b>남이 올린 사진을 제 작업에 붙일 수 있다</b>.
    /// </para>
    /// <para>
    /// 묶이지 못한 번호는 <b>조용히 넘긴다.</b> 여기서 실패로 되돌리면 사진
    /// 한 장 때문에 지시 자체가 안 나간다 — 이 화면에서 더 나쁜 쪽은 그쪽이다.
    /// 대신 몇 장이 붙었는지를 화면이 곧바로 다시 읽어 보여 준다.
    /// </para>
    /// </remarks>
    /// <returns>실제로 묶인 개수.</returns>
    public async Task<int> BindAsync(long taskKey, IReadOnlyCollection<long> fileKeys, string? userId)
    {
        if (fileKeys.Count == 0)
        {
            return 0;
        }

        using var db = Open();

        var bound = await db.ExecuteAsync("""
            UPDATE projmng.ai_task_file
               SET task_key = @taskKey
             WHERE file_key = ANY(@keys)
               AND task_key IS NULL
               AND cre_id   = @userId
            """, new { taskKey, keys = fileKeys.Take(MaxCount).ToArray(), userId = userId ?? string.Empty });

        if (bound != fileKeys.Count)
        {
            logger.LogWarning(
                "작업 {TaskKey} 에 첨부 {Asked}개를 묶으려 했는데 {Bound}개만 묶였습니다.",
                taskKey, fileKeys.Count, bound);
        }

        return bound;
    }

    /// <summary>내려받기. <b>바이트를 읽는 자리는 여기 하나다.</b></summary>
    /// <remarks>
    /// 목록(<see cref="ListAsync"/>)이 바이트를 안 담는 이유가 이것이다 —
    /// 카드를 그리는 조회가 사진을 함께 끌고 오면 안 된다.
    /// </remarks>
    public async Task<(AiTaskFile Meta, byte[] Bytes)?> OpenAsync(long fileKey)
    {
        using var db = Open();

        var row = await db.QuerySingleOrDefaultAsync<Blob>($"""
            SELECT {Columns}, content AS Content
              FROM projmng.ai_task_file
             WHERE file_key = @fileKey
            """, new { fileKey });

        if (row is null)
        {
            return null;
        }

        return (new AiTaskFile
        {
            FileKey = row.FileKey,
            TaskKey = row.TaskKey,
            FileNm = row.FileNm,
            ContentType = row.ContentType,
            ByteSize = row.ByteSize,
            IsImage = row.IsImage,
            CreId = row.CreId,
            CreDt = row.CreDt,
        }, row.Content ?? []);
    }

    /// <summary>메타와 바이트를 한 줄로 받는 그릇. 이 서비스 안에서만 쓴다.</summary>
    private sealed class Blob
    {
        public long FileKey { get; set; }
        public long? TaskKey { get; set; }
        public string FileNm { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long ByteSize { get; set; }
        public bool IsImage { get; set; }
        public string? CreId { get; set; }
        public DateTime? CreDt { get; set; }
        public byte[]? Content { get; set; }
    }

    /// <summary>
    /// 첨부 하나를 뗀다. <b>보내기 전에만</b> — 이미 작업에 묶인 것은 건드리지
    /// 않는다.
    /// </summary>
    /// <remarks>
    /// 묶인 뒤에도 지울 수 있게 두면 <b>이미 돈 실행이 무엇을 보고 일했는지</b>가
    /// 사라진다. 지시문(<c>ai_task_run.instruction</c>)을 스냅샷으로 남기는 것과
    /// 같은 이유다.
    /// </remarks>
    public async Task<bool> DeleteLooseAsync(long fileKey, string? userId)
    {
        using var db = Open();

        var gone = await db.ExecuteAsync("""
            DELETE FROM projmng.ai_task_file
             WHERE file_key = @fileKey
               AND task_key IS NULL
               AND cre_id   = @userId
            """, new { fileKey, userId = userId ?? string.Empty });

        return gone > 0;
    }

    /// <summary>묶이지 못하고 하루가 지난 것을 치운다.</summary>
    private async Task SweepLooseAsync(string? userId)
    {
        try
        {
            using var db = Open();

            var gone = await db.ExecuteAsync("""
                DELETE FROM projmng.ai_task_file
                 WHERE task_key IS NULL
                   AND cre_id   = @userId
                   AND cre_dt   < now() - make_interval(secs => @secs)
                """, new { userId = userId ?? string.Empty, secs = LooseLife.TotalSeconds });

            if (gone > 0)
            {
                logger.LogInformation("떠 있던 첨부 {Count}개를 치웠습니다 ({User}).", gone, userId);
            }
        }
        catch (NpgsqlException ex)
        {
            // **여기서 막히면 안 된다.** 치우는 일이 안 됐다고 올리는 일까지
            // 못 하게 할 이유가 없다 — 다음번에 다시 치운다.
            logger.LogWarning(ex, "떠 있던 첨부를 치우지 못했습니다.");
        }
    }

    /// <summary>
    /// 경로 조작과 제어문자를 막고 길이를 줄인다. <b>확장자는 그대로 둔다</b> —
    /// 실행기가 파일을 풀어 둘 때 그 이름을 쓰고, AI 도 확장자로 무엇인지 안다.
    /// </summary>
    /// <remarks>
    /// <c>InterfaceFileService.SafeName</c> 과 같은 규칙이다. 합치지 않은 것은
    /// 그쪽이 파일 이름에 메타를 담느라 가름자(<c>__</c>)까지 지우기 때문이다 —
    /// 여기는 표가 따로 있어 그럴 이유가 없다.
    /// </remarks>
    internal static string SafeName(string? name)
    {
        var n = Path.GetFileName(name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(n))
        {
            n = "file";
        }

        var bad = Path.GetInvalidFileNameChars()
            .Concat(['\\', '/', ':', '*', '?', '"', '<', '>', '|'])
            .ToHashSet();

        var sb = new StringBuilder(n.Length);

        foreach (var c in n)
        {
            sb.Append(bad.Contains(c) || char.IsControl(c) ? '_' : c);
        }

        // 앞의 점을 떼면 숨김파일과 `..` 흉내가 함께 막힌다.
        n = sb.ToString().TrimStart('.');

        if (n.Length > 120)
        {
            n = n[..60] + "~" + n[^40..];
        }

        return n.Length == 0 ? "file" : n;
    }

    /// <summary>올라온 형식 글자를 줄이고, 없으면 기본값으로 떨어뜨린다.</summary>
    private static string SafeType(string? contentType)
    {
        var t = (contentType ?? string.Empty).Trim();

        if (t.Length == 0 || t.Length > 150 || t.Any(char.IsControl))
        {
            return "application/octet-stream";
        }

        return t;
    }

    /// <summary>
    /// 미리보기를 그릴 수 있는 그림인가.
    /// </summary>
    /// <remarks>
    /// <b>형식과 확장자를 둘 다 본다.</b> 브라우저가 형식을 비워 보내는 일이
    /// 있고(일부 안드로이드 파일 고르개), 그때 확장자만 보면 살아난다.
    /// <para>
    /// <c>image/svg+xml</c> 은 <b>그림으로 치지 않는다.</b> SVG 는 스크립트를
    /// 품을 수 있는 문서라, 미리보기로 <c>&lt;img&gt;</c> 에 걸면 우리 출처에서
    /// 열리는 길이 생긴다 — 내려받기는 어차피 첨부로만 나간다.
    /// </para>
    /// </remarks>
    internal static bool LooksLikeImage(string? contentType, string? fileName)
    {
        if (contentType is not null
            && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".heic" or ".heif";
    }
}
