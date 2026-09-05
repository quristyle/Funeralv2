namespace JSini.PublicSite.Api;

// SiteServer 공개 API 의 응답 모양. 원본: fronts/apps/jsini-site/src/api/site.ts

/// <summary>DB(site.sections)의 문구 블록 하나</summary>
public sealed class Section
{
    public string SectionKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Body { get; set; }
    public int SortOrder { get; set; }
}

public class PostListItem
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }

    /// <summary>게이트웨이 상대 경로(/api/file/medium/...). 내보낼 때 절대 주소로 바꾼다</summary>
    public string? CoverUrl { get; set; }

    public DateTime? PublishedAt { get; set; }
}

public sealed class PostDetail : PostListItem
{
    public string? Body { get; set; }
}

public sealed class DownloadItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public int DownloadCount { get; set; }

    /// <summary>
    /// 게이트웨이 상대 경로. SiteServer 를 한 번 거친다 — 그쪽이 횟수를 세고
    /// FileServer 로 302 로 넘긴다. FileServer 를 직접 가리키면 셀 수가 없다.
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;
}

public sealed class InquiryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;

    /// <summary>개인정보 수집·이용 동의. false 면 서버가 거절한다</summary>
    public bool Consent { get; set; }

    /// <summary>
    /// 허니팟. 사람에게는 보이지 않는 칸이라 **비어 있어야 정상**이다.
    /// 채워져 있으면 서버가 조용히 버리고 성공 응답을 준다 — 봇에게 단서를 주지 않는다.
    /// </summary>
    public string? Website { get; set; }
}

/// <summary>문의 접수 결과. 실패 이유는 화면 문구를 고를 만큼만 나눈다</summary>
public sealed record InquiryResult(bool Ok, bool RateLimited = false, string? Message = null);
