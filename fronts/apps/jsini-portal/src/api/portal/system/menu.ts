import type { Recordable } from '@vben/types';

import { requestClient } from '#/api/request';

export namespace SystemMenuApi {
  /** 배지 색상 집합 */
  export const BadgeVariants = [
    'default',
    'destructive',
    'primary',
    'success',
    'warning',
  ] as const;
  /** 배지 유형 집합 */
  export const BadgeTypes = ['dot', 'normal'] as const;
  /** 메뉴 유형 집합 */
  export const MenuTypes = [
    'CATALOG',
    'MENU',
    'EMBEDDED',
    'LINK',
    'BUTTON',
  ] as const;
  /**
   * 메뉴가 사용하는 권한 항목 설정.
   *
   * role_menus 는 메뉴마다 권한 칸을 15개 들고 있지만 메뉴마다 쓰는 항목은 다르다.
   * 여기서 켠 항목만 역할 권한 화면에 나타나고, 사용자 정의 1~8 은 붙인 이름으로 보인다.
   */
  export interface MenuPermissionItems {
    cust1Name?: null | string;
    cust2Name?: null | string;
    cust3Name?: null | string;
    cust4Name?: null | string;
    cust5Name?: null | string;
    cust6Name?: null | string;
    cust7Name?: null | string;
    cust8Name?: null | string;
    useCreate: boolean;
    useCust1: boolean;
    useCust2: boolean;
    useCust3: boolean;
    useCust4: boolean;
    useCust5: boolean;
    useCust6: boolean;
    useCust7: boolean;
    useCust8: boolean;
    useDelete: boolean;
    useExcel: boolean;
    usePrint: boolean;
    useSearch: boolean;
    useUpdate: boolean;
    useView: boolean;
  }

  /** 새 메뉴의 기본 권한 항목. 기본 7종은 켜고 사용자 정의는 꺼 둔다. */
  export function defaultPermissionItems(): MenuPermissionItems {
    return {
      cust1Name: '',
      cust2Name: '',
      cust3Name: '',
      cust4Name: '',
      cust5Name: '',
      cust6Name: '',
      cust7Name: '',
      cust8Name: '',
      useCreate: true,
      useCust1: false,
      useCust2: false,
      useCust3: false,
      useCust4: false,
      useCust5: false,
      useCust6: false,
      useCust7: false,
      useCust8: false,
      useDelete: true,
      useExcel: true,
      usePrint: true,
      useSearch: true,
      useUpdate: true,
      useView: true,
    };
  }

  /** 시스템 메뉴 */
  export interface SystemMenu {
    [key: string]: any;
    /** 백엔드 권한 식별자 */
    authCode: string;
    /** 하위 수준 */
    children?: SystemMenu[];
    /** 컴포넌트 */
    component?: string;
    /** 메뉴 ID */
    id: string;
    /** 메뉴 메타데이터 */
    meta?: {
      /** 활성화 시 표시되는 아이콘 */
      activeIcon?: string;
      /** 라우트일 때 활성화해야 하는 메뉴의 경로 */
      activePath?: string;
      /** 탭 표시줄에 고정 */
      affixTab?: boolean;
      /** 탭 표시줄 고정 순서 */
      affixTabOrder?: number;
      /** 배지 내용 (배지 유형이 normal일 때 유효) */
      badge?: string;
      /** 배지 유형 */
      badgeType?: (typeof BadgeTypes)[number];
      /** 배지 색상 */
      badgeVariants?: (typeof BadgeVariants)[number];
      /** 메뉴에서 하위 항목 숨기기 */
      hideChildrenInMenu?: boolean;
      /** 브레드크럼에서 숨기기 */
      hideInBreadcrumb?: boolean;
      /** 메뉴에서 숨기기 */
      hideInMenu?: boolean;
      /** 탭 표시줄에서 숨기기 */
      hideInTab?: boolean;
      /** 메뉴 아이콘 */
      icon?: string;
      /** 내장 Iframe URL */
      iframeSrc?: string;
      /** 페이지 캐시 여부 */
      keepAlive?: boolean;
      /** 외부 링크 페이지 URL */
      link?: string;
      /** 동일 라우트에서 최대로 열 수 있는 탭 수 */
      maxNumOfOpenTab?: number;
      /** 기본 레이아웃 불필요 */
      noBasicLayout?: boolean;
      /** 새 창에서 열기 여부 */
      openInNewWindow?: boolean;
      /** 메뉴 정렬 */
      order?: number;
      /** 추가 라우트 파라미터 */
      query?: Recordable<any>;
      /** 메뉴 제목 */
      title?: string;
      /**
       * 휴대폰 크기(<768px) 메뉴목록에 보일지 여부.
       *
       * 끄면 그 크기의 **메뉴목록에서만** 빠진다 — 라우트는 그대로라
       * 주소·즐겨찾기·탭으로는 열린다(`status: 0` 과 다른 뜻이다).
       */
      useMobile?: boolean;
      /** 태블릿 크기(768~1023px) 메뉴목록에 보일지 여부. `useMobile` 과 같은 규칙이다. */
      useTablet?: boolean;
    };
    /** 메뉴 이름 */
    name: string;
    /** 라우트 경로 */
    path: string;
    /** 이 메뉴가 사용하는 권한 항목 */
    permissions?: MenuPermissionItems;
    /** 부모 ID */
    pid: string;
    /** 리다이렉트 */
    redirect?: string;
    /**
     * 메뉴 사용 상태 (0: 비활성, 1: 활성).
     * 비활성 메뉴는 사이드바 조회 API 가 내려주지 않아 라우트도 만들어지지 않는다.
     */
    status: number;
    /** 메뉴 유형 */
    type: (typeof MenuTypes)[number];
  }
}

