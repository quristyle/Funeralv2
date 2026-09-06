namespace JSini.Web.Models;

/// <summary>
/// 공지 한 건. AuthServer 의 <c>NoticeDto</c> 와 짝이다.
/// </summary>
/// <remarks>
/// <para>
/// <b>여기 있는 이유 — 셋이 쓴다.</b> 포털관리의 공지 관리 화면, 로그인한
/// 사용자에게 뜨는 팝업(<c>MainLayout</c>), 로그인 화면의 공개 공지 팝업이다.
/// 셸은 업무 모듈을 이름으로 알지 못하므로 포털관리 모듈에 두면 앞의 하나만
/// 쓸 수 있다. 「두 모듈이 쓰면 복제, 세 번째부터 승격」의 세 번째다.
/// </para>
///
/// <para>
/// [전체 공개와 팝업은 다른 것이다]
/// </para>
///
/// <list type="bullet">
///   <item><see cref="IsPublic"/> — 로그인하지 않아도 보인다(로그인 화면에서 뜬다).</item>
///   <item><see cref="IsPopup"/> — 팝업으로 띄울지. 끄면 공지 목록에만 남는다.</item>
/// </list>
/// </remarks>
public sealed class NoticeDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>본문 (HTML). 화면에 넣기 전에 <c>NoticeHtml.Sanitize</c> 를 거친다.</summary>
    public string Content { get; set; } = string.Empty;

    public bool IsPopup { get; set; }
    public bool IsPublic { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AuthorName { get; set; }

    /// <summary>첨부파일. 서버가 정렬해서 준다.</summary>
    public List<NoticeFileDto> Files { get; set; } = [];

    /// <summary>
    /// 편집 폼의 「사용」 스위치가 묶이는 자리. <see cref="Status"/> 의 0/1 을 가린다.
    ///
    /// <para>
    /// 팝업 안 편집기는 <c>@@bind-</c> 로 묶어야 한다. <c>Checked</c> 와
    /// <c>CheckedChanged</c> 를 따로 주면 검증 식이 없어 <b>팝업이 말없이
    /// 안 열린다</b> — 화면에는 아무 표시도 안 나고 브라우저 콘솔에만
    /// <c>requires a value for the 'CheckedExpression' property</c> 가 남는다.
    /// </para>
    /// </summary>
    public bool IsActive
    {
        get => Status == 1;
        set => Status = value ? 1 : 0;
    }
}

/// <summary>
/// 공지에 매달린 첨부파일 한 개.
/// </summary>
/// <remarks>
/// <para>
/// <b>서버가 주는 <c>downloadUrl</c> 을 쓰지 않는다.</b> 그 값은
/// <c>/api/file/download/id/{fileId}</c> 인데, 그것은 브라우저가 게이트웨이와
/// 같은 오리진에 있던 Vue 시절의 주소다. 지금 브라우저가 보는 것은 포털
/// (:5557)이고 거기에는 <c>/api</c> 가 없다. 내려받기는 셸이 중계한다 —
/// <c>FileDownload.UrlFor</c> 를 쓴다.
/// </para>
/// </remarks>
public sealed class NoticeFileDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>FileServer 가 발급한 파일 아이디.</summary>
    public string FileId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public int SortNo { get; set; }
}

/// <summary>공지 등록·수정 요청.</summary>
public sealed class SaveNoticeDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPopup { get; set; }
    public bool IsPublic { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; } = 1;
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    /// <summary>
    /// 첨부파일. <b>보낸 그대로가 최종이다</b> — 빠진 것은 서버가 뗀다.
    /// 그래서 고칠 때 이미 매달려 있던 것을 함께 실어야 한다.
    /// </summary>
    public List<SaveNoticeFileDto> Files { get; set; } = [];
}

/// <summary>공지에 매달 첨부파일 하나. 파일은 이미 FileServer 에 올라가 있어야 한다.</summary>
public sealed class SaveNoticeFileDto
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public int SortNo { get; set; }
}
