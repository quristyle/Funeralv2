<script lang="ts" setup>
/**
 * [환경설정] — `/setting/environment`
 *
 * 헤더 톱니를 눌러 나오는 드로어와 **같은 설정을 같은 구현으로** 다룬다.
 * 내용은 `PreferencesView` 한 컴포넌트가 전부 갖고 있다(vben 레이아웃 패키지).
 * 설정 항목이 늘어도 이 화면은 고칠 일이 없다.
 *
 * 톱니는 그대로 남겨 두었다. 이 화면은 메뉴라서 열람 권한(`scom.role_menus`)이
 * 걸리는데, 권한 없는 역할이 생기면 그 사람은 자기 테마조차 못 바꾸게 된다.
 * 톱니가 있으면 그 경우에도 길이 남는다.
 *
 * 설정은 계정에 붙어 서버에 저장된다(`store/preferences-sync.ts`).
 * 다른 PC 에서 로그인해도 따라온다.
 */
import { Page } from '@vben/common-ui';
import { PreferencesView } from '@vben/layouts';

import { useAuthStore } from '#/store';

const authStore = useAuthStore();

/**
 * '캐시 삭제 및 로그아웃'.
 * 레이아웃(`layouts/basic.vue`)이 드로어에서 같은 일을 하는 방식과 맞춘다.
 */
function handleClearPreferencesAndLogout() {
  authStore.logout(false);
}
</script>

<template>
  <Page auto-content-height>
    <PreferencesView
      @clear-preferences-and-logout="handleClearPreferencesAndLogout"
    />
  </Page>
</template>
