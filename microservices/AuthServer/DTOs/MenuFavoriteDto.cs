namespace AuthServer.DTOs;

/// <summary>
/// 즐겨찾기 메뉴 한 건. 사이드바 즐겨찾기 묶음과 탭 오른쪽 메뉴가 이 값을 쓴다.
///
/// <para>
/// 제목·아이콘은 저장된 값이 아니라 <c>scom.system_menus</c> 에서 그때그때 읽은 값이다.
/// 메뉴 관리에서 제목을 고치면 즐겨찾기 이름도 함께 따라간다.
/// </para>
/// </summary>
public class MenuFavoriteDto
{
    /// <summary>즐겨찾기한 메뉴 식별자 (<c>scom.system_menus.id</c>)</summary>
    public string MenuId { get; set; } = string.Empty;

    /// <summary>
    /// 메뉴 경로. 화면은 이 값으로 즐겨찾기 여부를 판단하고 이동한다.
    /// (탭이 아는 것은 경로뿐이라 등록·해제 API 도 경로를 받는다)
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>메뉴 및 라우트의 고유 명칭</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 화면에 표시할 제목. 다국어 키일 수도 있고 그대로 쓸 글자일 수도 있다
    /// (프론트의 <c>$tIfKey</c> 가 구분한다).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>메뉴 아이콘</summary>
    public string? Icon { get; set; }

    /// <summary>사이드바 즐겨찾기 묶음에서의 표시 순서</summary>
    public int SortOrder { get; set; }
}

/// <summary>즐겨찾기 등록·해제 요청. 탭이 아는 값(경로)으로 받는다.</summary>
public class MenuFavoriteRequest
{
    /// <summary>메뉴 경로 (예: <c>/system/account</c>)</summary>
    public string Path { get; set; } = string.Empty;
}
