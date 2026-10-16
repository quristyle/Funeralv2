namespace AIAgentServer.Services.Rag;

/// <summary>
/// 문서를 읽어 토막 내고 벡터로 바꿔 색인을 만든다.
/// </summary>
/// <remarks>
/// <b>무엇을 색인하는지는 설정이 정한다</b>(<c>Rag:Sources</c>). 지금은
/// <c>docs/*.md</c> 와 CLAUDE.md 둘뿐이다 — 소스 코드(1,400여 개 .cs)까지 한꺼번에
/// 넣으면 검색 품질을 고칠 때 무엇이 원인인지 가릴 수 없다. 작게 시작해 값어치를
/// 확인하고 늘린다.
/// </remarks>
public sealed class RagIndexer
{
    private readonly EmbeddingClient _embed;
    private readonly RagStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<RagIndexer> _logger;

    public RagIndexer(EmbeddingClient embed, RagStore store, IConfiguration config, ILogger<RagIndexer> logger)
    {
        _embed = embed;
        _store = store;
        _config = config;
        _logger = logger;
    }

    /// <summary>저장소 뿌리. 설정이 없으면 위로 올라가며 찾는다.</summary>
    public string ResolveRoot()
    {
        if (_config["Rag:RepositoryRoot"] is { Length: > 0 } configured && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "docs"))
                && File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// 저장소 문서가 들어가는 카테고리. 사람이 올린 문서와 섞이지 않게 따로 둔다.
    /// </summary>
    public string Category => _config["Rag:RepoCategory"] is { Length: > 0 } c ? c : "repo";

    private string[] Sources()
        => _config.GetSection("Rag:Sources").Get<string[]>() ?? ["docs", "CLAUDE.md", "web/CLAUDE.md"];

    public List<string> CollectFiles(string root)
    {
        var files = new List<string>();
        foreach (var rel in Sources())
        {
            var full = Path.Combine(root, rel);
            if (Directory.Exists(full))
            {
                files.AddRange(Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories));
            }
            else if (File.Exists(full))
            {
                files.Add(full);
            }
        }

        return [.. files.Distinct().OrderBy(x => x, StringComparer.Ordinal)];
    }

    public async Task<RagIndexFile> BuildAsync(CancellationToken ct = default)
    {
        var root = ResolveRoot();
        var files = CollectFiles(root);

        var chunks = new List<RagChunk>();
        foreach (var f in files)
        {
            var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
            chunks.AddRange(MarkdownChunker.Split(rel, await File.ReadAllTextAsync(f, ct)));
        }

        _logger.LogInformation("RAG 색인: 파일 {Files}개 → 토막 {Chunks}개", files.Count, chunks.Count);

        // 임베딩 서버가 CPU 로 돌고 있다. 한 번에 몰아 보내면 타임아웃이 난다.
        const int Batch = 16;
        var dims = 0;
        for (var i = 0; i < chunks.Count; i += Batch)
        {
            var slice = chunks.Skip(i).Take(Batch).ToList();
            var vectors = await _embed.EmbedAsync(
                [.. slice.Select(MarkdownChunker.ForEmbedding)], ct);

            for (var j = 0; j < slice.Count; j++)
            {
                slice[j].Vector = vectors[j];
            }

            dims = vectors[0].Length;
        }

        var index = new RagIndexFile
        {
            Model = _embed.Model,
            Dimensions = dims,
            BuiltAt = DateTime.UtcNow,
            Chunks = chunks,
        };

        _store.Replace(Category, index);
        await _store.SaveAsync(Category, ct);
        return index;
    }
}
