using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIAgentServer.Services.Rag;

/// <summary>
/// 색인을 들고 있다가 질문에 맞는 토막을 꺼내 준다.
/// </summary>
/// <remarks>
/// <para>
/// <b>벡터만 쓰면 안 된다.</b> 벡터 검색은 "비슷한 뜻" 은 잘 찾지만
/// <c>DEPLOY_NOTIFY_TOKEN</c> 이나 <c>LlmHostMetricsClient</c> 같은 <b>정확한 이름</b>
/// 은 못 찾는다. 반대로 낱말 검색은 "푸시가 늦어요" 로 "알림 TTL" 문서를 못 찾는다.
/// 둘을 섞어야 둘 다 된다.
/// </para>
/// <para>
/// 점수를 합칠 때 <b>값을 더하지 않고 순위를 더한다</b>(RRF). 코사인 점수와
/// BM25 점수는 자릿수도 분포도 달라서, 정규화해 더하면 가중치를 문서마다 다시
/// 맞춰야 한다. 순위는 그런 사정이 없다.
/// </para>
/// <para>
/// 저장은 JSON 파일 하나다. 지금 대상(<c>docs/*.md</c>)이 400KB 뿐이라 토막이
/// 수백 개이고, 전부 메모리에서 비교해도 1밀리초다. <b>벡터 DB 는 수십만 건부터
/// 의미가 있다</b> — 운영 DB 에 pgvector 를 설치하는 것은 그때 할 일이다.
/// </para>
/// </remarks>
public sealed class RagStore
{
    private static readonly Regex AsciiWord = new(@"[A-Za-z_][A-Za-z0-9_]{1,}", RegexOptions.Compiled);
    /// <summary>한 문서에서 가져올 수 있는 최대 토막 수.</summary>
    private const int MaxPerSource = 2;

    private static readonly Regex Hangul = new(@"[가-힣]{2,}", RegexOptions.Compiled);

    private readonly ILogger<RagStore> _logger;
    private readonly string _path;

    /// <summary>카테고리별 색인. 열쇠는 카테고리 id 다.</summary>
    private readonly Dictionary<string, Shard> _shards = new(StringComparer.Ordinal);

    /// <summary>한 카테고리의 색인과, 그것으로 미리 셈해 둔 낱말 통계.</summary>
    private sealed class Shard
    {
        public RagIndexFile Index { get; init; } = new();
        public Dictionary<string, int> Df { get; set; } = new(StringComparer.Ordinal);
        public double AvgLen { get; set; } = 1;
    }

    public RagStore(IConfiguration config, ILogger<RagStore> logger)
    {
        _logger = logger;
        _path = config["Rag:IndexPath"] is { Length: > 0 } p
            ? p
            : Path.Combine(AppContext.BaseDirectory, "rag-index.json");
    }

    public string IndexPath => _path;

    /// <summary>모든 카테고리의 토막을 합한 수.</summary>
    public int Count => _shards.Values.Sum(x => x.Index.Chunks.Count);

    public DateTime? BuiltAt => _shards.Count == 0
        ? null
        : _shards.Values.Max(x => (DateTime?)x.Index.BuiltAt);

    public string Model => _shards.Values.FirstOrDefault()?.Index.Model ?? string.Empty;

    public IReadOnlyCollection<string> Categories => _shards.Keys;

    public int CountOf(string category)
        => _shards.TryGetValue(category, out var sh) ? sh.Index.Chunks.Count : 0;

