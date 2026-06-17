import type { Recordable, UserInfo } from '@vben/types';

import { ref } from 'vue';
import { useRouter } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { preferences } from '@vben/preferences';
import { resetAllStores, useAccessStore, useUserStore } from '@vben/stores';

import { notification } from 'ant-design-vue';
import { defineStore } from 'pinia';

import { getAccessCodesApi, getUserInfoApi, loginApi, logoutApi } from '#/api';
import { $t } from '#/locales';

export const useAuthStore = defineStore('auth', () => {
  const accessStore = useAccessStore();
  const userStore = useUserStore();
  const router = useRouter();

  const loginLoading = ref(false);

  /**
   * 로그인 작업 비동기 처리
   * Asynchronously handle the login process
   * @param params 로그인 양식 데이터
   * @param onSuccess 성공 후 콜백 함수
   */
  async function authLogin(
    params: Recordable<any>,
    onSuccess?: () => Promise<void> | void,
  ) {
    // 사용자 로그인 작업을 비동기적으로 처리하고 accessToken을 가져옵니다.
    let userInfo: null | UserInfo = null;
    try {
      loginLoading.value = true;
      const { accessToken } = await loginApi(params);

      // accessToken을 성공적으로 가져온 경우
      if (accessToken) {
        accessStore.setAccessToken(accessToken);

        // 사용자 정보를 가져와서 accessStore에 저장합니다.
        const [fetchUserInfoResult, accessCodes] = await Promise.all([
          fetchUserInfo(),
          getAccessCodesApi(),
        ]);

        userInfo = fetchUserInfoResult;

        userStore.setUserInfo(userInfo);
        accessStore.setAccessCodes(accessCodes);

        if (accessStore.loginExpired) {
          accessStore.setLoginExpired(false);
        } else {
          onSuccess
            ? await onSuccess?.()
            : await router.push(
                userInfo.homePath || preferences.app.defaultHomePath,
              );
        }

        if (userInfo?.realName) {
          notification.success({
            description: `${$t('authentication.loginSuccessDesc')}:${userInfo?.realName}`,
            duration: 3,
            message: $t('authentication.loginSuccess'),
          });
        }
      }
    } finally {
      loginLoading.value = false;
    }

    return {
      userInfo,
    };
  }

  const isLoggingOut = ref(false); // 로그아웃 중임을 나타내는 플래그, /logout 무한 루프 방지용.

  async function logout(redirect: boolean = true) {
    if (isLoggingOut.value) return; // 로그아웃 중이면 이미 루프에 진입한 것이므로 즉시 반환합니다.
    isLoggingOut.value = true; // 플래그 설정

    try {
      await logoutApi();
    } catch {
      // 아무 처리도 하지 않음
    } finally {
      isLoggingOut.value = false; // 플래그 초기화

      resetAllStores();
      accessStore.setLoginExpired(false);
    }

    // 현재 라우트 주소를 포함하여 로그인 페이지로 이동
    await router.replace({
      path: LOGIN_PATH,
      query: redirect
        ? {
            redirect: encodeURIComponent(router.currentRoute.value.fullPath),
          }
        : {},
    });
  }

  async function fetchUserInfo() {
    const userInfo = await getUserInfoApi();
    userStore.setUserInfo(userInfo);
    return userInfo;
  }

  function $reset() {
    loginLoading.value = false;
  }

  return {
    $reset,
    authLogin,
    fetchUserInfo,
    loginLoading,
    logout,
  };
});
