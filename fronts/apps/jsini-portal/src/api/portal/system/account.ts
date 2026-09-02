import { unwrapList } from '#/api/envelope';
import { requestClient } from '#/api/request';

export namespace SystemAccountApi {
  export interface Account {
    id: string;
    loginId: string;
    userName: string;
    email?: string;
    phone?: string;
    status: 'ACTIVE' | 'LOCKED' | 'DISABLED';
    deptId?: string;
    deptName?: string;
    createdAt: string;
    roleIds?: string[];
    roleNames?: string[];
    /**
     * 프로필 사진 주소. 올리지 않았으면 없다 — **없는 쪽이 흔하다.**
     * 목록은 `utils/avatar` 로 썸네일 주소를 만들고, 없으면 이름 첫 글자를 그린다.
     */
    avatar?: null | string;
    /** 프로필 사진 파일 묶음. 사진을 고르는 화면에서 쓴다. */
    avatarGroupId?: null | string;
    /**
     * 생년월일 ('YYYY-MM-DD'). 생일 정본은 이 계정 테이블이다 (A안) —
     * 생활과환경 생일 화면(조회 전용)이 AuthServer 의 /auth/birthday/* 로 읽는다.
     */
    birthDate?: null | string;
    /** 생년월일이 음력인지 */
    birthDateIsLunar?: boolean;
    /** 생일 축하 표시 여부 — 끄면 생활과환경 생일 화면에 나오지 않는다 */
    birthdayCelebrated?: boolean;
  }
}

/**
 * 계정(사용자) 목록 조회
 */
export async function getAccounts(): Promise<SystemAccountApi.Account[]> {
  // 봉투 벗기는 기준은 `src/api/envelope.ts` 한 곳이다.
  return unwrapList<SystemAccountApi.Account>(
    await requestClient.get('/auth/system/account/list'),
  );
}

/**
 * 계정 생성
 */
export async function createAccount(data: Omit<SystemAccountApi.Account, 'id' | 'createdAt'>) {
  return requestClient.post('/auth/system/account', data);
}

/**
 * 계정 수정
 */
export async function updateAccount(id: string, data: Partial<Omit<SystemAccountApi.Account, 'id' | 'createdAt'>>) {
  return requestClient.put(`/auth/system/account/${id}`, data);
}

/**
 * 계정 삭제
 */
export async function deleteAccount(id: string) {
  return requestClient.delete(`/auth/system/account/${id}`);
}
