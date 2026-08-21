import type { RouteRecordStringComponent } from '@vben/types';

import { requestClient } from '#/api/request';

/**
 * 로그인한 사용자가 한 메뉴에 대해 실제로 가진 권한.
 *
 * 서버가 이미 두 가지를 반영해서 내려준다.
 *  - 사용자가 속한 여러 역할의 권한을 OR 로 합친 값
 *  - 메뉴가 '사용하지 않는다'고 지정한 항목(system_menus.use_*)은 꺼진 값
 */
export interface MenuPermission {
  canCreate: boolean;
  canCust1: boolean;
  canCust2: boolean;
  canCust3: boolean;
  canCust4: boolean;
  canCust5: boolean;
  canCust6: boolean;
  canCust7: boolean;
  canCust8: boolean;
  canDelete: boolean;
  canExcel: boolean;
  canPrint: boolean;
  canSearch: boolean;
  canUpdate: boolean;
  canView: boolean;
  menuId: string;
  /** 메뉴의 라우트 경로. 화면이 자기 권한을 찾는 연결 고리다. */
  path: string;
}

/**
 * 메뉴별 권한 목록을 가져온다.
 *
 * 권한은 JSini 포털 한 곳에서만 관리한다. 장례식장·헬프데스크 등 각 MSA 화면도
 * 자체 권한을 두지 않고 이 결과를 따른다.
 */
export async function getMenuPermissionsApi() {
  const response = await requestClient.get<any>('/auth/menu/permissions');

  if (Array.isArray(response)) return response as MenuPermission[];
  if (Array.isArray(response?.result)) return response.result as MenuPermission[];
  if (Array.isArray(response?.data?.result)) {
    return response.data.result as MenuPermission[];
  }
  if (Array.isArray(response?.data)) return response.data as MenuPermission[];
  return [] as MenuPermission[];
}

/**
 * 사용자의 모든 메뉴 가져오기
 */
export async function getAllMenusApi() {
  try {
    const response = await requestClient.get<any>('/auth/menu/all');

    if (Array.isArray(response)) {
      return response as RouteRecordStringComponent[];
    }

    if (Array.isArray(response?.result)) {
      return response.result as RouteRecordStringComponent[];
    }

    if (Array.isArray(response?.data?.result)) {
      return response.data.result as RouteRecordStringComponent[];
    }

    if (Array.isArray(response?.data)) {
      return response.data as RouteRecordStringComponent[];
    }

    return [] as RouteRecordStringComponent[];
  } catch (error) {
    console.warn(`메뉴 가져오기 에러 - 서버 통신 실패로 점검 화면으로 이동합니다.`);
    
    // 현재 위치가 이미 점검 화면이 아닐 때만 이동 (무한 루프 방지)
    //if (window.location.pathname !== '/maintenance') {
    //  window.location.href = '/maintenance';
    //}
    
    return [] as RouteRecordStringComponent[];
  }
}
