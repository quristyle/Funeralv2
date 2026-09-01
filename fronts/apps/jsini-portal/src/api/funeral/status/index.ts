import { requestClient } from '#/api/request';

/**
 * 빈소 현황 API.
 *
 * 옛 시스템은 현황 화면마다 프로시저가 따로 있었다(`monitor/room_status` ·
 * `room_status_simple` · `mobile/room_status`). 담는 내용은 같고 그리는 모양만
 * 달라서 하나로 모았다 — 화면이 필요한 것만 골라 그린다.
 */
export namespace StatusApi {
  /** 빈소 한 칸의 현황 */
  export interface FuneralStatus {
    roomId: string;
    roomName: string;
    /** 좁은 화면에서 쓰는 짧은 명칭 */
    roomShortName?: string;
    floorId?: string;
    floorName?: string;
    buildingId?: string;
    buildingName?: string;
    sortOrder: number;

    /** EMPTY 비어 있음 · USING 사용 중 · RESERVED 예약 */
    status: 'EMPTY' | 'RESERVED' | 'USING';

    deceasedId?: string;
    deceasedName?: string;
    deceasedGender?: string;
    deceasedAge?: number;
    religion?: string;

    photoFileId?: string;
    photoUrl?: string;

    /** 상주. 여러 명이면 쉼표로 이어져 온다. */
    chiefMourner?: string;

    /** 입관 일시 */
    coffinTime?: string;
    /** 발인 일시 */
    dischargeTime?: string;
    /** 장지 */
    burialPlace?: string;

    startTime?: string;
    useDays: number;

    deviceCount: number;
    onlineDeviceCount: number;

    updatedAt: string;
  }

  export interface Summary {
    totalRooms: number;
    usingRooms: number;
    emptyRooms: number;
    totalDevices: number;
    onlineDevices: number;
  }

  export interface Board {
    rooms: FuneralStatus[];
    summary: Summary;
  }
}

/** 목록과 요약을 함께 받는다 — 화면이 두 번 부르지 않도록. */
export async function getFuneralStatusBoard(params?: {
  buildingId?: string;
  floorId?: string;
  onlyInUse?: boolean;
}) {
  return requestClient.get<StatusApi.Board>(
    '/funeral/status/funeral-status/board',
    { params },
  );
}

/** 목록만 받는다. */
export async function getFuneralStatuses(params?: {
  buildingId?: string;
  floorId?: string;
  onlyInUse?: boolean;
}) {
  return requestClient.get<StatusApi.FuneralStatus[]>(
    '/funeral/status/funeral-status/list',
    { params },
  );
}

export async function getFuneralStatusDetail(roomId: string) {
  return requestClient.get<StatusApi.FuneralStatus>(
    `/funeral/status/funeral-status/${roomId}`,
  );
}
