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
    roles?: string[];
    roleNames?: string;
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

    // ── 이 메뉴가 쓰는 권한 항목 (읽기 전용) ────────────────
    //
    // 실제 값은 메뉴 관리 화면에서 정한다(system_menus).
    // 권한 화면은 이 값을 보고 쓰지 않는 항목의 체크박스를 잠그고,
    // 사용자 정의 1~8 은 붙인 이름으로 열 제목을 만든다.
    cust1Name?: null | string;
    cust2Name?: null | string;
    cust3Name?: null | string;
    cust4Name?: null | string;
    cust5Name?: null | string;
    cust6Name?: null | string;
    cust7Name?: null | string;
    cust8Name?: null | string;
    useCreate?: boolean;
    useCust1?: boolean;
    useCust2?: boolean;
    useCust3?: boolean;
    useCust4?: boolean;
    useCust5?: boolean;
    useCust6?: boolean;
    useCust7?: boolean;
    useCust8?: boolean;
    useDelete?: boolean;
    useExcel?: boolean;
    usePrint?: boolean;
    useSearch?: boolean;
    useUpdate?: boolean;
    useView?: boolean;
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
