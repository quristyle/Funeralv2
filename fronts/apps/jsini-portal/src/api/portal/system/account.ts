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
  }
}

/**
 * 계정(사용자) 목록 조회
 */
export async function getAccounts(): Promise<SystemAccountApi.Account[]> {
  // AuthServer 의 응답 필터는 목록을 `{ result: [...], page: { total } }` 로 감싼다.
  // requestClient 는 봉투의 `data` 까지만 벗기므로 여기서 배열을 꺼내야 한다.
  // 이걸 하지 않으면 호출부가 객체를 배열로 알고 `.map()` 을 불러 화면이 빈 채로 멈춘다.
  const res = await requestClient.get<any>('/auth/system/account/list');

  if (Array.isArray(res)) return res;
  if (Array.isArray(res?.result)) return res.result;
  if (Array.isArray(res?.data?.result)) return res.data.result;
  if (Array.isArray(res?.data)) return res.data;
  return [];
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
