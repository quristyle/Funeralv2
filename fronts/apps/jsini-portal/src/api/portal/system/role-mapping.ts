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

}

// 롤사람(/auth/role-user) · 롤메뉴(/auth/role-menu) 는 '역할 관리'(/system/role-map) 와
// 중복이라 없앴다. 역할에 사용자를 배정하고 메뉴 권한을 주는 일은 role-permission.ts 를 쓴다.

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
 * 메뉴별 롤 목록 조회 (메뉴롤)
 */
export async function getMenuRoles(menuId: string) {
  return requestClient.get<SystemRoleMappingApi.RoleUserMapping[]>(
    `/auth/system/menu/${menuId}/roles`,
  );
}
