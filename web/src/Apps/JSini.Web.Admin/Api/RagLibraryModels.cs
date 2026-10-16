namespace JSini.Web.Admin.Api;

/// <summary>RAG 단위 하나. 색인 파일 하나에 대응한다.</summary>
public sealed class RagCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? IndexedAt { get; set; }

    /// <summary>색인에 든 문서 수.</summary>
    public int DocCount { get; set; }

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
}
