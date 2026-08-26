import { requestClient } from '#/api/request';

/**
 * 자료실 API (`/help/archive`).
 *
 * 자료실은 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다
 * (공지 · F.A.Q 와 같은 방침).
 *
 * 관리자가 자료를 올리고 나머지 사용자는 설명을 읽고 내려받는다.
 * **판정은 서버가 한다** — 목록 응답의 `canManage` 를 보고 화면이 버튼을 켜고,
 * 저장 요청은 서버가 다시 확인한다.
 */
export namespace HelpArchiveApi {
  /** 첨부파일 한 개 */
  export interface ArchiveFile {
    contentType?: null | string;
    downloadCount: number;
    /**
     * 내려받을 주소.
     *
     * FileServer 주소가 아니라 AuthServer 를 한 번 거치는 주소다.
     * 거기서 다운로드 수를 세고 FileServer 로 302 로 넘긴다 —
     * 브라우저가 FileServer 를 직접 열면 셀 수가 없다.
     */
    downloadUrl: string;
    /** FileServer 가 발급한 파일 아이디 */
    fileId: string;
    fileName: string;
    /** 바이트 크기 */
    fileSize: number;
    id: string;
    sortNo: number;
  }

  /** 자료 한 건 */
  export interface Archive {
    /** 분류. 비우면 화면이 '기타' 로 묶는다. */
    category?: null | string;
    createdAt?: string;
    /** 자료 설명 (HTML) */
    description?: null | string;
    downloadCount: number;
    files: ArchiveFile[];
    id: string;
    orderNo: number;
    /** 0: 비활성, 1: 활성 */
    status: number;
    title: string;
    updatedAt?: null | string;
  }

  /** 목록 응답 */
  export interface ArchiveList {
    /** 등록·수정·삭제할 수 있는 사용자인지 (= 관리자) */
    canManage: boolean;
    /** 지금 등록된 분류 목록. 등록 창의 분류 추천에 쓴다. */
    categories: string[];
    items: Archive[];
  }

  /** 첨부 등록 요청. 파일은 먼저 FileServer 에 올리고 받은 fileId 를 담아 보낸다. */
  export interface SaveArchiveFile {
    contentType?: null | string;
    fileId: string;
    fileName: string;
    fileSize: number;
    sortNo: number;
  }

  /** 등록·수정 요청 */
  export interface SaveArchive {
    category?: null | string;
    description?: null | string;
    /** 보낸 목록이 그대로 저장된다 — 빠진 것은 지워지고 새로 온 것은 추가된다. */
    files: SaveArchiveFile[];
    orderNo: number;
    status: number;
    title: string;
  }
}

/**
 * AuthServer 의 응답 필터는 단건 객체도 `{ result: [ ... ], page }` 로 감싸 보낸다.
 * 배열로 와도 객체로 와도 하나를 꺼내 준다.
 */
function pickOne<T>(res: any): T | undefined {
  const raw = res?.result ?? res?.data?.result ?? res;
  return (Array.isArray(raw) ? raw[0] : raw) as T | undefined;
}

/** 목록. 관리자에게는 비활성 항목까지 온다. */
export async function getArchiveList(params?: {
  category?: string;
  keyword?: string;
}) {
  const res = await requestClient.get<any>('/auth/help/archives', { params });

  return (
    pickOne<HelpArchiveApi.ArchiveList>(res) ?? {
      canManage: false,
      categories: [],
      items: [],
    }
  );
}

export async function getArchive(id: string) {
  const res = await requestClient.get<any>(`/auth/help/archives/${id}`);
  return pickOne<HelpArchiveApi.Archive>(res);
}

export async function createArchive(data: HelpArchiveApi.SaveArchive) {
  return requestClient.post('/auth/help/archives', data);
}

export async function updateArchive(
  id: string,
  data: HelpArchiveApi.SaveArchive,
) {
  return requestClient.put(`/auth/help/archives/${id}`, data);
}

export async function deleteArchive(id: string) {
  return requestClient.delete(`/auth/help/archives/${id}`);
}
