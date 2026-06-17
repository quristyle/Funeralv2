import type { UserInfo } from '@vben/types';

import { requestClient } from '#/api/request';

/**
 * 사용자 정보 가져오기
 */
export async function getUserInfoApi() {
  //return await requestClient.get<UserInfo>('/auth/user/info');

  try {
    return await requestClient.get<UserInfo>(`/auth/user/info`);
  } catch (error) {
    console.warn(`사용자정보 가져오기 에러`);
    return {} as UserInfo;
  }

}
