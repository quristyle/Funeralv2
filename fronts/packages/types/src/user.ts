import type { BasicUserInfo } from '@vben-core/typings';

/** 사용자 정보 */
interface UserInfo extends BasicUserInfo {
  /**
   * 사용자 설명
   */
  desc: string;
  /**
   * 홈페이지 주소
   */
  homePath: string;

  /**
   * accessToken
   */
  token: string;
}

export type { UserInfo };
