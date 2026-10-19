using JSini.Shared.Domain;

namespace SiteServer.Entities;

/// <summary>회사 소개 사이트의 문구 블록. 화면의 한 덩어리(히어로·사업영역·연혁 …)가 한 행이다. 문구는 글이라 컬럼을 잘게 나누지 않고 본문(Body)에 맡기며,
/// 언어는 행을 나눠 담는다 — 컬럼을 언어별로 늘리면 언어가 셋일 때 배가 된다.</summary>
public class SiteSection : BaseEntity<Guid>
{
    /// <summary>
    /// 이 블록이 어느 화면의 어느 자리인지 가리키는 열쇠 (예: <c>home.hero</c> · <c>about.history</c>).
    /// 화면 코드가 이 값으로 찾아 쓴다. (Locale) 과 함께 유일하다.
    /// </summary>
    public string SectionKey { get; set; } = string.Empty;

    /// <summary>언어. <c>ko</c> · <c>en</c></summary>
    public string Locale { get; set; } = "ko";

    /// <summary>블록의 큰 제목</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>제목 아래 짧은 한 줄. 없으면 비운다</summary>
    public string? Subtitle { get; set; }

    /// <summary>본문. 마크다운으로 넣는다</summary>
    public string? Body { get; set; }

    /// <summary>같은 화면 안에서의 순서. 작은 값이 위</summary>
    public int SortOrder { get; set; }

    /// <summary>화면에 내보낼지 여부. 끄면 공개 조회에서 빠진다</summary>
    public bool IsPublished { get; set; }
}

/// <summary>
/// 뉴스 · 보도자료. 목록과 상세를 함께 쓰는 글이다.
/// </summary>
public class SitePost : BaseEntity<Guid>
{
    /// <summary>주소에 쓰는 열쇠 (예: <c>2026-jsini-partnership</c>). (Locale) 과 함께 유일하다</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>언어. <c>ko</c> · <c>en</c></summary>
    public string Locale { get; set; } = "ko";

    /// <summary>제목</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>목록에 보여 줄 요약. 비우면 화면이 본문 앞부분을 쓴다</summary>
    public string? Summary { get; set; }

    /// <summary>본문. 마크다운으로 넣는다</summary>
    public string? Body { get; set; }

    /// <summary>대표 이미지의 FileServer 파일 아이디. is_public 을 켜 두어야 한다 — 꺼져 있으면 방문자에게 404 로 나간다.</summary>
    public Guid? CoverFileId { get; set; }

    /// <summary>공개 시각. 이 시각이 지나야 공개 조회에 나온다</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>화면에 내보낼지 여부</summary>
    public bool IsPublished { get; set; }
}

/// <summary>공개 자료실의 한 자료. 파일은 FileServer 가 보관하고 여기는 아이디만 둔다. 내려받기 수를 세려고 브라우저가 FileServer 를 직접 열지 않게 GET /api/site/downloads/{id}/file 을 거쳐 세고 302 로 넘긴다.</summary>
public class SiteDownload : BaseEntity<Guid>
{
    /// <summary>언어. <c>ko</c> · <c>en</c></summary>
    public string Locale { get; set; } = "ko";

    /// <summary>자료명</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>설명 한두 줄</summary>
    public string? Description { get; set; }

    /// <summary>분류 (예: <c>회사소개</c> · <c>제품</c>). 화면의 탭이 된다</summary>
    public string? Category { get; set; }

    /// <summary>FileServer 파일 아이디. is_public 을 켜 두어야 한다.</summary>
    public Guid FileId { get; set; }

    /// <summary>목록에 보여 줄 파일명. FileServer 의 원본명과 달라도 된다</summary>
    public string? FileName { get; set; }

    /// <summary>파일 크기(바이트). 목록에 표시하려고 복사해 둔다</summary>
    public long? FileSize { get; set; }

    /// <summary>내려받은 횟수</summary>
    public int DownloadCount { get; set; }

    /// <summary>목록 순서. 작은 값이 위</summary>
    public int SortOrder { get; set; }

    /// <summary>공개 여부. 끄면 목록과 내려받기 모두 막힌다</summary>
    public bool IsPublished { get; set; }
}
