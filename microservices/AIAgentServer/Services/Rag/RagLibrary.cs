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
    private readonly FileServerClient _files;
    private readonly RagStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<RagLibrary> _logger;

    public RagLibrary(
        AiUsageDbContext db,
        EmbeddingClient embed,
        FileServerClient files,
        RagStore store,
        IConfiguration config,
        ILogger<RagLibrary> logger)
    {
        _db = db;
        _embed = embed;
        _files = files;
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

    /// <summary>카테고리마다 지금 올라와 있는 문서 수를 센다.</summary>
    /// <remarks>
    /// <b><c>RagCategoryRow.DocCount</c> 로 대신할 수 없다.</b> 그 칸은 색인을
    /// 만들 때 찍어 두는 사진이라, 파일을 올린 직후에는 **0 으로 남는다** —
    /// 올린 사람은 목록에서 제 파일을 보면서 머리의 숫자는 0 인 것을 본다.
    /// </remarks>
    public async Task<Dictionary<string, RagDocTally>> CategoryTalliesAsync(
        CancellationToken ct = default)
    {
        var rows = await _db.RagDocuments.AsNoTracking()
            .GroupBy(x => new { x.CategoryId, x.Status })
            .Select(g => new { g.Key.CategoryId, g.Key.Status, N = g.Count() })
            .ToListAsync(ct);

        var map = new Dictionary<string, RagDocTally>();
        foreach (var r in rows)
        {
            var t = map.TryGetValue(r.CategoryId, out var cur) ? cur : new RagDocTally();
            map[r.CategoryId] = t with
            {
                Total = t.Total + r.N,
                Indexed = t.Indexed + (r.Status == "indexed" ? r.N : 0),
                Skipped = t.Skipped + (r.Status is "skipped" or "failed" ? r.N : 0),
            };
        }
        return map;
    }

    /// <summary>분류마다 **그 분류에 바로 든** 문서를 상태별로 센 것. 하위 분류는 안 센다.</summary>
    /// <remarks>
    /// 분류 미지정(<c>FolderId</c> 가 <c>null</c>)은 여기 들어오지 않는다 —
    /// 카테고리 합계에서 이 값들을 빼면 그 수다.
    /// </remarks>
    public async Task<Dictionary<long, RagDocTally>> FolderTalliesAsync(
        string categoryId, CancellationToken ct = default)
    {
        var rows = await _db.RagDocuments.AsNoTracking()
            .Where(x => x.CategoryId == categoryId && x.FolderId != null)
            .GroupBy(x => new { Id = x.FolderId!.Value, x.Status })
            .Select(g => new { g.Key.Id, g.Key.Status, N = g.Count() })
            .ToListAsync(ct);

        var map = new Dictionary<long, RagDocTally>();
        foreach (var r in rows)
        {
            var t = map.TryGetValue(r.Id, out var cur) ? cur : new RagDocTally();
            map[r.Id] = t with
            {
                Total = t.Total + r.N,
                Indexed = t.Indexed + (r.Status == "indexed" ? r.N : 0),
                Skipped = t.Skipped + (r.Status is "skipped" or "failed" ? r.N : 0),
            };
        }
        return map;
    }

    /// <summary>
    /// 카테고리별 문서 실태. <b>상태 화면(「LLM 장비 상태」)이 읽는다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>카테고리 표에 적힌 수(<c>DocCount</c>·<c>ChunkCount</c>)를 쓰지 않는다.</b>
    /// 그 값은 마지막 색인 때 찍힌 것이라, 그 뒤에 올린 문서는 거기 없다 —
    /// 「올려만 놓고 색인을 안 돌린」 상태가 화면에서 보여야 하는데 그 숫자로는
    /// 안 보인다. 문서 표를 그때그때 센다.
    /// </para>
    /// <para>
    /// 바로 위 <see cref="CategoryTalliesAsync"/> 와 같은 표를 세지만 <b>묶는
    /// 자리가 다르다</b> — 저쪽은 문서함 머리에 적을 「전체·색인됨·건너뜀」이고,
    /// 이쪽은 상태 화면이 짚어야 하는 <b>색인 안 된 것(<c>pending</c>)</b>과
    /// 차지한 바이트까지 따로 센다. 합치려면 저쪽 칸을 늘려야 하는데, 그러면
    /// 문서함 머리가 쓰지도 않는 값을 들고 다니게 된다.
    /// </para>
    /// </remarks>
    public async Task<List<RagDocStat>> DocumentStatsAsync(CancellationToken ct = default)
    {
        var rows = await _db.RagDocuments.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => new { x.CategoryId, x.Status })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.Status,
                Count = g.Count(),
                Bytes = g.Sum(x => x.SizeBytes),
            })
            .ToListAsync(ct);

        return [.. rows
            .GroupBy(x => x.CategoryId, StringComparer.Ordinal)
            .Select(g => new RagDocStat(
                CategoryId: g.Key,
                Total: g.Sum(x => x.Count),
                Indexed: g.Where(x => x.Status == "indexed").Sum(x => x.Count),
                Pending: g.Where(x => x.Status == "pending").Sum(x => x.Count),
                Failed: g.Where(x => x.Status == "failed").Sum(x => x.Count),
                Skipped: g.Where(x => x.Status == "skipped").Sum(x => x.Count),
                Bytes: g.Sum(x => x.Bytes)))];
    }

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
            await DropFileAsync(d, ct);
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
    /// 분류를 다른 부모 아래로 옮기거나 형제 사이 차례를 바꾼다.
    /// </summary>
    /// <param name="parentId">새 부모. null 이면 카테고리 바로 아래(최상위).</param>
    /// <param name="beforeId">
    /// 이 분류 <b>앞에</b> 놓는다. null 이면 맨 뒤.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>색인에는 아무 영향이 없다.</b> 색인은 카테고리 단위이고
    /// (<see cref="ReindexAsync"/> 가 <c>CategoryId</c> 로만 추린다) 토막의 출처도
    /// <c>문서id:파일이름</c> 이라 분류가 들어가지 않는다. 옮긴 뒤 다시 색인할
    /// 까닭이 없다.
    /// </para>
    /// <para>
    /// <b>막는 것 둘.</b> 카테고리를 넘나드는 이동은 막는다 — 그것은 색인이
    /// 갈리는 일이라 조용히 넘기면 안 된다. 그리고 자기 자손 아래로 옮기는
    /// 것도 막는다 — 트리가 고리가 되어 화면이 무한히 그려진다.
    /// </para>
    /// </remarks>
    public async Task MoveFolderAsync(
        long id, long? parentId, long? beforeId, string? who, CancellationToken ct = default)
    {
        var row = await _db.RagFolders.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("없는 분류입니다.");

        var all = await _db.RagFolders
            .Where(x => x.CategoryId == row.CategoryId && !x.IsDeleted)
            .ToListAsync(ct);

        if (parentId is { } pid)
        {
            var parent = all.FirstOrDefault(x => x.Id == pid)
                         ?? throw new InvalidOperationException("옮길 자리가 같은 카테고리에 없습니다.");

            if (pid == id || IsDescendant(all, id, parent.Id))
            {
                throw new InvalidOperationException("자기 자신이나 그 아래로는 옮길 수 없습니다.");
            }
        }

        row.ParentId = parentId;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = who;

        // 새 부모의 형제들을 통째로 다시 번호 매긴다. 빈틈이나 같은 번호가
        // 생기지 않게 하는 가장 단순한 길이다 — 형제가 수십을 넘지 않는다.
        var siblings = all
            .Where(x => x.Id != id && x.ParentId == parentId)
            .OrderBy(x => x.SortNo).ThenBy(x => x.Name)
            .ToList();

        var at = beforeId is { } b ? siblings.FindIndex(x => x.Id == b) : -1;
        if (at < 0) siblings.Add(row);
        else siblings.Insert(at, row);

        for (var i = 0; i < siblings.Count; i++) siblings[i].SortNo = i;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary><paramref name="maybeChild"/> 가 <paramref name="ancestorId"/> 의 아래에 있나.</summary>
    private static bool IsDescendant(List<RagFolderRow> all, long ancestorId, long maybeChild)
    {
        var cur = all.FirstOrDefault(x => x.Id == maybeChild);
        var guard = 0;
        while (cur?.ParentId is { } p && guard++ < 64)
        {
            if (p == ancestorId) return true;
            cur = all.FirstOrDefault(x => x.Id == p);
        }

        return false;
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

    /// <summary>문서를 다른 분류로 옮긴다. <paramref name="folderId"/> 가 <c>null</c> 이면 분류 미지정.</summary>
    /// <remarks>
    /// <b>색인에는 아무 영향이 없다.</b> 분류는 사람이 보기 좋으라고 있는 것이고
    /// 검색은 카테고리 전체에서 한다 — 토막에도, 디스크의 자리에도 분류가 적혀
    /// 있지 않다(파일은 <c>{카테고리}/{연월}/{guid}</c> 에 놓인다). 그래서 옮긴
    /// 뒤에 색인을 다시 만들 일이 없다.
    /// </remarks>
    public async Task MoveDocumentAsync(
        long id, long? folderId, string? who, CancellationToken ct = default)
    {
        var row = await _db.RagDocuments.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                  ?? throw new InvalidOperationException("없는 문서입니다.");

        if (folderId is { } f)
        {
            // **카테고리를 넘나들 수 없다.** 넘기면 토막은 옛 카테고리의 색인에
            // 남고 문서만 새 카테고리에 서서, 「목록에는 있는데 그 카테고리에서는
            // 안 찾아지는」 문서가 된다.
            var ok = await _db.RagFolders.AnyAsync(
                x => x.Id == f && x.CategoryId == row.CategoryId && !x.IsDeleted, ct);

            if (!ok) throw new InvalidOperationException("이 카테고리의 분류가 아닙니다.");
        }

        if (row.FolderId == folderId) return;

        // 문서 줄에는 「고친 때」 칸이 없다 — 옮기는 것은 내용을 건드리지 않고,
        // 그 칸 하나 때문에 운영 DDL 을 돌릴 일이 아니다. 자취는 로그에 남긴다.
        row.FolderId = folderId;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "RAG 문서를 옮겼습니다: {Id} → 분류 {Folder} ({Who})",
            id, folderId?.ToString() ?? "미지정", who);
    }

    public async Task<RagDocumentRow> UploadAsync(
        string categoryId, long? folderId, string fileName, string? contentType,
        Stream content, string? who, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName)) throw new ArgumentException("파일 이름이 없습니다.");

        // **받은 것을 먼저 임시 파일에 받아 둔다.** 들어오는 스트림은 한 번만
        // 읽히는데 할 일이 둘이다 — 지문(SHA256)을 뜨고, 파일 서버로 보낸다.
        // 메모리에 들면 50MB 짜리가 그대로 힙에 앉는다.
        var temp = Path.Combine(Path.GetTempPath(), $"rag-{Guid.NewGuid():N}.tmp");

        long size;
        string hash;
        Guid fileId;
        try
        {
            await using (var fs = File.Create(temp))
            {
                await content.CopyToAsync(fs, ct);
                size = fs.Length;
            }

            await using (var read = File.OpenRead(temp))
            {
                hash = Convert.ToHexString(await SHA256.HashDataAsync(read, ct)).ToLowerInvariant();
            }

            await using (var read = File.OpenRead(temp))
            {
                fileId = await _files.UploadAsync(read, safeName, contentType, who, ct);
            }
        }
        finally
        {
            TryDeleteTemp(temp);
        }

        var row = new RagDocumentRow
        {
            CategoryId = categoryId,
            FolderId = folderId,
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = size,
            Sha256 = hash,
            FileId = fileId,
            StoredPath = string.Empty,   // 실물은 파일 서버에 있다. 이 칸은 옛 줄만 쓴다.
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

    public async Task<(Stream Content, string Name)?> OpenAsync(long id, CancellationToken ct = default)
    {
        var row = await _db.RagDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return null;

        if (row.FileId is { } fid)
        {
            var stream = await _files.OpenAsync(fid, ct: ct);
            return stream is null ? null : (stream, row.FileName);
        }

        // 아직 안 옮긴 줄. 이사가 끝나면 이 갈래는 안 쓰인다.
        var full = Path.Combine(Root, row.StoredPath);
        return File.Exists(full) ? (File.OpenRead(full), row.FileName) : null;
    }

    public async Task DeleteDocumentAsync(long id, CancellationToken ct = default)
    {
        var row = await _db.RagDocuments.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("없는 문서입니다.");

        await DropFileAsync(row, ct);
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

            // **글자를 뽑는 쪽은 길을 받는다**(PdfPig 이 그렇다). 파일 서버에
            // 있는 것은 임시 파일로 한 번 내려 받아 넘기고 바로 치운다.
            var (full, temp) = await MaterializeAsync(d, ct);
            if (full is null)
            {
                d.Status = "failed";
                d.Error = "파일을 찾지 못했습니다.";
                continue;
            }

            string text;
            string? why;
            try
            {
                text = DocumentText.Extract(full, d.FileName, out why);
            }
            finally
            {
                if (temp) TryDeleteTemp(full);
            }

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

    /// <summary>실물을 치운다. 옮긴 줄은 파일 서버에, 안 옮긴 줄은 디스크에 있다.</summary>
    private async Task DropFileAsync(RagDocumentRow row, CancellationToken ct)
    {
        if (row.FileId is { } fid) await _files.DeleteAsync(fid, ct);
        else TryDeleteFile(row.StoredPath);
    }

    /// <summary>
    /// 글자를 뽑을 수 있게 <b>길 하나</b>로 만들어 준다.
    /// </summary>
    /// <returns>
    /// 길과 「임시 파일인가」. 임시면 쓴 쪽이 치운다 — 여기서 치우면 돌려주는
    /// 순간 없어진다.
    /// </returns>
    private async Task<(string? Path, bool Temp)> MaterializeAsync(
        RagDocumentRow row, CancellationToken ct)
    {
        if (row.FileId is not { } fid)
        {
            var legacy = Path.Combine(Root, row.StoredPath);
            return File.Exists(legacy) ? (legacy, false) : (null, false);
        }

        var stream = await _files.OpenAsync(fid, ct: ct);
        if (stream is null) return (null, false);

        var temp = Path.Combine(Path.GetTempPath(),
            $"rag-{Guid.NewGuid():N}{Path.GetExtension(row.FileName)}");

        await using (stream)
        await using (var fs = File.Create(temp))
        {
            await stream.CopyToAsync(fs, ct);
        }

        return (temp, true);
    }

    private void TryDeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            // 임시 파일이 남는 것뿐이다. 하던 일을 멈출 까닭이 없다.
            _logger.LogDebug(ex, "임시 파일을 못 지웠습니다: {Path}", path);
        }
    }

    /// <summary>
    /// 아직 디스크에 있는 문서를 파일 서버로 옮긴다. <b>여러 번 돌려도 된다</b> —
    /// 이미 옮긴 줄(<c>FileId</c> 가 있는 줄)은 건너뛴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>원본을 지우지 않는다.</b> 옮긴 뒤 한동안 멀쩡한 것을 보고 사람이
    /// 지우는 편이 낫다 — 여기서 함께 지우면 파일 서버 쪽이 잘못됐을 때
    /// 되돌릴 바이트가 없다.
    /// </para>
    /// <para>
    /// <b>운영 컨테이너에서만 돌린다.</b> 개발 장비는 운영과 **같은 DB** 를
    /// 보는데(docs 의 개발 환경 설명) 디스크와 파일 서버는 제 것이라,
    /// 개발에서 돌리면 <b>운영 줄에 개발 파일 서버의 아이디가 적힌다</b> —
    /// 그 줄은 운영에서 영영 404 다. 실제로 한 번 그렇게 적었다(시험 문서
    /// 넷이라 지우고 끝냈다). 바이트가 없는 줄은 건너뛰므로 사고가 커지지는
    /// 않지만, <b>개발 장비에 우연히 같은 파일이 남아 있으면 그것이 올라간다.</b>
    /// </para>
    /// </remarks>
    public async Task<(int Moved, int Missing, int Failed)> MigrateToFileServerAsync(
        string? who, CancellationToken ct = default)
    {
        var rows = await _db.RagDocuments
            .Where(x => x.FileId == null && !x.IsDeleted)
            .ToListAsync(ct);

        int moved = 0, missing = 0, failed = 0;

        foreach (var row in rows)
        {
            var full = Path.Combine(Root, row.StoredPath);
            if (string.IsNullOrWhiteSpace(row.StoredPath) || !File.Exists(full))
            {
                missing++;
                _logger.LogWarning(
                    "RAG 이사: {Id} ({Name}) 의 바이트가 디스크에 없습니다.", row.Id, row.FileName);
                continue;
            }

            try
            {
                // **올리기 전에 지문을 맞춰 본다.** 디스크에 같은 이름의 다른
                // 바이트가 있을 수 있다(환경을 잘못 짚은 경우가 그렇다).
                // 어긋나면 옮기지 않는다 — 엉뚱한 파일에 아이디를 달아 두면
                // 겉보기에는 멀쩡하고 내용만 다르다.
                if (!string.IsNullOrWhiteSpace(row.Sha256))
                {
                    await using var probe = File.OpenRead(full);
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(probe, ct))
                        .ToLowerInvariant();

                    if (!string.Equals(hash, row.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        failed++;
                        _logger.LogError(
                            "RAG 이사: {Id} ({Name}) 의 지문이 다릅니다. 옮기지 않습니다.",
                            row.Id, row.FileName);
                        continue;
                    }
                }

                await using var read = File.OpenRead(full);
                row.FileId = await _files.UploadAsync(
                    read, row.FileName, row.ContentType, who ?? row.CreatedBy, ct);

                await _db.SaveChangesAsync(ct);
                moved++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "RAG 이사: {Id} ({Name}) 를 옮기지 못했습니다.", row.Id, row.FileName);
            }
        }

        _logger.LogInformation(
            "RAG 이사 끝 — 옮김 {Moved} · 바이트 없음 {Missing} · 실패 {Failed} ({Who})",
            moved, missing, failed, who);

        return (moved, missing, failed);
    }

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

/// <summary>카테고리 하나에 올라와 있는 문서를 상태별로 센 것.</summary>
/// <param name="Total">올라와 있는 문서 전부.</param>
/// <param name="Indexed">색인에 들어간 것.</param>
/// <param name="Skipped">읽을 수 없어 건너뛴 것과 실패한 것.</param>
public readonly record struct RagDocTally(int Total = 0, int Indexed = 0, int Skipped = 0)
{
    /// <summary>아직 색인에 안 들어간 것. <b>올린 직후의 그 수다.</b></summary>
    public int Pending => Total - Indexed - Skipped;
}
