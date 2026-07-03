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
    Task<List<DeceasedDto>> GetDeceasedListAsync(DeceasedSearchDto searchDto);

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

    /// <summary>
    /// 고인 종합 상세 정보 조회
    /// </summary>
    Task<DeceasedDetailDto?> GetDeceasedDetailAsync(string id);

    /// <summary>
    /// 고인 종합 상세 정보 저장 및 일괄 갱신
    /// </summary>
    Task<DeceasedDetailDto?> SaveDeceasedDetailAsync(string id, DeceasedDetailDto dto);

    /// <summary>
    /// 호실 ID로 현재 배정된 고인의 종합 상세 정보 조회
    /// </summary>
    Task<DeceasedDetailDto?> GetDeceasedDetailByDeviceCodeAsync(string deviceCode);
}
