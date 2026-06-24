import { requestClient } from '#/api/request';

export namespace SystemDeptApi {
  export interface SystemDept {
    [key: string]: any;
    children?: SystemDept[];
    id: string;
    name: string;
    companyId?: string;
    companyName?: string;
    remark?: string;
    status: 0 | 1;
  }
}

/**
 * 부서 목록 데이터 가져오기
 */
async function getDeptList(companyId?: string) {
  return requestClient.get<Array<SystemDeptApi.SystemDept>>(
    '/auth/system/dept/list',
    { params: { companyId } }
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

/**
 * 부서 소속 사용자 목록 가져오기
 * @param id 부서 ID
 */
async function getDeptUsers(id: string) {
  return requestClient.get<Array<any>>(`/auth/system/dept/${id}/users`);
}

/**
 * 부서 미지정 사용자 목록 가져오기 (배정용)
 * @param companyId 회사 ID
 */
async function getEligibleDeptUsers(companyId?: string) {
  return requestClient.get<Array<any>>('/auth/system/dept/eligible-users', {
    params: { companyId },
  });
}

/**
 * 특정 부서에 사용자 추가 등록 (일괄)
 * @param id 부서 ID
 * @param userIds 사용자 ID 목록
 */
async function assignDeptUsers(id: string, userIds: string[]) {
  return requestClient.post<boolean>(`/auth/system/dept/${id}/users`, userIds);
}

/**
 * 부서에서 사용자 소속 해제 (일괄)
 * @param userIds 사용자 ID 목록
 */
async function removeDeptUsers(userIds: string[]) {
  return requestClient.post<boolean>('/auth/system/dept/users/remove', userIds);
}

/**
 * 부서 위치 이동 (하위 부서 이동 포함)
 * @param id 부서 ID
 * @param parentId 상위 부서 ID
 */
async function moveDept(id: string, parentId?: string) {
  return requestClient.post<boolean>(`/auth/system/dept/${id}/move`, null, {
    params: { parentId },
  });
}

/**
 * 사용자 부서 이동
 * @param accountId 계정 ID
 * @param departmentId 이동할 부서 ID
 */
async function moveUserDept(accountId: string, departmentId?: string) {
  return requestClient.post<boolean>('/auth/system/dept/user/move', null, {
    params: { accountId, departmentId },
  });
}

export { createDept, deleteDept, getDeptList, updateDept, getDeptUsers, getEligibleDeptUsers, assignDeptUsers, removeDeptUsers, moveDept, moveUserDept };
