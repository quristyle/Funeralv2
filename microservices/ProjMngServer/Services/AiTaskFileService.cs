using System.Data;
using System.Text;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>AI 작업 첨부(<c>projmng.ai_task_file</c>). 바이트를 DB 에 담는다 — 이 컨테이너에는 디스크가 없고, 바이트를 실제로 읽는 쪽은 게이트웨이를 못 지나는 실행기다.
/// 상한(<see cref="MaxBytes"/>)은 게이트웨이 본문 상한·RequestSizeLimit·화면 MaxFileBytes 와 함께 올려야 한다 — 한 군데만 올리면 고르기는 되고 올리다 끊긴다.</summary>
public sealed class AiTaskFileService(IConfiguration configuration, ILogger<AiTaskFileService> logger)
{
    /// <summary>파일 한 개의 최대 크기.</summary>
    public const long MaxBytes = 100L * 1024 * 1024;

    /// <summary>작업 한 건에 붙일 수 있는 개수.</summary>
    public const int MaxCount = 5;

    /// <summary>묶이지 못한 첨부를 하루 뒤에 치운다. 바로 치우지 않는 것은 붙여 두고 화면을 떠났다 돌아오는 일이 흔하기 때문이다.</summary>
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

    /// <summary>내가 붙여 두고 아직 안 보낸 것. 화면을 다시 열면 이것으로 붙여 둔 것을 그대로 보인다. 번호를 브라우저에 적어 두지 않는다 — 서버에 남은 것과 어긋나 없는 첨부가 붙어 보이기 때문이다.</summary>
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

    /// <summary>고른 파일을 작업 번호 없이 담는다 — 「빠른 지시」가 고르는 순간 부르며, 그때는 번호가 없어 보낼 때 <see cref="BindAsync"/> 가 묶는다. 담기 전에 먼저 치운다.</summary>
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

            // 메모리에 한 장씩만 올린다 — Npgsql 이 bytea 를 배열로 받으므로 스트림으로 흘려도 어차피 여기서 모은다.
            // 담을 크기를 미리 잡는다 — 안 잡으면 MemoryStream 이 두 배씩 늘며 옮겨 담아 100MB 한 장에 세 배 가까운 메모리가 뜬다.
            using var buffer = new MemoryStream(checked((int)file.Length));
            await file.CopyToAsync(buffer, ct);

            // 잡은 만큼 정확히 찼으면 배열을 그대로 넘긴다 — ToArray() 는 한 벌을 더 만든다.
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
                CreDt = DateTime.UtcNow,
            });
        }

        return saved;
    }

    /// <summary>떠 있던 첨부를 작업에 묶는다. 올린 사람 것만, 그 번호이고 아직 아무 작업에도 안 묶인 것만 묶는다. 묶이지 못한 번호는 조용히 넘긴다 — 실패로 되돌리면 사진 한 장 때문에 지시 전체가 안 나간다.</summary>
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

    /// <summary>내려받기. 바이트를 읽는 자리는 여기 하나다. 목록(<see cref="ListAsync"/>)이 바이트를 안 담는 것은 카드 조회가 사진을 끌고 오지 않게 하려는 것이다.</summary>
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

    /// <summary>첨부 하나를 뗀다 — 보내기 전에만 된다. 묶인 뒤에도 지울 수 있으면 이미 돈 실행이 무엇을 보고 일했는지가 사라진다(지시문 스냅샷과 같은 이유).</summary>
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
            // 치우는 일이 실패해도 올리는 일을 막지 않는다 — 다음번에 다시 치운다.
            logger.LogWarning(ex, "떠 있던 첨부를 치우지 못했습니다.");
        }
    }

    /// <summary>경로 조작과 제어문자를 막고 길이를 줄인다. 확장자는 그대로 둔다 — 실행기가 파일을 풀 때 그 이름을 쓰고 AI 도 확장자로 무엇인지 안다.
    /// <c>InterfaceFileService.SafeName</c> 과 달리 가름자(<c>__</c>)를 지우지 않는다 — 여기는 메타를 이름에 담지 않는다.</summary>
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

    /// <summary>미리보기를 그릴 수 있는 그림인가. 형식과 확장자를 둘 다 본다 — 브라우저가 형식을 비워 보내기도 한다. <c>image/svg+xml</c> 은 그림으로 치지 않는다: 스크립트를 품을 수 있어 우리 출처에서 열리는 길이 생긴다.</summary>
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
