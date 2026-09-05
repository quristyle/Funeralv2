import type { BuildingApi } from '#/api/funeral/building';

import { unwrapList, unwrapOne } from '#/api/envelope';
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

    /** EMPTY 비어 있음 · USING 사용 중 (예약은 옛 시스템에서도 실사용 0건 — 이식 안 함) */
    status: 'EMPTY' | 'USING';

    deceasedId?: string;
    /** 고인 장례 상태 — FUNERAL_IN_PROGRESS · FUNERAL_DEPARTURE_COMPLETED · COMPLETED */
    deceasedStatus?: string;
    deceasedName?: string;
    deceasedGender?: string;
    deceasedAge?: number;
    religion?: string;

    /** 영정 — 서버가 보정본 우선으로 골라 준다 */
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

    /** 사망 일시 */
    deathDate?: string;

    startTime?: string;
    /** 빈 호실의 마지막 퇴실 일시 (배정 이력에서 유도) */
    lastVacatedAt?: string;
    /** 빈 호실에서 마지막으로 출상한 고인 — 출상 취소 진입점 (`room-board` 전용) */
    lastDepartedDeceasedId?: string;
    lastDepartedDeceasedName?: string;
    useDays: number;

    deviceCount: number;
    onlineDeviceCount: number;

    /** 이 호실의 장비 목록 — `room-board` 만 채운다. 다른 조회에선 null. */
    devices?: BuildingApi.Device[];

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

  /** 빈소현황 대시보드(`/room_status`) 응답 — 건물 공용 장비까지 한 번에 */
  export interface RoomBoard extends Board {
    /** 호실에 매이지 않은 건물 공용 장비 */
    commonDevices: BuildingApi.Device[];
  }

  /** 대시보드 조회 조건. 기간 이름은 실제로 거르는 컬럼을 그대로 말한다. */
  export interface RoomBoardQuery {
    companyId?: string;
    buildingId?: string;
    floorId?: string;
    /** 고인명 부분 일치 */
    name?: string;
    /** 입관 일시 범위 */
    coffinStartDate?: string;
    coffinEndDate?: string;
    /** 발인 일시 범위 */
    burialStartDate?: string;
    burialEndDate?: string;
    /**
     * 응답의 자세함.
     *
     * `full`(기본)은 장비 목록·영정 사진·상주까지 붙인다 — 호실을 직접 조작하는
     * 밀도(운영)가 쓴다. `summary` 는 감시·상황판 밀도용으로, 타일에 그리지 않는
     * 칸을 빼고 보낸다. 시설 수십 곳을 60초마다 다시 받을 때 차이가 크다.
     */
    detail?: 'full' | 'summary';
  }
}

/** 목록과 요약을 함께 받는다 — 화면이 두 번 부르지 않도록. */
export async function getFuneralStatusBoard(params?: {
  buildingId?: string;
  floorId?: string;
  onlyInUse?: boolean;
}) {
  return unwrapOne<StatusApi.Board>(
    await requestClient.get('/funeral/status/funeral-status/board', { params }),
  );
}

/** 목록만 받는다. */
export async function getFuneralStatuses(params?: {
  buildingId?: string;
  floorId?: string;
  onlyInUse?: boolean;
}) {
  return unwrapList<StatusApi.FuneralStatus>(
    await requestClient.get('/funeral/status/funeral-status/list', { params }),
  );
}

export async function getFuneralStatusDetail(roomId: string) {
  return unwrapOne<StatusApi.FuneralStatus>(
    await requestClient.get(`/funeral/status/funeral-status/${roomId}`),
  );
}

/**
 * 빈소현황 대시보드(`/room_status`) — 호실·고인·장비를 서버가 붙여 준다.
 * 예전에는 화면이 건물·호실·장비·고인 네 목록을 받아 브라우저에서 조인했다
 * (47번 문서 0단계).
 */
export async function getRoomBoard(params?: StatusApi.RoomBoardQuery) {
  return unwrapOne<StatusApi.RoomBoard>(
    await requestClient.get('/funeral/status/room-board', { params }),
  );
}
