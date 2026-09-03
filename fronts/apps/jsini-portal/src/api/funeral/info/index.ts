import { unwrapList, unwrapOne } from '#/api/envelope';
import { requestClient } from '#/api/request';

/**
 * 정보 화면 묶음 API — 호실히스토리 · 고인정보조회 · 나의정보 · 미리보기.
 *
 * 경로가 `/funeral/...` 로 시작하는 이유는 게이트웨이가 `/api/funeral/**` 만
 * 장례식장 서비스로 보내기 때문이다(`funeral-service-route`).
 * 예전에는 `/info/...` 로 적혀 있었는데 그러면 어디로도 가지 않는다.
 *
 * **알림정보(`/info/notice`)는 2026-09-03 에 걷어냈다.** 쓰지 않는 화면이었다 —
 * 포털의 공지(`/portal/notice`, AuthServer)와 알림 설정(`/system/push/setting`,
 * NotificationServer)이 그 자리를 이미 채우고 있다. 경위는
 * docs/analysis/40-old-funeral-migration.md 의 알림정보 항목.
 */
export namespace InfoApi {
  /** 호실 히스토리 한 줄 */
  export interface RoomHistory {
    id: string;
    roomId: string;
    roomName: string;
    floorName?: string;
    buildingId?: string;
    buildingName?: string;
    deceasedId: string;
    deceasedName: string;
    gender?: string;
    age?: number;
    memorialPhotoFileId?: string;
    memorialPhotoUrl?: string;
    /** 입실 */
    startTime?: string;
    /** 퇴실 */
    endTime?: string;
    useDays: number;
    /** 발인 */
    departureDate?: string;
    burialPlot?: string;
    inUse: boolean;
    departed: boolean;
    status: string;
  }

  /** 고인 정보 조회 결과 한 줄 */
  export interface DeceasedLookup {
    id: string;
    name: string;
    gender?: string;
    age?: number;
    religion?: string;
    memorialPhotoFileId?: string;
    memorialPhotoUrl?: string;
    deathDate?: string;
    funeralDate?: string;
    burialDate?: string;
    burialPlot?: string;
    status: string;
    roomId?: string;
    roomName?: string;
    floorName?: string;
    buildingId?: string;
    buildingName?: string;
    startTime?: string;
    endTime?: string;
    /** 상주 이름을 쉼표로 이어 붙인 것 */
    mournerNames?: string;
    createdAt: string;
  }

  /** 나의 정보 */
  export interface MyInfo {
    userId: string;
    role?: string;
    buildingCount: number;
    roomsInUse: number;
    settings: SettingItem[];
  }

  /** 계정별 업무 설정 한 줄 */
  export interface SettingItem {
    code: string;
    name: string;
    description?: string;
    groupName: string;
    enabled: boolean;
    defaultValue: boolean;
    updatedAt?: string;
  }

  /** 미리보기 대상 장비 */
  export interface DevicePreview {
    id: string;
    name: string;
    deviceCode?: string;
    deviceType?: string;
    roomId?: string;
    roomName?: string;
    buildingId?: string;
    buildingName?: string;
    isOnline: boolean;
    lastConnectedAt?: string;
    previewUrl: string;
  }
}

// ── 호실 히스토리 ───────────────────────────────────────────

export async function getRoomHistories(params?: {
  buildingId?: string;
  from?: string;
  /** 참이면 사용 중만 · 거짓이면 출상만 · 비우면 둘 다 */
  inUse?: boolean;
  /** 고인 성명 일부. 호실을 몰라도 이름으로 찾는다. */
  keyword?: string;
  roomId?: string;
  to?: string;
}) {
  return unwrapList<InfoApi.RoomHistory>(
    await requestClient.get('/funeral/info/room-history/list', { params }),
  );
}

// ── 고인 정보 조회 ──────────────────────────────────────────

export async function searchDeceased(params?: {
  buildingId?: string;
  from?: string;
  keyword?: string;
  roomId?: string;
  status?: string;
  to?: string;
}) {
  return unwrapList<InfoApi.DeceasedLookup>(
    await requestClient.get('/funeral/info/deceased-search/list', { params }),
  );
}

// ── 나의 정보 ───────────────────────────────────────────────

export async function getMyInfo() {
  return unwrapOne<InfoApi.MyInfo>(
    await requestClient.get('/funeral/info/my-info'),
  );
}

// ── 미리보기 ────────────────────────────────────────────────

export async function getDevicePreviews(params?: {
  buildingId?: string;
  roomId?: string;
}) {
  return unwrapList<InfoApi.DevicePreview>(
    await requestClient.get('/funeral/info/preview/list', { params }),
  );
}
