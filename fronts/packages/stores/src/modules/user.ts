import { acceptHMRUpdate, defineStore } from 'pinia';

interface BasicUserInfo {
  [key: string]: any;
  /**
   * 아바타
   */
  avatar: string;
  /**
   * 사용자 닉네임
   */
  realName: string;
  /**
   * 사용자 역할
   */
  roles?: string[];
  /**
   * 사용자 ID
   */
  userId: string;
  /**
   * 사용자 이름
   */
  username: string;
}

interface AccessState {
  /**
   * 사용자 정보
   */
  userInfo: BasicUserInfo | null;
  /**
   * 사용자 역할
   */
  userRoles: string[];
}

/**
 * @ko_KR 사용자 정보 관련
 */
export const useUserStore = defineStore('core-user', {
  actions: {
    setUserInfo(userInfo: BasicUserInfo | null) {
      // 사용자 정보 설정
      this.userInfo = userInfo;
      // 역할 정보 설정
      const roles = userInfo?.roles ?? [];
      this.setUserRoles(roles);
    },
    setUserRoles(roles: string[]) {
      this.userRoles = roles;
    },
  },
  state: (): AccessState => ({
    userInfo: null,
    userRoles: [],
  }),
});

// HMR(Hot Module Replacement) 이슈 해결
const hot = import.meta.hot;
if (hot) {
  hot.accept(acceptHMRUpdate(useUserStore, hot));
}
