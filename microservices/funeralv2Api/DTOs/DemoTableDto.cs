namespace funeralv2Api.DTOs;

/// <summary>
/// 데모 테이블의 데이터를 전달하기 위한 DTO
/// </summary>
public class DemoTableDto
{
    /// <summary>
    /// 고유 아이디
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 제목
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 작성자
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 생성 일시
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 상태 (기본값: published)
    /// </summary>
    public string Status { get; set; } = "published";
}

/// <summary>
/// 페이징된 결과를 담는 공통 DTO
/// </summary>
/// <typeparam name="T">목록 항목의 데이터 타입</typeparam>
public class PagedResultDto<T>
{
    /// <summary>
    /// 실제 데이터 목록
    /// </summary>
    public List<T> Result { get; set; } = new();

    /// <summary>
    /// 전체 데이터 건수
    /// </summary>
    public PageInfo Page { get; set; } = new();
}

/// <summary>
/// 페이징 정보
/// </summary>
public class PageInfo
{
    /// <summary>전체 데이터 건수</summary>
    public int Total { get; set; }
}
