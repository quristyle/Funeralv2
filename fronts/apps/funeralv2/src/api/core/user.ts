import type { UserInfo } from '@vben/types';

import { requestClient, requestListClient } from '#/api/request';

/**
 * 사용자 정보 가져오기
 */
export async function getUserInfoApi() {
  try {
    const response = await requestClient.get<any>('/auth/user/info');
    console.log('check response', response);

    const userInfo = response?.result?.[0] ?? ({} as UserInfo);
    console.log('userInfo', userInfo);

    return userInfo;
  } catch (error) {
    console.warn('사용자정보 가져오기 에러', error);
    return {} as UserInfo;
  }
}

/**
 * 사용자 기본 프로필 수정
 */
export async function updateProfileApi(data: { realName?: string; introduction?: string; email?: string; phone?: string; avatar?: string }) {
  return requestClient.post('/auth/user/profile', data);
}

/**
 * 사용자 비밀번호 변경
 */
export async function changePasswordApi(data: any) {
  return requestClient.post('/auth/user/change-password', data);
}

/**
 * 사용자 설정(보안/알림) 업데이트
 */
export async function updateSettingApi(data: { fieldName: string; value: boolean }) {
  return requestClient.post('/auth/user/settings', data);
}
