import { requestClient } from '#/api/request';

export namespace SystemDeptApi {
  export interface SystemDept {
    [key: string]: any;
    children?: SystemDept[];
    id: string;
    name: string;
    remark?: string;
    status: 0 | 1;
  }
}

/**
 * 부서 목록 데이터 가져오기
 */
async function getDeptList() {
  return requestClient.get<Array<SystemDeptApi.SystemDept>>(
    '/auth/system/dept/list',
  );
}

/**
 * 부서 생성
 * @param data 부서 데이터
 */
async function createDept(
  data: Omit<SystemDeptApi.SystemDept, 'children' | 'id'>,
) {
  return requestClient.post('/auth/system/dept', data);
}

/**
 * 부서 업데이트
 *
 * @param id 부서 ID
 * @param data 부서 데이터
 */
async function updateDept(
  id: string,
  data: Omit<SystemDeptApi.SystemDept, 'children' | 'id'>,
) {
  return requestClient.put(`/auth/system/dept/${id}`, data);
}

/**
 * 부서 삭제
 * @param id 부서 ID
 */
async function deleteDept(id: string) {
  return requestClient.delete(`/auth/system/dept/${id}`);
}

export { createDept, deleteDept, getDeptList, updateDept };
