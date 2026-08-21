import { requestClient } from '#/api/request';

export namespace SystemRoleMappingApi {
  export interface RoleUserMapping {
    id: string;
    roleId: string;
    roleName: string;
    userId: string;
    userName: string;
    loginId: string;
    assignedAt: string;
  }

  export interface RoleMenuMapping {
    id: string;
    roleId: string;
    menuId: string;
    menuName: string;
    menuCode: string;
    permissions: string[];
  }
}

/**
 * 롤별 사용자 목록 조회 (롤사람)
 */
export async function getRoleUsers(roleId: string) {
  return requestClient.get<SystemRoleMappingApi.RoleUserMapping[]>(
    `/auth/system/role/${roleId}/users`,
  );
}

/**
 * 롤에 사용자 추가
 */
export async function assignRoleToUsers(roleId: string, userIds: string[]) {
  return requestClient.post(`/auth/system/role/${roleId}/users`, { userIds });
}

/**
 * 롤에서 사용자 제거
 */
export async function removeRoleFromUsers(roleId: string, userIds: string[]) {
  return requestClient.delete(`/auth/system/role/${roleId}/users`, { data: { userIds } });
}

/**
 * 사용자별 롤 목록 조회 (사람롤)
 */
export async function getUserRoles(userId: string) {
  return requestClient.get<SystemRoleMappingApi.RoleUserMapping[]>(
    `/auth/system/user/${userId}/roles`,
  );
}

/**
 * 사용자에 롤 지정
 */
export async function assignUserRoles(userId: string, roleIds: string[]) {
  return requestClient.post(`/auth/system/user/${userId}/roles`, { roleIds });
}

/**
 * 롤별 메뉴 목록 조회 (롤메뉴)
 */
export async function getRoleMenus(roleId: string) {
  return requestClient.get<SystemRoleMappingApi.RoleMenuMapping[]>(
    `/auth/system/role/${roleId}/menus`,
  );
}

/**
 * 롤에 메뉴 권한 저장
 */
export async function saveRoleMenus(roleId: string, mappings: Omit<SystemRoleMappingApi.RoleMenuMapping, 'id' | 'roleId'>[]) {
  return requestClient.post(`/auth/system/role/${roleId}/menus`, { mappings });
}

/**
 * 메뉴별 롤 목록 조회 (메뉴롤)
 */
export async function getMenuRoles(menuId: string) {
  return requestClient.get<SystemRoleMappingApi.RoleUserMapping[]>(
    `/auth/system/menu/${menuId}/roles`,
  );
}
