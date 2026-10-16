using System.Security.Cryptography;
using AIAgentServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services.Rag;

/// <summary>
/// RAG 가 읽을 문서를 보관하고 색인한다.
/// </summary>
/// <remarks>
/// <para>
/// <b>파일 실물은 운영시스템 디스크에 있고 LLM 서버는 갖지 않는다.</b>
/// 운영시스템이 파일을 읽어 글자로 바꾸고 토막을 내며, LLM 서버에는
/// 임베딩만 맡긴다(<c>/embed</c>). 그래서 문서가 장비 밖으로 통째로 나가지
/// 않고, LLM 서버를 갈아 끼워도 자료는 그대로 남는다.
/// </para>
/// <para>
/// <b>색인은 카테고리마다 하나다.</b> 분류(트리)는 사람이 정리하려고 있는 것이고
/// 검색은 카테고리 전체에서 한다 — 어느 서랍에 넣었는지 기억해야 찾을 수
/// 있다면 그것은 검색이 아니다.
/// </para>
/// </remarks>
public sealed class RagLibrary
{
    private readonly AiUsageDbContext _db;
    private readonly EmbeddingClient _embed;
    private readonly RagStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<RagLibrary> _logger;

    public RagLibrary(
        AiUsageDbContext db,
        EmbeddingClient embed,
        RagStore store,
        IConfiguration config,
        ILogger<RagLibrary> logger)
    {
        _db = db;
        _embed = embed;
        _store = store;
        _config = config;
        _logger = logger;
    }

    /// <summary>파일이 쌓이는 자리. 운영에서는 컨테이너에 물린 볼륨이다.</summary>
    public string Root
    {
        get
        {
            var v = _config["Rag:UploadRoot"];
            return string.IsNullOrWhiteSpace(v)
                ? Path.Combine(AppContext.BaseDirectory, "rag-docs")
                : v;
        }
    }

    // ── 카테고리 ────────────────────────────────────────────────

    public Task<List<RagCategoryRow>> CategoriesAsync(CancellationToken ct = default)
        => _db.RagCategories.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<RagCategoryRow> CreateCategoryAsync(
        string id, string name, string? description, string? who, CancellationToken ct = default)
    {
        id = Slug(id);
        if (id.Length == 0) throw new ArgumentException("열쇠는 영문·숫자·하이픈이어야 합니다.");

        if (await _db.RagCategories.AnyAsync(x => x.Id == id, ct))
        {
            throw new InvalidOperationException($"이미 있는 열쇠입니다: {id}");
        }

        var row = new RagCategoryRow
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
            Description = description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = who,
        };
        _db.RagCategories.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task DeleteCategoryAsync(string id, string? who, CancellationToken ct = default)
    {
        var row = await _db.RagCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("없는 카테고리입니다.");

        // 파일까지 지운다. **표만 지우면 디스크에 남아 쌓인다.**
        foreach (var d in await _db.RagDocuments.Where(x => x.CategoryId == id).ToListAsync(ct))
        {
            TryDeleteFile(d.StoredPath);
        }

        _db.RagCategories.Remove(row);   // 분류·문서는 FK CASCADE 가 지운다
        await _db.SaveChangesAsync(ct);

        _store.Remove(id);
        _logger.LogInformation("RAG 카테고리를 지웠습니다: {Id} ({Who})", id, who);
    }

    // ── 분류 (트리) ─────────────────────────────────────────────

