import { requestClient } from '#/api/request';

/**
 * 정보 화면 묶음 API — 알림정보 · 호실히스토리 · 고인정보조회 · 나의정보 · 미리보기.
 *
 * 경로가 `/funeral/...` 로 시작하는 이유는 게이트웨이가 `/api/funeral/**` 만
 * 장례식장 서비스로 보내기 때문이다(`funeral-service-route`).
 * 예전에는 `/info/...` 로 적혀 있었는데 그러면 어디로도 가지 않는다.
 */
export namespace InfoApi {
  /** 알림 정보 한 건 */
  export interface Notice {
    id: string;
    title: string;
    content?: string;
    /** NOTICE 공지 · ALERT 경고 · SYSTEM 시스템 */
    noticeType: string;
    isImportant: boolean;
    targetUserId?: string;
    buildingId?: string;
    buildingName?: string;
    targetPage?: string;
    targetParam?: string;
    startAt?: string;
    endAt?: string;
    author?: string;
    createdAt: string;
    /** 지금 보고 있는 사람이 읽었는지 */
    isRead: boolean;
  }

  export interface NoticeSave {
    title: string;
    content?: string;
    noticeType: string;
    isImportant: boolean;
    targetUserId?: string;
    buildingId?: string;
    targetPage?: string;
    targetParam?: string;
    startAt?: string;
    endAt?: string;
  }

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
    funeralDate?: string;
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
    unreadNoticeCount: number;
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

// ── 알림정보 ────────────────────────────────────────────────

/** 알림 목록. 전체 공지와 본인 앞으로 온 것을 합쳐서 돌려준다. */
export async function getNotices(params?: {
  buildingId?: string;
  includeExpired?: boolean;
}) {
  return requestClient.get<InfoApi.Notice[]>('/funeral/info/notice/list', {
    params,
  });
}

export async function getNotice(id: string) {
  return requestClient.get<InfoApi.Notice>(`/funeral/info/notice/${id}`);
}

export async function createNotice(data: InfoApi.NoticeSave) {
  return requestClient.post<InfoApi.Notice>('/funeral/info/notice', data);
}

export async function updateNotice(id: string, data: InfoApi.NoticeSave) {
  return requestClient.put<InfoApi.Notice>(`/funeral/info/notice/${id}`, data);
}

export async function deleteNotice(id: string) {
  return requestClient.delete(`/funeral/info/notice/${id}`);
}

/** 읽음으로 표시한다. 이미 읽었으면 아무 일도 하지 않는다. */
export async function markNoticeRead(id: string) {
  return requestClient.post(`/funeral/info/notice/${id}/read`);
}

/** 안 읽은 알림 수 */
export async function getUnreadNoticeCount(buildingId?: string) {
  return requestClient.get<number>('/funeral/info/notice/unread-count', {
    params: { buildingId },
  });
}

// ── 호실 히스토리 ───────────────────────────────────────────

export async function getRoomHistories(params?: {
  buildingId?: string;
  roomId?: string;
  from?: string;
  to?: string;
}) {
  return requestClient.get<InfoApi.RoomHistory[]>(
    '/funeral/info/room-history/list',
    { params },
  );
}

// ── 고인 정보 조회 ──────────────────────────────────────────

export async function searchDeceased(params?: {
  keyword?: string;
  buildingId?: string;
  roomId?: string;
  from?: string;
  to?: string;
  status?: string;
}) {
  return requestClient.get<InfoApi.DeceasedLookup[]>(
    '/funeral/info/deceased-search/list',
    { params },
  );
}

// ── 나의 정보 ───────────────────────────────────────────────

export async function getMyInfo() {
  return requestClient.get<InfoApi.MyInfo>('/funeral/info/my-info');
}

// ── 미리보기 ────────────────────────────────────────────────

export async function getDevicePreviews(params?: {
  buildingId?: string;
  roomId?: string;
}) {
  return requestClient.get<InfoApi.DevicePreview[]>(
    '/funeral/info/preview/list',
    { params },
  );
}
