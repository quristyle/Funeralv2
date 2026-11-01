namespace ProjMngServer.Models;

// 개발자 명부는 포털 계정의 Dev.* 속성(scom.account_profile_details)으로 옮겨 이 파일에는 없다(docs/projmng-account-merge.md).

/// <summary>팀 공유 문서 한 쪽 — <c>projmng.wbs_docs</c>.</summary>
public sealed class WbsBoardDoc
{
    public int Id { get; set; }

    public string? Title { get; set; }

    /// <summary>본문 HTML. 화면의 서식 편집기가 만든 것이다.</summary>
    public string? Content { get; set; }

    public int SortOrder { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }

    /// <summary>처음 쓴 때·사람. 이 칸을 넣기 전에 만든 쪽은 비어 있다.</summary>
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }

    /// <summary>휴지통으로 보낸 때·사람. 휴지통 목록에서만 채운다.</summary>
    public string? DeletedBy { get; set; }
    public string? DeletedAt { get; set; }
}

/// <summary>쪽에 붙인 문서 한 장 — <c>projmng.wbs_doc_files</c>. 올린 HTML 이거나 걸어 둔 주소다. 본문(html)은 목록에 싣지 않는다.</summary>
public sealed class WbsBoardDocFile
{
    public int Id { get; set; }
    public int DocId { get; set; }

    /// <summary>탭에 적을 이름.</summary>
    public string? Name { get; set; }

    /// <summary>걸어 둔 주소. 비어 있으면 올린 HTML 이다.</summary>
    public string? Url { get; set; }

    public long Bytes { get; set; }
    public int SortOrder { get; set; }
    public string? UploadedBy { get; set; }
    public string? UploadedAt { get; set; }
}

/// <summary>HTML 한 장 올리기. 글자라서 여러 부분 양식 대신 JSON 으로 받는다.</summary>
public sealed class WbsBoardDocUpload
{
    public string? Name { get; set; }
    public string? Html { get; set; }
}

/// <summary>주소 걸기.</summary>
public sealed class WbsBoardDocLink
{
    public string? Url { get; set; }
    public string? Name { get; set; }
}

/// <summary>사용자별 화면 설정 한 건 — <c>projmng.wbs_user_pref</c>.</summary>
public sealed class WbsBoardPref
{
    public string? Key { get; set; }

    /// <summary>담아 둔 값. JSON 글자 그대로다.</summary>
    public string? Value { get; set; }

    /// <summary>누구 몫으로 담겼나. 화면이 「누구의 설정인지」를 보여 준다.</summary>
    public string? Who { get; set; }
}
