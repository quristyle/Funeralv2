import { requestClient } from '#/api/request';

/**
 * 메뉴 기준 권한 현황 API (`/auth/menu-role` 화면).
 *
 * `/system/role-map` 은 **역할**에서 출발한다 — "이 역할은 어떤 메뉴를 쓰나".
 * 이쪽은 반대로 **"이 메뉴는 누가 쓸 수 있나"** 를 본다. 같은 데이터를 거꾸로 훑는다.
 *
 * **저장은 이 파일에 없다.** 이미 있는 경로를 그대로 쓴다 —
 * 같은 일을 하는 저장 경로를 두 개 만들면 한쪽에만 규칙이 붙는다.
 *   - 역할↔메뉴 권한 → `role-permission.ts` 의 `saveRoleMenus`
 *   - 역할↔회사·부서·사람 → `role-scope.ts` 의 `assignRoleScope` · `removeRoleScope`
 */
export namespace MenuRoleApi {
  /** 메뉴가 실제로 쓰는 권한 항목. 안 쓰는 항목은 켜도 효과가 없다. */
  export interface UsedPermission {
    view: boolean;
    search: boolean;
    create: boolean;
    update: boolean;
    delete: boolean;
    print: boolean;
    excel: boolean;
    cust1: boolean;
    cust2: boolean;
    cust3: boolean;
    cust4: boolean;
    cust5: boolean;
    cust6: boolean;
    cust7: boolean;
    cust8: boolean;
    cust1Name?: null | string;
    cust2Name?: null | string;
    cust3Name?: null | string;
    cust4Name?: null | string;
    cust5Name?: null | string;
    cust6Name?: null | string;
    cust7Name?: null | string;
    cust8Name?: null | string;
  }

  /** 역할 하나가 이 메뉴에 대해 가진 권한 */
  export interface RoleGrant {
    roleId: string;
    roleName: string;
    /** 이 역할에 이 메뉴 권한이 한 줄이라도 걸려 있는지 */
    granted: boolean;
    canView: boolean;
    canSearch: boolean;
    canCreate: boolean;
    canUpdate: boolean;
    canDelete: boolean;
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
    /** 이 역할이 걸린 대상 수. "이 역할을 끄면 몇이 영향받나" 를 알려 준다. */
    companyCount: number;
    departmentCount: number;
    accountCount: number;
  }

  /** 이 메뉴에 닿는 대상 하나 (회사 · 부서 · 사람) */
  export interface Target {
    id: string;
    name: string;
    /** 부서·사람이면 소속 회사명 */
    companyName?: null | string;
    /** 사람이면 로그인 아이디 */
    loginId?: null | string;
    /** 어느 역할 때문에 닿는지 */
    viaRoleNames: string[];
    viaRoleIds: string[];
    /** 이 대상에 딸린 사람 수. 사람이면 1 이다. */
    userCount: number;
  }

  /** 메뉴 하나의 권한 현황 */
  export interface MenuRole {
    menuId: string;
    menuName: string;
    menuPath?: null | string;
    used: UsedPermission;
    roles: RoleGrant[];
    companies: Target[];
    departments: Target[];
    accounts: Target[];
    /**
     * 실제 열람 가능 사용자 수.
     * 회사·부서·사람을 합쳐 사람 단위로 중복을 없앤 값이라
     * 목록 건수를 더한 값과 다를 수 있다.
     */
    effectiveUserCount: number;
  }
}

/** 단건 응답도 `{ result: [...] }` 로 감싸 오므로 하나를 꺼낸다. */
function pickOne<T>(res: any): T | undefined {
  const raw = res?.result ?? res?.data?.result ?? res;
  return (Array.isArray(raw) ? raw[0] : raw) as T | undefined;
}

/** 메뉴 하나의 권한 현황 */
export async function getMenuRole(menuId: string) {
  const res = await requestClient.get<any>(`/auth/system/menu-role/${menuId}`);
  return pickOne<MenuRoleApi.MenuRole>(res);
}
