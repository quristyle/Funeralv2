import { requestClient } from '#/api/request';

export namespace StatusApi {
  export interface FuneralStatus {
    roomId: string;
    roomName: string;
    roomType: string;
    status: 'EMPTY' | 'USING';
    deceasedName?: string;
    deceasedGender?: 'MALE' | 'FEMALE';
    deceasedAge?: number;
    chiefMourner?: string; // 상주
    coffinTime?: string; // 입관 일시
    dischargeTime?: string; // 발인 일시
    burialPlace?: string; // 장지
    photoUrl?: string; // 영정 사진
    updatedAt: string;
  }
}

/**
 * 전역 빈소 정보 및 현황 목록 조회 (빈소 정보, 빈소 현황, 고인 현황, 심플, 모바일 등 공통 사용)
 */
export async function getFuneralStatuses() {
  return requestClient.get<StatusApi.FuneralStatus[]>('/status/funeral-status/list');
}

/**
 * 개별 빈소 상세 정보 조회
 */
export async function getFuneralStatusDetail(roomId: string) {
  return requestClient.get<StatusApi.FuneralStatus>(`/status/funeral-status/${roomId}`);
}