/**
 * 메뉴 데이터 목록 가져오기
 */
async function getMenuList(): Promise<Array<SystemMenuApi.SystemMenu>> {
  const response = await requestClient.get<any>(
    '/auth/system/menu/list',
  );
    return response;
}

async function isMenuNameExists(
  name: string,
  id?: SystemMenuApi.SystemMenu['id'],
): Promise<boolean> {
  const res = await requestClient.get<any>('/auth/system/menu/name-exists', {
    params: { id, name },
  });
  const result = res?.result ?? res;
  if (Array.isArray(result)) {
    return result[0] === true;
  }
  return result === true;
}

async function isMenuPathExists(
  path: string,
  id?: SystemMenuApi.SystemMenu['id'],
): Promise<boolean> {
  const res = await requestClient.get<any>('/auth/system/menu/path-exists', {
    params: { id, path },
  });
  const result = res?.result ?? res;
  if (Array.isArray(result)) {
    return result[0] === true;
  }
  return result === true;
}

/**
 * 메뉴 생성
 * @param data 메뉴 데이터
 */
async function createMenu(
  data: Omit<SystemMenuApi.SystemMenu, 'children' | 'id'>,
) {
  return requestClient.post('/auth/system/menu', data);
}

/**
 * 메뉴 업데이트
 *
 * @param id 메뉴 ID
 * @param data 메뉴 데이터
 */
async function updateMenu(
  id: string,
  data: Omit<SystemMenuApi.SystemMenu, 'children' | 'id'>,
) {
  return requestClient.put(`/auth/system/menu/${id}`, data);
}

/**
 * 메뉴 삭제
 * @param id 메뉴 ID
 */
async function deleteMenu(id: string) {
  return requestClient.delete(`/auth/system/menu/${id}`);
}

/**
 * 메뉴 이동 및 순서 변경
 * @param menuId 이동할 메뉴 ID
 * @param newParentId 새 부모 ID
 * @param newOrderNo 새 순서
 */
async function moveMenu(menuId: string, newParentId: string | null, newOrderNo: number) {
  return requestClient.post('/auth/system/menu/move', { menuId, newParentId, newOrderNo });
}

/**
 * 메뉴 위치·순서 일괄 저장
 *
 * 트리에서 한 번 드래그하면 옮긴 노드뿐 아니라 형제들의 순번도 함께 밀린다.
 * 화면이 확정한 배치를 그대로 보내 한 번의 왕복으로 저장한다.
 *
 * @param items 변경된 메뉴들의 `{ id, pid, orderNo }`
 */
async function reorderMenus(
  items: { id: string; orderNo: number; pid: null | string }[],
) {
  return requestClient.post('/auth/system/menu/reorder', items);
}

export {
  createMenu,
  deleteMenu,
  getMenuList,
  isMenuNameExists,
  isMenuPathExists,
  moveMenu,
  reorderMenus,
  updateMenu,
};
