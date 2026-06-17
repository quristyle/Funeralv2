import type { RouteRecordRaw } from 'vue-router';

import type { MenuRecordRaw } from '@vben-core/typings';

import { acceptHMRUpdate, defineStore } from 'pinia';

type AccessToken = null | string;

interface AccessState {
  /**
   * 권한 코드
   */
  accessCodes: string[];
  /**
   * 접근 가능한 메뉴 목록
   */
  accessMenus: MenuRecordRaw[];
  /**
   * 접근 가능한 라우트 목록
   */
  accessRoutes: RouteRecordRaw[];
  /**
   * 로그인 액세스 토큰
   */
  accessToken: AccessToken;
  /**
   * 권한 확인 여부
   */
  isAccessChecked: boolean;
  /**
   * 잠금 화면 상태 여부
   */
  isLockScreen: boolean;
  /**
   * 잠금 화면 비밀번호
   */
  lockScreenPassword?: string;
  /**
   * 로그인 만료 여부
   */
  loginExpired: boolean;
  /**
   * 로그인 액세스 토큰
   */
  refreshToken: AccessToken;
}

/**
 * @ko_KR 접근 권한 관련
 */
export const useAccessStore = defineStore('core-access', {
  actions: {
    getMenuByPath(path: string) {
      function findMenu(
        menus: MenuRecordRaw[],
        path: string,
      ): MenuRecordRaw | undefined {
        for (const menu of menus) {
          if (menu.path === path) {
            return menu;
          }
          if (menu.children) {
            const matched = findMenu(menu.children, path);
            if (matched) {
              return matched;
            }
          }
        }
      }
      return findMenu(this.accessMenus, path);
    },
    lockScreen(password: string) {
      this.isLockScreen = true;
      this.lockScreenPassword = password;
    },
    setAccessCodes(codes: string[]) {
      this.accessCodes = codes;
    },
    setAccessMenus(menus: MenuRecordRaw[]) {
      this.accessMenus = menus;
    },
    setAccessRoutes(routes: RouteRecordRaw[]) {
      this.accessRoutes = routes;
    },
    setAccessToken(token: AccessToken) {
      this.accessToken = token;
    },
    setIsAccessChecked(isAccessChecked: boolean) {
      this.isAccessChecked = isAccessChecked;
    },
    setLoginExpired(loginExpired: boolean) {
      this.loginExpired = loginExpired;
    },
    setRefreshToken(token: AccessToken) {
      this.refreshToken = token;
    },
    unlockScreen() {
      this.isLockScreen = false;
      this.lockScreenPassword = undefined;
    },
  },
  persist: {
    // 지속성(Persistence)
    pick: [
      'accessToken',
      'refreshToken',
      'accessCodes',
      'isLockScreen',
      'lockScreenPassword',
    ],
  },
  state: (): AccessState => ({
    accessCodes: [],
    accessMenus: [],
    accessRoutes: [],
    accessToken: null,
    isAccessChecked: false,
    isLockScreen: false,
    lockScreenPassword: undefined,
    loginExpired: false,
    refreshToken: null,
  }),
});

// HMR(Hot Module Replacement) 이슈 해결
const hot = import.meta.hot;
if (hot) {
  hot.accept(acceptHMRUpdate(useAccessStore, hot));
}
