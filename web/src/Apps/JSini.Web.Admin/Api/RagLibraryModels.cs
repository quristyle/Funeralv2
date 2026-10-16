namespace JSini.Web.Admin.Api;

/// <summary>RAG 단위 하나. 색인 파일 하나에 대응한다.</summary>
public sealed class RagCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? IndexedAt { get; set; }

    /// <summary>
    /// 색인을 만들 때 표에 찍어 둔 문서 수. <b>올린 직후에는 안 바뀐다</b> —
    /// 지금 몇 건이 올라와 있는지는 <see cref="DocTotal"/> 이다.
    /// </summary>
    public int DocCount { get; set; }

    /// <summary>지금 올라와 있는 문서 전부. 서버가 셀 때마다 센 값이다.</summary>
    public int DocTotal { get; set; }

    /// <summary>그중 색인에 들어간 것.</summary>
    public int DocIndexed { get; set; }

    /// <summary>아직 색인에 안 들어간 것. <b>「색인 다시 만들기」가 데려갈 수다.</b></summary>
    public int DocPending { get; set; }

    /// <summary>읽을 수 없어 건너뛴 것과 실패한 것. 색인을 다시 만들어도 안 들어간다.</summary>
    public int DocSkipped { get; set; }

    /// <summary>
    /// <b>지금 메모리에 올라와 있는</b> 토막 수. 표에 적힌 값이 아니다 —
    /// 둘이 다르면 색인을 만든 뒤 서비스가 다시 뜨지 않은 것이다.
    /// </summary>
    public int ChunkCount { get; set; }
}

/// <summary>분류 한 칸. 카테고리 안의 트리다.</summary>
public sealed class RagFolderDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortNo { get; set; }

    /// <summary>이 분류에 <b>바로</b> 든 문서 수. 하위 분류의 것은 안 센다.</summary>
    public int DocCount { get; set; }

    /// <summary>그중 아직 색인에 안 들어간 것.</summary>
    public int DocPending { get; set; }
}

/// <summary>올려 둔 문서 한 건.</summary>
public sealed class RagDocumentDto
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }

    /// <summary><c>pending</c> · <c>indexed</c> · <c>failed</c> · <c>skipped</c></summary>
    public string Status { get; set; } = string.Empty;

    public int ChunkCount { get; set; }

    /// <summary>색인에 못 들어간 까닭. <b>조용히 넘기지 않는다.</b></summary>
    public string? Error { get; set; }

    public DateTime? IndexedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>올리기 결과 한 건.</summary>
public sealed class RagUploadedDto
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
}

/// <summary>색인을 다시 만든 결과.</summary>
public sealed class RagReindexDto
{
    public string Category { get; set; } = string.Empty;
    public int Chunks { get; set; }
    public int Dimensions { get; set; }
    public DateTime BuiltAt { get; set; }
}

/// <summary>화면이 그리는 트리 한 칸. 분류를 부모-자식으로 엮은 것이다.</summary>
public sealed class RagFolderNode
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<RagFolderNode> Children { get; } = [];

    /// <summary>이 분류에 바로 든 문서 수.</summary>
    public int DocCount { get; set; }

    /// <summary>그중 아직 색인에 안 들어간 것.</summary>
    public int DocPending { get; set; }

    /// <summary>
    /// 하위 분류에 든 문서 수를 모두 더한 것.
    /// <para>
    /// 이것을 따로 보이는 까닭 — 묶음 노릇만 하는 분류는 <see cref="DocCount"/>
    /// 가 0 이다. 그 값만 적으면 **아래에 수십 건이 든 분류가 「0」으로 보여**
    /// 비어 있는 것으로 읽힌다.
    /// </para>
    /// </summary>
    public int DescendantCount => Children.Sum(c => c.DocCount + c.DescendantCount);

    /// <summary>
    /// 이 분류와 그 아래에서 아직 색인에 안 들어간 문서 수.
    /// <para>
    /// 하위까지 더하는 까닭 — 접지 않는 트리라 눈에는 다 보이지만, 가지가
    /// 길면 <b>어느 가지를 펴 봐야 하는지</b>는 위에서 알아야 한다.
    /// </para>
    /// </summary>
    public int PendingTotal => DocPending + Children.Sum(c => c.PendingTotal);
}

/// <summary>올리는 중인 파일 하나의 상태. 화면이 진행바를 그리는 자료다.</summary>
public sealed class RagUploadItem
{
    public required string FileName { get; init; }
    public required long TotalBytes { get; init; }

    /// <summary>지금까지 보낸 바이트.</summary>
    public long SentBytes { get; set; }

    /// <summary><c>waiting</c> · <c>sending</c> · <c>done</c> · <c>failed</c></summary>
    public string State { get; set; } = "waiting";

    /// <summary>서버가 매긴 상태(<c>pending</c>·<c>skipped</c>). 다 보낸 뒤에 채워진다.</summary>
    public string? ServerStatus { get; set; }

    public string? Error { get; set; }

    public double Percent => TotalBytes <= 0
        ? 0
        : Math.Clamp(100.0 * SentBytes / TotalBytes, 0, 100);
}