    /// <summary>색인 파일이 놓이는 자리. 카테고리마다 파일이 하나다.</summary>
    private string PathOf(string category)
    {
        var dir = Path.GetDirectoryName(_path);
        var safe = string.Concat(category.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        return Path.Combine(dir ?? ".", $"rag-index-{safe}.json");
    }

    // ── 낱말 쪼개기 ─────────────────────────────────────────────

    /// <summary>
    /// 낱말 검색용으로 글을 쪼갠다.
    /// </summary>
    /// <remarks>
    /// 한국어는 형태소 분석기 없이 낱말 경계를 못 가른다. 그래서 <b>글자 두 개씩
    /// 겹쳐 가며(bigram)</b> 쪼갠다 — "빈소배정" 을 "빈소·소배·배정" 으로 보는 식이다.
    /// 조사가 붙어도("빈소를") 앞부분이 겹쳐서 걸린다. 영문·식별자는 통째로 둔다.
    /// </remarks>
    internal static string[] Tokenize(string text)
    {
        var terms = new List<string>();

        foreach (Match m in AsciiWord.Matches(text))
        {
            terms.Add(m.Value.ToLowerInvariant());
        }

        foreach (Match m in Hangul.Matches(text))
        {
            var w = m.Value;
            for (var i = 0; i + 1 < w.Length; i++) terms.Add(w.Substring(i, 2));
        }

        return [.. terms];
    }

    // ── 색인 담기 ───────────────────────────────────────────────

    public void Replace(string category, RagIndexFile index)
    {
        foreach (var c in index.Chunks)
        {
            c.Terms = Tokenize(MarkdownChunker.ForEmbedding(c));
        }

        var sh = new Shard { Index = index };
        Reindex(sh);
        _shards[category] = sh;
    }

    public void Remove(string category)
    {
        _shards.Remove(category);
        var f = PathOf(category);
        if (File.Exists(f)) File.Delete(f);
    }

    private static void Reindex(Shard sh)
    {
        sh.Df = new Dictionary<string, int>(StringComparer.Ordinal);
        double total = 0;

        foreach (var c in sh.Index.Chunks)
        {
            total += c.Terms.Length;
            foreach (var t in c.Terms.Distinct(StringComparer.Ordinal))
            {
                sh.Df[t] = sh.Df.GetValueOrDefault(t) + 1;
            }
        }

        sh.AvgLen = sh.Index.Chunks.Count > 0 ? total / sh.Index.Chunks.Count : 1;
    }

    public async Task SaveAsync(string category, CancellationToken ct = default)
    {
        if (!_shards.TryGetValue(category, out var sh)) return;
        var target = PathOf(category);
        var dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // 낱말은 저장하지 않는다 — 불러올 때 다시 만든다. 파일이 두 배로 커질 일이 없다.
        var slim = new RagIndexFile
        {
            Model = sh.Index.Model,
            Dimensions = sh.Index.Dimensions,
            BuiltAt = sh.Index.BuiltAt,
            Chunks = [.. sh.Index.Chunks.Select(c => new RagChunk
            {
                Id = c.Id, Source = c.Source, Heading = c.Heading, Text = c.Text, Vector = c.Vector,
            })],
        };

        await using var fs = File.Create(target);
        await JsonSerializer.SerializeAsync(fs, slim, cancellationToken: ct);
    }

    /// <summary>
    /// 디스크에 있는 색인 파일을 모두 읽는다. 기동 때 한 번 부른다.
    /// </summary>
    public async Task<int> LoadAllAsync(CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;

        var n = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "rag-index-*.json"))
        {
            var category = Path.GetFileNameWithoutExtension(f)["rag-index-".Length..];
            try
            {
                await using var fs = File.OpenRead(f);
                var loaded = await JsonSerializer.DeserializeAsync<RagIndexFile>(fs, cancellationToken: ct);
                if (loaded is null) continue;

                Replace(category, loaded);
                n++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG 색인을 읽지 못했습니다. path={Path}", f);
            }
        }

        if (n > 0)
        {
            _logger.LogInformation("RAG 색인 {Files}개를 읽었습니다. 토막 {Count}개", n, Count);
        }

