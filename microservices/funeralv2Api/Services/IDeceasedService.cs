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
    /// 고인 등록. 상태가 허용 값이 아니거나 호실 배정이 불가하면
    /// <see cref="System.InvalidOperationException"/> 을 던진다.
    /// </summary>
    /// <param name="actor">변경한 사용자 (게이트웨이 X-User-Id)</param>
    Task<DeceasedDto> CreateDeceasedAsync(DeceasedCreateDto dto, string? actor = null);

    /// <summary>
    /// 고인 정보 수정. 상태가 허용 값이 아니거나 호실 배정이 불가하면
    /// <see cref="System.InvalidOperationException"/> 을 던진다.
    /// </summary>
    /// <param name="actor">변경한 사용자 (게이트웨이 X-User-Id)</param>
    Task<DeceasedDto?> UpdateDeceasedAsync(string id, DeceasedUpdateDto dto, string? actor = null);

    /// <summary>
    /// 고인 삭제 (Soft Delete)
    /// </summary>
    /// <param name="actor">변경한 사용자 (게이트웨이 X-User-Id)</param>
    Task<bool> DeleteDeceasedAsync(string id, string? actor = null);

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

    /// <summary>
    /// 장비코드로 입구 안내용 호실 및 고인 상세 정보 목록 조회
    /// </summary>
    Task<List<EntranceGuideRoomDto>> GetEntranceGuideRoomsByDeviceCodeAsync(string deviceCode);

    /// <summary>
    /// 장비코드를 이용해 키오스크용 건물 전체 호실 및 이미지 리스트 조회
    /// </summary>
    Task<KioskGuideResponseDto> GetKioskRoomsByDeviceCodeAsync(string deviceCode);

    /// <summary>
    /// 고인의 호실 이동 — 배정만 바꾸고 인적 사항은 건드리지 않는다.
    /// 대상 호실이 배정 불가하면 <see cref="System.InvalidOperationException"/> 을 던진다.
    /// </summary>
    /// <param name="actor">변경한 사용자 (게이트웨이 X-User-Id)</param>
    Task<bool> MoveRoomAsync(string deceasedId, string newRoomId, string? actor = null);

    /// <summary>
    /// 고인의 출상 처리 — 상태를 출상 완료로 바꾸고 활성 배정을 끝낸다.
    /// 다른 인적 사항은 건드리지 않는다 (전체 PUT 으로 하면 목록 DTO 에 없는
    /// 칸들이 지워지는 문제가 있었다).
    /// </summary>
    /// <param name="actor">변경한 사용자 (게이트웨이 X-User-Id)</param>
    Task<bool> DepartAsync(string deceasedId, string? actor = null);

    /// <summary>
    /// 고인의 출상 취소 처리. 되돌아갈 호실에 다른 고인이 입실 중이면
    /// <see cref="System.InvalidOperationException"/> 을 던진다 (옛 시스템과 같은 규칙).
    /// </summary>
    /// <param name="actor">변경한 사용자 (게이트웨이 X-User-Id)</param>
    Task<bool> CancelDepartureAsync(string deceasedId, string? actor = null);
}
