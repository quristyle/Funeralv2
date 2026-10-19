namespace AuthServer.DTOs;

/// <summary>자료실 첨부파일</summary>
public class HelpArchiveFileDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>FileServer 가 발급한 파일 아이디</summary>
    public string FileId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    /// <summary>바이트 크기. 화면이 KB·MB 로 바꿔 보여준다.</summary>
    public long FileSize { get; set; }

    public string? ContentType { get; set; }

    public int SortNo { get; set; }

    public int DownloadCount { get; set; }

    /// <summary>내려받을 주소. 다운로드 수를 세려고 FileServer 로 302 로 넘긴다 — 브라우저가 FileServer 를 직접 열면 셀 수가 없다.</summary>
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>자료실 항목</summary>
public class HelpArchiveDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>분류. 비우면 화면이 '기타' 로 묶는다.</summary>
    public string? Category { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>자료 설명 (HTML)</summary>
    public string? Description { get; set; }

    public int OrderNo { get; set; }

    /// <summary>0: 비활성, 1: 활성</summary>
    public int Status { get; set; }

    public int DownloadCount { get; set; }

    public List<HelpArchiveFileDto> Files { get; set; } = new();

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>자료실 등록·수정 요청</summary>
public class SaveHelpArchiveDto
{
    public string? Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; } = 1;

    /// <summary>첨부파일 목록. 보낸 목록이 그대로 저장된다 — 빠진 것은 지워지고 새로 온 것은 추가된다.</summary>
    public List<SaveHelpArchiveFileDto> Files { get; set; } = new();
}

/// <summary>자료실 첨부파일 등록 요청. 파일은 화면이 먼저 FileServer 에 올리고 받은 fileId 를 보낸다.</summary>
public class SaveHelpArchiveFileDto
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public int SortNo { get; set; }
}

/// <summary>자료실 목록 응답. 관리자 판정은 서버 한 곳에서 한다 — 화면이 권한 스토어로 판단하면 늦게 온 권한과 어긋난다.</summary>
public class HelpArchiveListDto
{
    public List<HelpArchiveDto> Items { get; set; } = new();

    /// <summary>등록·수정·삭제할 수 있는 사용자인지</summary>
    public bool CanManage { get; set; }

    /// <summary>지금 등록된 분류 목록. 등록 창의 분류 추천에 쓴다.</summary>
    public List<string> Categories { get; set; } = new();
}