        return n;
    }

    // ── 찾기 ────────────────────────────────────────────────────

    /// <param name="queryVector">
    /// 질문의 임베딩. <b>null 이면 낱말 검색만 한다</b> — 임베딩 서버가 죽어도
    /// 검색이 통째로 멈추지는 않게.
    /// </param>
    /// <param name="category">
    /// 찾을 범위. <b>null 이면 모든 카테고리를 함께 본다.</b>
    /// 카테고리를 나눈 까닭이 "섞이면 안 되는 자료" 라, 쓰는 쪽에서 범위를
    /// 좁히는 것이 보통이다.
    /// </param>
    public IReadOnlyList<RagHit> Search(
        string? category, string query, float[]? queryVector, int topK = 5)
    {
        var shards = category is null
            ? _shards.Values.ToList()
            : _shards.TryGetValue(category, out var one) ? [one] : [];

        if (shards.Count == 0) return [];

        // 카테고리마다 점수를 매기고 한 줄로 모은다. BM25 는 카테고리 안의
        // 낱말 통계로 셈해야 맞으므로 합치기 전에 각자 돈다.
        var all = new List<(RagChunk Chunk, double Vec, double Kw)>();
        foreach (var sh in shards)
        {
            var kw = Bm25(sh, query);
            for (var i = 0; i < sh.Index.Chunks.Count; i++)
            {
                var c = sh.Index.Chunks[i];
                all.Add((c, queryVector is null ? 0 : Dot(c.Vector, queryVector), kw[i]));
            }
        }

        var vecRank = queryVector is null
            ? []
            : Rank(all.Select((x, i) => (i, x.Vec)));
        var kwRank = Rank(all.Select((x, i) => (i, x.Kw)));

        const double K = 60;   // RRF 의 완충 상수. 1~2위와 9~10위의 차이를 눌러 준다.
        var fused = new Dictionary<int, double>();
        foreach (var (idx, rank) in vecRank) fused[idx] = fused.GetValueOrDefault(idx) + 1.0 / (K + rank);
        foreach (var (idx, rank) in kwRank) fused[idx] = fused.GetValueOrDefault(idx) + 1.0 / (K + rank);

        // 한 문서가 결과를 독차지하지 못하게 막는다.
        //
        // **긴 문서일수록 토막이 많아서, 내용이 맞아서가 아니라 수가 많아서 이긴다.**
        // 실제로 설계안 하나가 전체 토막의 31% 를 차지해 상위 20개 중 11개를
        // 가져갔고, 정답이 든 짧은 문서가 그 아래로 밀렸다.
        var perSource = new Dictionary<string, int>(StringComparer.Ordinal);
        var picked = new List<RagHit>();

        foreach (var kv in fused.OrderByDescending(x => x.Value))
        {
            if (picked.Count >= topK) break;

            var (chunk, vec, kw) = all[kv.Key];
            var used = perSource.GetValueOrDefault(chunk.Source);
            if (used >= MaxPerSource) continue;

            perSource[chunk.Source] = used + 1;
            picked.Add(new RagHit(chunk, kv.Value, vec, kw));
        }

        // 상한 때문에 자리를 못 채웠으면 남은 것으로 메운다 — 한 문서에만 답이
        // 있는 질문도 있다.
        if (picked.Count < topK)
        {
            var taken = picked.Select(h => h.Chunk.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var kv in fused.OrderByDescending(x => x.Value))
            {
                if (picked.Count >= topK) break;
                var (chunk, vec, kw) = all[kv.Key];
                if (!taken.Add(chunk.Id)) continue;
                picked.Add(new RagHit(chunk, kv.Value, vec, kw));
            }
        }

        return picked;
    }

    /// <summary>점수를 등수로 바꾼다. 0점짜리는 넣지 않는다.</summary>
    private static List<(int Index, int Rank)> Rank(IEnumerable<(int Index, double Score)> scored)
    {
        var rank = 1;
        var outp = new List<(int, int)>();
        foreach (var (i, sc) in scored.Where(x => x.Score > 0).OrderByDescending(x => x.Score))
        {
            outp.Add((i, rank++));
        }

        return outp;
    }

    private static double Dot(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double sum = 0;
        for (var i = 0; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }

    private static double[] Bm25(Shard sh, string query)
    {
        const double K1 = 1.2, B = 0.75;
        var qTerms = Tokenize(query).Distinct(StringComparer.Ordinal).ToArray();
        var scores = new double[sh.Index.Chunks.Count];
        var n = sh.Index.Chunks.Count;

        for (var i = 0; i < n; i++)
        {
            var c = sh.Index.Chunks[i];
            if (c.Terms.Length == 0) continue;

            var tf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var t in c.Terms) tf[t] = tf.GetValueOrDefault(t) + 1;

            double s = 0;
            foreach (var q in qTerms)
            {
                if (!tf.TryGetValue(q, out var f)) continue;
                var df = sh.Df.GetValueOrDefault(q);
                if (df == 0) continue;

                var idf = Math.Log(1 + (n - df + 0.5) / (df + 0.5));
                s += idf * (f * (K1 + 1)) / (f + K1 * (1 - B + B * c.Terms.Length / sh.AvgLen));
            }

            scores[i] = s;
        }

        return scores;
    }
}