    public Task<List<RagFolderRow>> FoldersAsync(string categoryId, CancellationToken ct = default)
        => _db.RagFolders.AsNoTracking()
            .Where(x => x.CategoryId == categoryId && !x.IsDeleted)
            .OrderBy(x => x.SortNo).ThenBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<RagFolderRow> CreateFolderAsync(
        string categoryId, long? parentId, string name, string? who, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("분류 이름이 없습니다.");

        var row = new RagFolderRow
        {
            CategoryId = categoryId,
            ParentId = parentId,
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = who,
        };
        _db.RagFolders.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task RenameFolderAsync(long id, string name, string? who, CancellationToken ct = default)
    {
        var row = await _db.RagFolders.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("없는 분류입니다.");
        row.Name = name.Trim();
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = who;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 분류를 지운다. <b>아래 분류와 문서가 함께 사라진다</b>(FK CASCADE).
    /// 문서는 분류를 잃고 카테고리 바로 아래로 올라간다 — SET NULL 이다.
    /// </summary>
    public async Task DeleteFolderAsync(long id, CancellationToken ct = default)
    {
        var row = await _db.RagFolders.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("없는 분류입니다.");
        _db.RagFolders.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    // ── 문서 ────────────────────────────────────────────────────

    public Task<List<RagDocumentRow>> DocumentsAsync(
        string categoryId, long? folderId, CancellationToken ct = default)
        => _db.RagDocuments.AsNoTracking()
            .Where(x => x.CategoryId == categoryId && !x.IsDeleted
                        && (folderId == null ? x.FolderId == null : x.FolderId == folderId))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<RagDocumentRow> UploadAsync(
        string categoryId, long? folderId, string fileName, string? contentType,
        Stream content, string? who, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName)) throw new ArgumentException("파일 이름이 없습니다.");

        var rel = Path.Combine(Slug(categoryId), DateTime.UtcNow.ToString("yyyyMM"),
                               $"{Guid.NewGuid():N}{Path.GetExtension(safeName)}");
        var full = Path.Combine(Root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        long size;
        string hash;
        await using (var fs = File.Create(full))
        {
            await content.CopyToAsync(fs, ct);
            size = fs.Length;
        }

        await using (var read = File.OpenRead(full))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(read, ct)).ToLowerInvariant();
        }

        var row = new RagDocumentRow
        {
            CategoryId = categoryId,
            FolderId = folderId,
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = size,
            Sha256 = hash,
            StoredPath = rel.Replace('\\', '/'),
            Status = DocumentText.IsSupported(safeName) ? "pending" : "skipped",
            Error = DocumentText.IsSupported(safeName)
                ? null
                : $"읽을 수 없는 형식입니다. 쓸 수 있는 것: {DocumentText.Describe()}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = who,
        };
        _db.RagDocuments.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<(string Path, string Name)?> OpenAsync(long id, CancellationToken ct = default)
    {
        var row = await _db.RagDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return null;

        var full = Path.Combine(Root, row.StoredPath);
        return File.Exists(full) ? (full, row.FileName) : null;
    }

    public async Task DeleteDocumentAsync(long id, CancellationToken ct = default)
    {
        var row = await _db.RagDocuments.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("없는 문서입니다.");

        TryDeleteFile(row.StoredPath);
        _db.RagDocuments.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    // ── 색인 ────────────────────────────────────────────────────

    /// <summary>
    /// 한 카테고리를 통째로 다시 색인한다.
    /// </summary>
    /// <remarks>
    /// <b>조금씩 고치지 않고 매번 다시 만든다.</b> 문서를 지우거나 옮겼을 때
    /// 색인에서 그 토막만 빼는 것은 어긋나기 쉽고, 지금 규모(문서 수십~수백 건)
    /// 에서는 전체를 다시 만드는 편이 빠르고 틀릴 일이 없다.
    /// </remarks>
    public async Task<RagIndexFile> ReindexAsync(string categoryId, CancellationToken ct = default)
    {
        var docs = await _db.RagDocuments
            .Where(x => x.CategoryId == categoryId && !x.IsDeleted)
            .ToListAsync(ct);

        var chunks = new List<RagChunk>();
        var seenHash = new HashSet<string>(StringComparer.Ordinal);

        foreach (var d in docs)
        {
            d.ChunkCount = 0;
            d.IndexedAt = DateTime.UtcNow;

            if (!DocumentText.IsSupported(d.FileName))
            {
                d.Status = "skipped";
                d.Error = $"읽을 수 없는 형식입니다. 쓸 수 있는 것: {DocumentText.Describe()}";
                continue;
            }

            // 같은 내용을 두 번 넣지 않는다. 분류를 옮기려고 다시 올리는 일이 흔하다.
            if (d.Sha256 is { Length: > 0 } h && !seenHash.Add(h))
            {
                d.Status = "skipped";
                d.Error = "같은 내용의 문서가 이미 색인에 있습니다.";
                continue;
            }

            var full = Path.Combine(Root, d.StoredPath);
            if (!File.Exists(full))
            {
                d.Status = "failed";
                d.Error = "파일이 디스크에 없습니다.";
                continue;
            }

            var text = DocumentText.Extract(full, d.FileName, out var why);
            if (text.Length == 0)
            {
                d.Status = "failed";
                d.Error = why ?? "글자를 뽑지 못했습니다.";
                continue;
            }

            var mine = MarkdownChunker.Split($"{d.Id}:{d.FileName}", text);
            chunks.AddRange(mine);
            d.ChunkCount = mine.Count;
            d.Status = "indexed";
            d.Error = null;
        }

        var dims = 0;
        if (chunks.Count > 0)
        {
            // 임베딩 서버가 CPU 로 돈다. 한 번에 몰아 보내면 타임아웃이 난다.
            const int Batch = 16;
            for (var i = 0; i < chunks.Count; i += Batch)
            {
                var slice = chunks.Skip(i).Take(Batch).ToList();
                var vectors = await _embed.EmbedAsync(
                    [.. slice.Select(MarkdownChunker.ForEmbedding)], ct);

                for (var j = 0; j < slice.Count; j++) slice[j].Vector = vectors[j];
                dims = vectors[0].Length;
            }
        }

        var index = new RagIndexFile
        {
            Model = _embed.Model,
            Dimensions = dims,
            BuiltAt = DateTime.UtcNow,
            Chunks = chunks,
        };

        _store.Replace(categoryId, index);
        await _store.SaveAsync(categoryId, ct);

        var cat = await _db.RagCategories.FirstOrDefaultAsync(x => x.Id == categoryId, ct);
        if (cat is not null)
        {
            cat.IndexedAt = index.BuiltAt;
            cat.ChunkCount = chunks.Count;
            cat.DocCount = docs.Count(d => d.Status == "indexed");
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "RAG 카테고리 색인: {Category} — 문서 {Docs}건 → 토막 {Chunks}개",
            categoryId, docs.Count, chunks.Count);

        return index;
    }

    // ── 거들기 ──────────────────────────────────────────────────

    private void TryDeleteFile(string relative)
    {
        try
        {
            var full = Path.Combine(Root, relative);
            if (File.Exists(full)) File.Delete(full);
        }
        catch (Exception ex)
        {
            // 디스크에서 못 지워도 표에서는 지운다. 남은 파일은 고아가 될 뿐이다.
            _logger.LogWarning(ex, "문서 파일을 지우지 못했습니다: {Path}", relative);
        }
    }

    /// <summary>
    /// 카테고리 열쇠를 다듬는다. <b>ASCII 만 받는다.</b>
    /// </summary>
    /// <remarks>
    /// 한글도 <c>char.IsLetterOrDigit</c> 로는 글자라 그냥 통과하는데, 이 값이
    /// <b>색인 파일 이름과 디스크 폴더 이름</b>이 된다. 거기서 다시 걸러지면
    /// 서로 다른 카테고리가 같은 파일을 가리키게 된다. 이름은 <c>Name</c> 에
    /// 한글로 적고 열쇠는 ASCII 로 둔다.
    /// </remarks>
    private static string Slug(string s)
        => string.Concat((s ?? string.Empty).Trim().ToLowerInvariant()
            .Select(c => (c is >= 'a' and <= 'z') || (c is >= '0' and <= '9') || c is '-' or '_'
                ? c : '-')).Trim('-');
}
