using System.Collections.Generic;
using System.Threading.Tasks;
using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 고인 관리 서비스 인터페이스
/// </summary>
public interface IDeceasedService
{
    /// <summary>
    /// 고인 목록 조회 (필터링 가능)
    /// </summary>
    Task<List<DeceasedDto>> GetDeceasedListAsync();

    /// <summary>
    /// 고인 등록
    /// </summary>
    Task<DeceasedDto> CreateDeceasedAsync(DeceasedCreateDto dto);

    /// <summary>
    /// 고인 정보 수정
    /// </summary>
    Task<DeceasedDto?> UpdateDeceasedAsync(string id, DeceasedUpdateDto dto);

    /// <summary>
    /// 고인 삭제 (Soft Delete)
    /// </summary>
    Task<bool> DeleteDeceasedAsync(string id);
}
