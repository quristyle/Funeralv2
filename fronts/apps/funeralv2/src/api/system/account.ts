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
  }
}

/**
 * 계정(사용자) 목록 조회
 */
export async function getAccounts() {
   var aaa = await requestClient.get<SystemAccountApi.Account[]>('/auth/system/account/list');
console.log('aaaaaaaaaaaaaaaaaaaa', aaa);
   return aaa;
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
