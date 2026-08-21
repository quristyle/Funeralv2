import { requestClient } from '#/api/request';

export namespace InfoApi {
  export interface Notice {
    id: string;
    title: string;
    content: string;
    author: string;
    createdAt: string;
    isImportant: boolean;
  }

  export interface RoomHistory {
    id: string;
    roomId: string;
    roomName: string;
    actionType: string; // ENTER, LEAVE, CLEAN, REPAIR 등
    actorName: string;
    remark?: string;
    createdAt: string;
  }

  export interface MyInfo {
    userId: string;
    loginId: string;
    userName: string;
    email?: string;
    phone?: string;
    roleName: string;
    lastLoginAt?: string;
  }

  export interface JqlData {
    code: string;
    value: string;
    description?: string;
    updatedAt: string;
  }
}

/**
 * 알림 정보 목록 조회
 */
export async function getNotices() {
  return requestClient.get<InfoApi.Notice[]>('/info/notice/list');
}

/**
 * 호실 히스토리 목록 조회
 */
export async function getRoomHistories(roomId?: string) {
  return requestClient.get<InfoApi.RoomHistory[]>('/info/room-history/list', { params: { roomId } });
}

/**
 * 나의 정보 조회
 */
export async function getMyInfo() {
  return requestClient.get<InfoApi.MyInfo>('/info/my-info');
}

/**
 * 나의 정보 수정
 */
export async function updateMyInfo(data: Partial<InfoApi.MyInfo>) {
  return requestClient.put('/info/my-info', data);
}

/**
 * JQL 관련 데이터 조회 (JQLXME, JQLBME, JQLME, JQLSME 공통 지원)
 */
export async function getJqlData(type: string) {
  return requestClient.get<InfoApi.JqlData[]>('/info/jql/list', { params: { type } });
}
