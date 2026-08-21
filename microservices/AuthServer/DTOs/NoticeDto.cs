namespace AuthServer.DTOs;

/// <summary>
/// 공지 첨부파일
/// </summary>
public class NoticeFileDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>FileServer 가 발급한 파일 아이디</summary>
    public string FileId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public int SortNo { get; set; }

    /// <summary>내려받기 주소. 화면이 그대로 링크로 쓴다.</summary>
    public string DownloadUrl => $"/api/file/download/id/{FileId}";
}

/// <summary>
/// 공지 조회 결과
/// </summary>
public class NoticeDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }

    /// <summary>로그인하지 않은 사용자도 볼 수 있는지</summary>
    public bool IsPublic { get; set; }

    /// <summary>팝업으로 띄울지</summary>
    public bool IsPopup { get; set; }

    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    /// <summary>0: 비활성, 1: 활성</summary>
    public int Status { get; set; }

    public int OrderNo { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public List<NoticeFileDto> Files { get; set; } = new();
}

/// <summary>
/// 공지 등록·수정 요청
/// </summary>
public class SaveNoticeDto
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public bool IsPublic { get; set; }
    public bool IsPopup { get; set; } = true;
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int Status { get; set; } = 1;
    public int OrderNo { get; set; }

    /// <summary>
    /// 첨부파일 목록. 보낸 그대로가 최종 상태가 된다 —
    /// 빠진 것은 지우고 새로 들어온 것은 추가한다.
    /// </summary>
    public List<SaveNoticeFileDto> Files { get; set; } = new();
}

/// <summary>
/// 공지 첨부파일 등록 요청
/// </summary>
public class SaveNoticeFileDto
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public int SortNo { get; set; }
}
