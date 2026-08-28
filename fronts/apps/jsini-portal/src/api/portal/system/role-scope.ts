import { requestClient } from '#/api/request';

export namespace RoleScopeApi {
  /** 역할을 걸 수 있는 대상의 종류 */
  export type ScopeKind = 'account' | 'company' | 'department';

  /**
   * 조직 트리의 한 칸. 회사·부서·사람이 같은 모양이라 화면이 한 가지로 그린다.
   */
  export interface ScopeNode {
    /** 이 부서(또는 회사) 소속 사람 */
    accounts?: ScopeNode[];
    /** 하위 부서 */
    children?: ScopeNode[];
    id: string;
    kind: ScopeKind;
    loginId?: string;
    name: string;
    /** 이 대상에 **직접** 걸린 역할. 물려받은 것은 들어 있지 않다 */
    roleIds: string[];
  }

  /** 어떤 계정에 실제로 적용되는 역할과 그것이 온 단계 */
  export interface EffectiveRoles {
    roleIds: string[];
    roleNames: string[];
    /**
     * 역할 식별자 → 그 역할이 온 단계들 (`company` · `department` · `account`).
     * 한 역할이 여러 단계에 걸릴 수 있어 목록이다.
     */
    sources: Record<string, string[]>;
  }
}

/**
 * 응답 봉투에서 알맹이를 꺼낸다.
 *
 * 서버의 `ApiResponse` 는 **단일 객체도 `result` 배열에 담아** 보낸다
 * (`{ data: { result: [ {...} ], page } }`). 그래서 `result` 만 벗기면 배열이 나오고,
 * 화면이 `.company` 를 찾다가 undefined 가 된다. 배열이면 첫 칸까지 꺼낸다.
 */
function unwrap<T>(res: any): T {
  const inner = res?.result ?? res;
  return (Array.isArray(inner) ? inner[0] : inner) as T;
}

/** 회사 하나의 조직 트리와 각 단계에 걸린 역할 */
export async function getRoleScopeTree(companyId: string) {
  const res = await requestClient.get<any>('/auth/system/role-scope/tree', {
    params: { companyId },
  });
  return unwrap<{ company: RoleScopeApi.ScopeNode }>(res);
}

/** 대상에 역할을 건다. 이미 걸려 있으면 서버가 그대로 둔다. */
export async function assignRoleScope(
  kind: RoleScopeApi.ScopeKind,
  targetId: string,
  roleId: string,
) {
  return requestClient.post('/auth/system/role-scope/assign', {
    kind,
    roleId,
    targetId,
  });
}

/** 대상에서 역할을 푼다. 걸려 있지 않아도 오류가 아니다. */
export async function removeRoleScope(
  kind: RoleScopeApi.ScopeKind,
  targetId: string,
  roleId: string,
) {
  return requestClient.post('/auth/system/role-scope/remove', {
    kind,
    roleId,
    targetId,
  });
}

/** 그 계정에 실제로 적용되는 역할 (회사 &lt; 부서 &lt; 사람) */
export async function getEffectiveRoles(accountId: string) {
  const res = await requestClient.get<any>('/auth/system/role-scope/effective', {
    params: { accountId },
  });
  return unwrap<RoleScopeApi.EffectiveRoles>(res);
}

/** 왼쪽 사람 목록의 한 칸 */
export interface AccountPick {
  /**
   * 프로필 사진 주소. 올리지 않았으면 없다 — **없는 쪽이 흔하다.**
   * 화면은 이 값이 없으면 이름 첫 글자로 아바타를 그린다.
   */
  avatar?: null | string;
  companyId?: null | string;
  companyName?: null | string;
  departmentId?: null | string;
  departmentName?: null | string;
  id: string;
  loginId: string;
  name: string;
}

/** 메뉴 한 칸과 그 메뉴를 열어 준 역할 */
export interface AccountMenuItem {
  breadcrumb?: null | string;
  grantedBy: string[];
  id: string;
  path: string;
  title?: null | string;
  type: string;
}

/** 검색용 사람 목록 (회사·부서 이름 포함) */
export async function getRoleScopeAccounts() {
  const res = await requestClient.get<any>('/auth/system/role-scope/accounts');
  return (res?.result ?? res ?? []) as AccountPick[];
}

/** 그 계정이 볼 수 있는 메뉴와 볼 수 없는 메뉴 */
export async function getAccountMenuAccess(accountId: string) {
  const res = await requestClient.get<any>('/auth/system/role-scope/menus', {
    params: { accountId },
  });
  return unwrap<{
    assigned: AccountMenuItem[];
    unassigned: AccountMenuItem[];
  }>(res);
}
