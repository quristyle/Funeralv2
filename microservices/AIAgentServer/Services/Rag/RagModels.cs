namespace AIAgentServer.Services.Rag;

/// <summary>
/// 색인에 들어가는 글 토막 하나.
/// </summary>
/// <remarks>
/// <b>출처를 반드시 함께 들고 다닌다.</b> RAG 의 값어치 절반이 "어느 문서에서
/// 나온 말인가" 를 댈 수 있다는 데 있다. 틀린 답이 나와도 어디를 고치면 되는지
/// 바로 알 수 있고, 모델이 지어낸 것인지도 가려진다.
/// </remarks>
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
