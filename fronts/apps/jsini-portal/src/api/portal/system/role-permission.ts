import { requestClient } from '#/api/request';

export namespace SystemRolePermissionApi {
  export interface RoleUser {
    id: string;
    loginId: string;
    userName: string;
    email?: string;
    phone?: string;
    deptName?: string;
    companyName?: string;
  }

  export interface RoleMenu {
    menuId: string;
    menuName: string;
    parentId?: string;
    canView: boolean;
    canSearch: boolean;
    canCreate: boolean;
    canDelete: boolean;
    canUpdate: boolean;
    canPrint: boolean;
    canExcel: boolean;
    canCust1: boolean;
    canCust2: boolean;
    canCust3: boolean;
    canCust4: boolean;
    canCust5: boolean;
    canCust6: boolean;
    canCust7: boolean;
    canCust8: boolean;
  }
}

/**
 * 역할별 지정 사용자 목록 조회
 */
export async function getRoleUsers(roleId: string) {
  return requestClient.get<SystemRolePermissionApi.RoleUser[]>(
    `/auth/system/role-permission/roles/${roleId}/users`
  );
}

/**
 * 역할에 지정 가능한 사용자 목록 조회
 */
export async function getEligibleUsers(roleId: string) {
  return requestClient.get<SystemRolePermissionApi.RoleUser[]>(
    `/auth/system/role-permission/roles/${roleId}/eligible-users`
  );
}

/**
 * 역할에 사용자 지정
 */
export async function assignRoleUsers(roleId: string, accountIds: string[]) {
  return requestClient.post(
    `/auth/system/role-permission/roles/${roleId}/users/assign`,
    { accountIds }
  );
}

/**
 * 역할에서 사용자 제거
 */
export async function removeRoleUser(roleId: string, userId: string) {
  return requestClient.delete(
    `/auth/system/role-permission/roles/${roleId}/users/${userId}`
  );
}

/**
 * 역할의 메뉴 세부 권한 정보 조회
 */
export async function getRoleMenus(roleId: string) {
  return requestClient.get<SystemRolePermissionApi.RoleMenu[]>(
    `/auth/system/role-permission/roles/${roleId}/menus`
  );
}

/**
 * 역할의 메뉴 세부 권한 정보 일괄 저장
 */
export async function saveRoleMenus(roleId: string, data: Omit<SystemRolePermissionApi.RoleMenu, 'menuName' | 'parentId'>[]) {
  return requestClient.post(
    `/auth/system/role-permission/roles/${roleId}/menus/save`,
    data
  );
}
