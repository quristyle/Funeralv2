import type { RouteRecordStringComponent } from '@vben/types';

import { requestClient } from '#/api/request';

/**
 * 사용자의 모든 메뉴 가져오기
 */
export async function getAllMenusApi() {
  try{
  return await requestClient.get<RouteRecordStringComponent[]>('/auth/menu/all');
  }catch(error){
    console.warn(`메뉴 가져오기 에러 - 서버 통신 실패로 점검 화면으로 이동합니다.`);
    
    // 현재 위치가 이미 점검 화면이 아닐 때만 이동 (무한 루프 방지)
    //if (window.location.pathname !== '/maintenance') {
    //  window.location.href = '/maintenance';
    //}
    
    return [] as RouteRecordStringComponent[];
  }
}
