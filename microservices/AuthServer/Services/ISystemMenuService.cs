using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 시스템 메뉴(라우트/권한) 관리를 위한 서비스 인터페이스
/// </summary>
public interface ISystemMenuService
{
    /// <summary>
    /// 시스템에 등록된 전체 메뉴를 트리 구조로 조회합니다.
    /// 제목의 다국어는 여기서 함께 붙여 <see cref="SystemMenuMetaDto.TitleText"/> 로 내려보냅니다.
    /// </summary>
    /// <param name="locale">
    /// 제목을 옮길 언어(<c>ko</c> · <c>en</c>). 비우면 <c>ko</c> 로 본다.
    /// </param>
    /// <returns>시스템 메뉴 DTO 트리 목록</returns>
    Task<List<SystemMenuDto>> GetMenuListAsync(string? locale = null);

    /// <summary>
    /// 중복된 메뉴 이름이 있는지 확인합니다.
    /// </summary>
    /// <param name="name">메뉴 이름</param>
    /// <param name="id">현재 수정 중인 메뉴 ID (신규 생성 시 null)</param>
    /// <returns>중복 여부 (true: 중복 있음)</returns>
    Task<bool> IsNameExistsAsync(string name, string? id);

    /// <summary>
    /// 중복된 메뉴 경로가 있는지 확인합니다.
    /// </summary>
    /// <param name="path">메뉴 경로</param>
    /// <param name="id">현재 수정 중인 메뉴 ID (신규 생성 시 null)</param>
    /// <returns>중복 여부 (true: 중복 있음)</returns>
    Task<bool> IsPathExistsAsync(string path, string? id);

    /// <summary>
    /// 새로운 시스템 메뉴를 생성합니다.
    /// </summary>
    /// <param name="request">생성할 메뉴 정보</param>
    /// <returns>생성된 메뉴의 기본 정보 DTO</returns>
    Task<SystemMenuDto> CreateMenuAsync(CreateSystemMenuDto request);

    /// <summary>
    /// 기존 시스템 메뉴 정보를 수정합니다.
    /// </summary>
    /// <param name="id">수정할 메뉴 ID</param>
    /// <param name="request">수정될 메뉴 정보</param>
    /// <returns>성공 여부</returns>
    Task<bool> UpdateMenuAsync(string id, CreateSystemMenuDto request);

    /// <summary>
    /// 특정 시스템 메뉴를 삭제합니다.
    /// </summary>
    /// <param name="id">삭제할 메뉴 ID</param>
    /// <returns>성공 여부</returns>
    Task<bool> DeleteMenuAsync(string id);
}
