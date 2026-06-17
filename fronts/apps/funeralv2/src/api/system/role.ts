import type { Recordable } from '@vben/types';

import { requestClient } from '#/api/request';

export namespace SystemRoleApi {
  export interface SystemRole {
    [key: string]: any;
    id: string;
    name: string;
    permissions: string[];
    remark?: string;
    status: 0 | 1;
  }
}

/**
 * 역할 목록 데이터 가져오기
 */
async function getRoleList(params: Recordable<any>) {
  return requestClient.get<Array<SystemRoleApi.SystemRole>>(
    '/auth/system/role/list',
    { params },
  );
}

/**
 * 역할 생성
 * @param data 역할 데이터
 */
async function createRole(data: Omit<SystemRoleApi.SystemRole, 'id'>) {
  return requestClient.post('/auth/system/role', data);
}

/**
 * 역할 업데이트
 *
 * @param id 역할 ID
 * @param data 역할 데이터
 */
async function updateRole(
  id: string,
  data: Omit<SystemRoleApi.SystemRole, 'id'>,
) {
  return requestClient.put(`/auth/system/role/${id}`, data);
}

/**
 * 역할 삭제
 * @param id 역할 ID
 */
async function deleteRole(id: string) {
  return requestClient.delete(`/auth/system/role/${id}`);
}

export { createRole, deleteRole, getRoleList, updateRole };
