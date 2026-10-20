using System.Security.Cryptography;
using AIAgentServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services.Rag;

/// <summary>RAG 문서를 보관하고 색인한다. 파일 실물은 운영 디스크에 있고 LLM 서버에는 임베딩만 맡긴다. 색인은 카테고리마다 하나다 — 검색은 카테고리 전체에서 하고, 분류는 사람이 정리하려고 있다.</summary>
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
        => CategoriesAsync(companyId: null, ct);

    public Task<List<RagCategoryRow>> CategoriesAsync(string? companyId, CancellationToken ct = default)
    {
        var q = _db.RagCategories.AsNoTracking()
            .Include(x => x.Company)
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(companyId))
        {
            q = q.Where(x => x.CompanyId == companyId);
        }

        return q.OrderBy(x => x.Name).ToListAsync(ct);
    }

    /// <summary>카테고리마다 올라와 있는 문서 수를 센다. RagCategoryRow.DocCount 는 색인 때 찍힌 사진이라 방금 올린 문서는 0 으로 남으므로 쓰지 않는다.</summary>
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

    /// <summary>분류마다 그 분류에 바로 든 문서를 상태별로 센다(하위 분류는 안 센다). 분류 미지정은 여기 들어오지 않는다.</summary>
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

    /// <summary>카테고리별 문서 실태(상태 화면이 읽는다). 카테고리 표의 DocCount 는 쓰지 않고 문서 표를 그때그때 센다 — 올려만 두고 색인을 안 돌린 상태가 보여야 하므로.</summary>
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
        string id, string name, string? description, string? companyId, string? who, CancellationToken ct = default)
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
            CompanyId = string.IsNullOrWhiteSpace(companyId) ? null : companyId.Trim(),
            Name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
            Description = description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = who,
        };
        _db.RagCategories.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public Task<RagCategoryRow?> GetCategoryAsync(string id, CancellationToken ct = default)
        => _db.RagCategories.AsNoTracking()
            .Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

    /// <summary>카테고리 접근 권한을 확인한다. 관리자는 모든 카테고리에 접근 가능하고, 일반 사용자는 자신의 소속회사 카테고리만 제어할 수 있다.</summary>
    public async Task<RagCategoryRow> EnsureCategoryAccessAsync(
        string categoryId, UserContext? user, CancellationToken ct = default)
    {
        var category = await _db.RagCategories.FirstOrDefaultAsync(x => x.Id == categoryId && !x.IsDeleted, ct)
                       ?? throw new InvalidOperationException($"없는 카테고리입니다: {categoryId}");

        if (user is not null && !user.IsAdmin)
        {
            if (string.IsNullOrWhiteSpace(user.CompanyId) || category.CompanyId != user.CompanyId)
            {
                throw new UnauthorizedAccessException("소속 회사의 카테고리만 관리하고 제어할 수 있습니다.");
            }
        }

        return category;
    }

    /// <summary>폴더(분류)가 속한 카테고리에 대한 접근 권한을 확인한다.</summary>
    public async Task EnsureFolderAccessAsync(long folderId, UserContext? user, CancellationToken ct = default)
    {
        if (user is null || user.IsAdmin) return;
        var folder = await _db.RagFolders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == folderId, ct)
                     ?? throw new InvalidOperationException("없는 분류입니다.");
        await EnsureCategoryAccessAsync(folder.CategoryId, user, ct);
    }

    /// <summary>문서가 속한 카테고리에 대한 접근 권한을 확인한다.</summary>
    public async Task EnsureDocumentAccessAsync(long docId, UserContext? user, CancellationToken ct = default)
    {
        if (user is null || user.IsAdmin) return;
        var doc = await _db.RagDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == docId, ct)
                  ?? throw new InvalidOperationException("없는 문서입니다.");
        await EnsureCategoryAccessAsync(doc.CategoryId, user, ct);
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

    /// <summary>분류를 다른 부모 아래로 옮기거나 형제 사이 차례를 바꾼다. 색인에는 영향이 없다(색인은 카테고리 단위). 카테고리를 넘나드는 이동과 자기 자손 아래로 옮기는 것은 막는다.</summary>
    /// <param name="parentId">새 부모. null 이면 카테고리 바로 아래(최상위).</param>
    /// <param name="beforeId">이 분류 앞에 놓는다. null 이면 맨 뒤.</param>
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

    /// <summary>분류를 지운다. 아래 분류·폴더·문서는 FK CASCADE 로 함께 지워진다.</summary>
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

    /// <summary>문서를 다른 분류로 옮긴다. folderId 가 null 이면 분류 미지정. 분류는 토막에도 디스크 자리에도 적히지 않으므로 옮긴 뒤 색인을 다시 만들 일이 없다.</summary>
    public async Task MoveDocumentAsync(
        long id, long? folderId, string? who, CancellationToken ct = default)
    {
        var row = await _db.RagDocuments.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                  ?? throw new InvalidOperationException("없는 문서입니다.");

        if (folderId is { } f)
        {
            // 카테고리를 넘나들 수 없다 — 넘기면 토막은 옛 색인에 남고 문서만 새 카테고리에 서서 안 찾아진다.
            var ok = await _db.RagFolders.AnyAsync(
                x => x.Id == f && x.CategoryId == row.CategoryId && !x.IsDeleted, ct);

            if (!ok) throw new InvalidOperationException("이 카테고리의 분류가 아닙니다.");
        }

        if (row.FolderId == folderId) return;

        // 문서 줄에 고친 때 칸이 없다. 옮김은 내용을 안 건드리고, 자취는 로그에 남긴다.
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

        // 들어오는 스트림은 한 번만 읽히므로 임시 파일에 먼저 받는다 — 지문(SHA256)과 파일 서버 전송에 쓰고, 메모리에 두면 50MB 짜리가 힙에 앉는다.
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

    public async Task<string> DeleteDocumentAsync(long id, CancellationToken ct = default)
    {
        var row = await _db.RagDocuments.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("없는 문서입니다.");

        var categoryId = row.CategoryId;
        await DropFileAsync(row, ct);
        _db.RagDocuments.Remove(row);
        await _db.SaveChangesAsync(ct);
        return categoryId;
    }

    // ── 색인 ────────────────────────────────────────────────────

    /// <summary>한 카테고리를 통째로 다시 색인한다. 조금씩 고치지 않는다 — 지금 규모(문서 수십~수백)에서는 다시 만드는 편이 빠르고 틀릴 일이 없다.</summary>
    /// <param name="progress">단계(<c>read</c>·<c>embed</c>·<c>save</c>)와 몇 개 중 몇 개를 했나. 화면이 진행을 그리는 값이다.</param>
    public async Task<RagIndexFile> ReindexAsync(
        string categoryId, Action<string, int, int>? progress = null, CancellationToken ct = default)
    {
        var docs = await _db.RagDocuments
            .Where(x => x.CategoryId == categoryId && !x.IsDeleted)
            .ToListAsync(ct);

        var chunks = new List<RagChunk>();
        var seenHash = new HashSet<string>(StringComparer.Ordinal);

        var read = 0;
        foreach (var d in docs)
        {
            progress?.Invoke("read", read++, docs.Count);
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
                progress?.Invoke("embed", i, chunks.Count);
                var slice = chunks.Skip(i).Take(Batch).ToList();
                var vectors = await _embed.EmbedAsync(
                    [.. slice.Select(MarkdownChunker.ForEmbedding)], ct);

                for (var j = 0; j < slice.Count; j++) slice[j].Vector = vectors[j];
                dims = vectors[0].Length;
            }
        }

        progress?.Invoke("save", chunks.Count, chunks.Count);

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

    /// <summary>글자를 뽑을 수 있게 길 하나로 만든다. 임시 파일이면 쓴 쪽이 치운다(여기서 치우면 돌려주는 순간 없어진다).</summary>
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

    /// <summary>아직 디스크에 있는 문서를 파일 서버로 옮긴다. 여러 번 돌려도 된다(옮긴 줄은 건너뛴다). 원본은 지우지 않는다 — 파일 서버가 잘못됐을 때 되돌릴 바이트가 있어야 한다.
    /// 운영 컨테이너에서만 돈다. 개발 장비는 운영과 같은 DB 를 보므로 개발 파일 서버의 아이디가 운영 줄에 적힌다.</summary>
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
                // 올리기 전에 지문을 맞춘다. 디스크에 같은 이름의 다른 바이트가 있으면(환경을 잘못 짚은 경우) 옮기지 않는다.
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

    /// <summary>카테고리 열쇠를 다듬는다. ASCII 만 받는다 — 열쇠가 색인 파일 이름과 디스크 폴더 이름이 되므로 한글은 Name 에 적고 열쇠는 ASCII 로 둔다.</summary>
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
