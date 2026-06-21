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
    };
    /** 메뉴 이름 */
    name: string;
    /** 라우트 경로 */
    path: string;
    /** 부모 ID */
    pid: string;
    /** 리다이렉트 */
    redirect?: string;
    /** 메뉴 유형 */
    type: (typeof MenuTypes)[number];
  }
}

/**
 * 메뉴 데이터 목록 가져오기
 */
async function getMenuList() {
  return requestClient.get<Array<SystemMenuApi.SystemMenu>>(
    '/auth/system/menu/list',
  );
}

async function isMenuNameExists(
  name: string,
  id?: SystemMenuApi.SystemMenu['id'],
) {
  return requestClient.get<boolean>('/auth/system/menu/name-exists', {
    params: { id, name },
  });
}

async function isMenuPathExists(
  path: string,
  id?: SystemMenuApi.SystemMenu['id'],
) {
  return requestClient.get<boolean>('/auth/system/menu/path-exists', {
    params: { id, path },
  });
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

export {
  createMenu,
  deleteMenu,
  getMenuList,
  isMenuNameExists,
  isMenuPathExists,
  moveMenu,
  updateMenu,
};
