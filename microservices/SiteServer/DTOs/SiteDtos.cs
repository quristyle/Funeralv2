namespace SiteServer.DTOs;

/// <summary>공개 조회용 문구 블록</summary>
public class SectionDto
{
    public string SectionKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Body { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>공개 조회용 글 목록 항목</summary>
public class PostListItemDto
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public Guid? CoverFileId { get; set; }

    /// <summary>대표 이미지 주소. 비어 있으면 이미지가 없다는 뜻이다</summary>
    public string? CoverUrl => CoverFileId is null ? null : $"/api/file/medium/{CoverFileId}";

    public DateTime? PublishedAt { get; set; }
}

/// <summary>공개 조회용 글 상세</summary>
public class PostDetailDto : PostListItemDto
{
    public string? Body { get; set; }
}

/// <summary>공개 조회용 자료실 항목</summary>
public class DownloadDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public int DownloadCount { get; set; }

    /// <summary>
    /// 내려받기 주소. FileServer 를 직접 가리키지 않는다 —
    /// 이 주소로 오면 횟수를 세고 FileServer 로 넘긴다.
    /// </summary>
    public string DownloadUrl => $"/api/site/downloads/{Id}/file";
}

/// <summary>문의 접수 요청</summary>
public class InquiryRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>언어. <c>ko</c> · <c>en</c> 외의 값은 <c>ko</c> 로 본다</summary>
    public string? Locale { get; set; }

    /// <summary>
    /// 개인정보 수집·이용 동의. **false 면 접수하지 않는다.**
    /// 동의 문구 자체는 회사가 확정해야 한다 (D-S7).
    /// </summary>
    public bool Consent { get; set; }

    /// <summary>
    /// 허니팟. 사람에게는 보이지 않는 칸이라 **비어 있어야 정상**이다.
    /// 채워져 있으면 폼을 기계가 훑은 것으로 보고 조용히 버린다.
    /// 화면에서는 `aria-hidden` 과 화면 밖 배치로 감춘다 — `display:none` 은 봇도 알아본다.
    /// </summary>
    public string? Website { get; set; }
}

/// <summary>관리(포털)용 문의 목록 항목. 본문과 내부 메모까지 함께 준다</summary>
public class InquiryAdminDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Locale { get; set; } = "ko";
    public string Status { get; set; } = "new";
    public string? InternalNote { get; set; }
    public string? ClientIp { get; set; }
    public DateTime ConsentedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
