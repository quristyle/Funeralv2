import { unwrapList, unwrapOne } from '#/api/envelope';
import { baseRequestClient, requestClient } from '#/api/request';

/**
 * 공지 API.
 *
 * 공지는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
 * 각 MSA 가 자기 공지를 따로 두지 않는다.
 */
export namespace NoticeApi {
  /** 공지 첨부파일 */
  export interface NoticeFile {
    contentType?: null | string;
    /** 내려받기 주소. 서버가 만들어 준다. */
    downloadUrl?: string;
    /** FileServer 가 발급한 파일 아이디 */
    fileId: string;
    fileName: string;
    fileSize: number;
    id?: string;
    sortNo: number;
  }

  /** 공지 */
  export interface Notice {
    content?: null | string;
    createdAt?: string;
    createdBy?: null | string;
    /** 게시 종료 일시. 비우면 제한 없음 */
    endAt?: null | string;
    files: NoticeFile[];
    id: string;
    /** 팝업으로 띄울지. 끄면 목록에만 남는다. */
    isPopup: boolean;
    /** 로그인하지 않은 사용자도 볼 수 있는지 */
    isPublic: boolean;
    orderNo: number;
    /** 게시 시작 일시. 비우면 제한 없음 */
    startAt?: null | string;
    /** 0: 비활성, 1: 활성 */
    status: number;
    title: string;
  }

  /** 등록·수정 요청 */
  export interface SaveNotice {
    content?: null | string;
    endAt?: null | string;
    /** 보낸 그대로가 최종 상태가 된다. 빠진 것은 지워진다. */
    files: Omit<NoticeFile, 'downloadUrl' | 'id'>[];
    isPopup: boolean;
    isPublic: boolean;
    orderNo: number;
    startAt?: null | string;
    status: number;
    title: string;
  }
}

/** 목록을 꺼낸다. 기준은 `src/api/envelope.ts` 한 곳이다. */
function toList(res: any): NoticeApi.Notice[] {
  return unwrapList<NoticeApi.Notice>(res);
}

/** 관리 목록 */
export async function getNoticeList(keyword?: string) {
  const res = await requestClient.get<any>('/auth/notices', {
    params: keyword ? { keyword } : undefined,
  });
  return toList(res);
}

/**
 * 단건 조회.
 *
 * 봉투는 단건도 `{ result: [ … ], page }` 로 감싸 보낸다 — 그래서 `unwrapOne` 이다.
 */
export async function getNotice(id: string) {
  return unwrapOne<NoticeApi.Notice>(
    await requestClient.get(`/auth/notices/${id}`),
  ) as NoticeApi.Notice;
}

export async function createNotice(data: NoticeApi.SaveNotice) {
  return requestClient.post('/auth/notices', data);
}

export async function updateNotice(id: string, data: NoticeApi.SaveNotice) {
  return requestClient.put(`/auth/notices/${id}`, data);
}

export async function deleteNotice(id: string) {
  return requestClient.delete(`/auth/notices/${id}`);
}

/** 로그인한 사용자에게 띄울 팝업 공지 */
export async function getPopupNotices() {
  const res = await requestClient.get<any>('/auth/notices/popup');
  return toList(res);
}

/**
 * 로그인 전에도 볼 수 있는 팝업 공지.
 *
 * 인증 헤더를 붙이지 않는 클라이언트를 쓴다. 로그인 화면에서도 불러야 하는데,
 * 일반 클라이언트는 토큰이 없거나 만료됐을 때 로그인 화면으로 되돌리는
 * 인터셉터가 걸려 있어 화면이 튕긴다.
 */
export async function getPublicPopupNotices() {
  try {
    const res = await baseRequestClient.get<any>('/auth/notices/popup/public');
    // baseRequestClient 는 봉투를 벗기지 않으므로 여기서 꺼낸다.
    return toList(res?.data ?? res);
  } catch {
    // 공지를 못 받아도 화면 진입을 막지 않는다.
    return [];
  }
}
