namespace JSini.Web.Admin.Api;

/// <summary>답의 근거를 대는 두 길의 상태 — 사내 문서 검색(RAG)과 웹 검색(MCP).</summary>
public sealed class AiFeatureStatusDto
{
    public RagStatusDto? Rag { get; set; }
    public WebSearchStatusDto? Web { get; set; }
}

/// <summary>사내 문서 검색 쪽.</summary>
public sealed class RagStatusDto
{
    /// <summary>대화에 자료를 끼우는지. 끄면 검색 화면에서만 쓰인다.</summary>
    public bool AugmentChat { get; set; }

    /// <summary>프롬프트에 넣는 토막 수.</summary>
    public int TopK { get; set; }

    /// <summary>이 점수에 못 미치는 토막은 넣지 않는다.</summary>
    public double MinVectorScore { get; set; }

    /// <summary>대화에서 찾아볼 카테고리. 비어 있으면 전부.</summary>
    public string? ChatCategory { get; set; }

    /// <summary>저장소 문서(docs/*.md)가 들어가는 카테고리 열쇠.</summary>
    public string? RepoCategory { get; set; }

    public string? IndexPath { get; set; }
    public string? UploadRoot { get; set; }

    /// <summary>모든 카테고리의 토막을 합한 수.</summary>
    public int Chunks { get; set; }

    public DateTime? BuiltAt { get; set; }

    public List<RagCategoryStatusDto> Categories { get; set; } = [];

    public EmbeddingStatusDto? Embedding { get; set; }
}

/// <summary>카테고리 한 칸의 색인·문서 실태.</summary>
public sealed class RagCategoryStatusDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }

    /// <summary>문서함(DB)에 있는 카테고리인지. 거짓이면 저장소 문서처럼 색인에만 있는 것이다.</summary>
    public bool FromLibrary { get; set; }

    /// <summary>색인이 메모리에 올라와 있는지.</summary>
    public bool Indexed { get; set; }

    public int Chunks { get; set; }

    /// <summary>그 가운데 벡터가 붙은 토막 수. 토막 수와 다르면 낱말 검색밖에 못 한다 — 임베딩 서버가 죽은 채로 색인을 돌렸다는 뜻이다.</summary>
    public int VectorChunks { get; set; }

    /// <summary>토막이 나온 문서 수.</summary>
    public int Sources { get; set; }

    public int Dimensions { get; set; }
    public string? Model { get; set; }
    public DateTime? BuiltAt { get; set; }
    public long? IndexBytes { get; set; }

    public int Documents { get; set; }
    public int DocumentsIndexed { get; set; }

    /// <summary>0 이 아니면 「색인 다시 만들기」를 눌러야 한다.</summary>
    public int DocumentsPending { get; set; }

    public int DocumentsFailed { get; set; }
    public int DocumentsSkipped { get; set; }
    public long DocumentBytes { get; set; }
}

public sealed class EmbeddingStatusDto
{
    public bool Reachable { get; set; }
    public string? Model { get; set; }

    /// <summary>돌아온 벡터의 차원. 색인에 적힌 것과 다르면 모델이 바뀐 것이다.</summary>
    public int Dimensions { get; set; }

    public int? LatencyMs { get; set; }
    public string? Endpoint { get; set; }
    public string? Message { get; set; }

    /// <summary>언제 눌러 본 값인지. 서버가 2분 동안 재어 둔다.</summary>
    public DateTime CheckedAt { get; set; }
}

/// <summary>웹 검색 쪽.</summary>
public sealed class WebSearchStatusDto
{
    /// <summary>도구를 아예 쓰는지 (<c>Mcp:Enabled</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>보통 대화에서도 웹을 찾는지 (<c>Mcp:AugmentChat</c>). 꺼 두는 것이 기본이다 — 꺼져 있어도 「웹 검색」 화면은 그대로 쓴다.</summary>
    public bool AugmentChat { get; set; }

    public int MaxToolRounds { get; set; }

    /// <summary>사내 문서 점수가 이보다 높으면 웹을 건너뛴다.</summary>
    public double SkipWhenRagScore { get; set; }

    public string? Endpoint { get; set; }

    /// <summary>MCP 규격으로 붙어 도구 목록을 받았는지.</summary>
    public bool Connected { get; set; }

    public List<McpToolDto> Tools { get; set; } = [];

    public McpServerStatusDto? Server { get; set; }
}

public sealed class McpToolDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

/// <summary>MCP 도구 서버 자신의 상태.</summary>
public sealed class McpServerStatusDto
{
    public bool Reachable { get; set; }

    /// <summary>검색 뒷단(duckduckgo · searxng · brave). 그 서버 설정이 정한다.</summary>
    public string? SearchProvider { get; set; }

    public string? Endpoint { get; set; }
    public int? LatencyMs { get; set; }
    public string? Message { get; set; }
    public DateTime CheckedAt { get; set; }
}

public sealed class WebSearchCheckDto
{
    public string? Query { get; set; }
    public bool Ok { get; set; }
    public int Count { get; set; }
    public int LatencyMs { get; set; }
    public List<WebSearchHitDto> Results { get; set; } = [];

    /// <summary>실패했을 때의 까닭, 또는 모양이 바뀌어 못 읽은 원문.</summary>
    public string? Note { get; set; }
}

public sealed class WebSearchHitDto
{
    public string? Title { get; set; }
    public string? Url { get; set; }
}
