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
    sortOrder?: number;
    status: 0 | 1;
    /** 이 부서에 **직접** 소속된 사용자 수 (하위 부서 제외) */
    userCount?: number;
    /**
     * 하위 부서까지 합친 사용자 수.
     * 트리를 접어 둔 상태에서 조직 전체 인원을 보여 줄 때 쓴다.
     */
    totalUserCount?: number;
  }
}

/**
 * 부서 목록 데이터 가져오기
 */
/**
 * 부서 목록 (트리)
 *
 * @param companyId 조회할 회사. **비우면 '전체' 가 아니라 '로그인한 사람의 회사'** 다
 *                  (서버가 그렇게 좁힌다). 전체를 보려면 `allCompanies` 를 쓴다.
 * @param allCompanies 모든 회사의 부서를 함께 받을지
 */
async function getDeptList(companyId?: string, allCompanies?: boolean) {
  return requestClient.get<Array<SystemDeptApi.SystemDept>>(
    '/auth/system/dept/list',
    { params: { companyId, allCompanies } }
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
