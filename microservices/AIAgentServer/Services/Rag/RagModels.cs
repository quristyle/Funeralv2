namespace AIAgentServer.Services.Rag;

/// <summary>색인에 들어가는 글 토막 하나. 출처를 반드시 함께 들고 다닌다.</summary>
public sealed class RagChunk
{
    public required string Id { get; init; }

    /// <summary>저장소 기준 상대 경로. <c>docs/utc-time.md</c> 같은 모양이다.</summary>
    public required string Source { get; init; }

    /// <summary>제목 경로. <c>## 규칙 &gt; ### 시각</c> 처럼 이어 붙인다.</summary>
    public required string Heading { get; init; }

    public required string Text { get; init; }

    /// <summary>임베딩. 색인 파일에 그대로 실린다.</summary>
    public float[] Vector { get; set; } = [];

    /// <summary>키워드 점수를 매길 때 쓰는, 미리 쪼개 둔 낱말.</summary>
    public string[] Terms { get; set; } = [];
}

/// <summary>대화에서 사내 문서를 어떻게 쓸지.</summary>
public enum RagMode
{
    /// <summary>잘 맞는 문서가 있을 때만 끼우고, 없으면 웹과 모델이 아는 것으로 답한다.</summary>
    Auto,

    /// <summary>사내 문서로만 답한다. 점수 문턱을 안 두고 웹도 안 찾는다.</summary>
    Only,

    /// <summary>사내 문서를 보지 않는다.</summary>
    Off,
}

/// <summary>대화에서 사내 자료를 찾을 범위. 사람이 채팅창에서 고른다.</summary>
/// <param name="Categories">
/// 찾을 카테고리들. null 이면 모든 카테고리. <b>여럿인 것은 업무 비서 때문이다</b> —
/// 거기서는 사람이 카테고리가 아니라 프로젝트를 고르고, 프로젝트 하나에 카테고리가
/// 여럿 매여 있다(projmng.dev_proj_rag).
/// </param>
/// <param name="DocIds">
/// 분류를 골랐을 때 그 분류(하위 포함)에 든 문서 아이디. null 이면 카테고리 전체.
/// 분류는 카테고리 하나 안의 나무라 카테고리가 여럿일 때는 쓰지 않는다.
/// </param>
/// <param name="Label">안내에 보여 줄 이름(「카테고리 › 분류」· 「프로젝트 둘」).</param>
public sealed record RagScope(IReadOnlyList<string>? Categories, IReadOnlySet<long>? DocIds, string Label);

/// <summary>검색 결과 한 건.</summary>
public sealed record RagHit(RagChunk Chunk, double Score, double VectorScore, double KeywordScore);

/// <summary>색인 파일의 겉모양.</summary>
public sealed class RagIndexFile
{
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public DateTime BuiltAt { get; set; }
    public List<RagChunk> Chunks { get; set; } = [];
}

/// <summary>카테고리 하나의 색인 상태. <b>상태 화면이 읽는 것이고 검색에는 쓰지 않는다.</b></summary>
/// <param name="Chunks">색인에 든 토막 수.</param>
/// <param name="VectorChunks">
/// 그 가운데 <b>벡터가 붙은</b> 토막 수. 토막 수와 다르면 임베딩이 빠진 채
/// 색인이 만들어졌다는 뜻이고, 그 카테고리는 낱말 검색밖에 못 한다.
/// </param>
/// <param name="Sources">토막이 나온 문서 수.</param>
/// <param name="Dimensions">벡터 차원. 임베딩 모델이 바뀌면 함께 바뀐다.</param>
/// <param name="Model">색인을 만들 때 쓴 임베딩 모델 이름.</param>
/// <param name="BuiltAt">색인을 만든 때(UTC).</param>
/// <param name="FileBytes">디스크에 저장된 색인 파일 크기. 저장 전이면 null.</param>
public sealed record RagIndexStat(
    int Chunks,
    int VectorChunks,
    int Sources,
    int Dimensions,
    string Model,
    DateTime BuiltAt,
    long? FileBytes);

/// <summary>카테고리 하나에 올라와 있는 문서의 실태.</summary>
/// <param name="Pending">
/// 올렸지만 <b>아직 색인에 안 들어간</b> 문서. 0 이 아니면 「색인 다시 만들기」를
/// 눌러야 한다 — 올릴 때마다 돌리지 않기 때문에 생기는 정상적인 중간 상태다.
/// </param>
/// <param name="Failed">읽다가 깨진 문서. 까닭은 문서함 화면에 적혀 있다.</param>
/// <param name="Skipped">읽을 수 없는 형식이거나 내용이 겹쳐 건너뛴 문서.</param>
public sealed record RagDocStat(
    string CategoryId,
    int Total,
    int Indexed,
    int Pending,
    int Failed,
    int Skipped,
    long Bytes);
