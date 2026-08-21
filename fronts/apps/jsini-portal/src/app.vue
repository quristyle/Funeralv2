<script lang="ts" setup>
import { computed } from 'vue';

import { useAntdDesignTokens } from '@vben/hooks';
import { preferences, usePreferences } from '@vben/preferences';

import { App, ConfigProvider, theme } from 'ant-design-vue';

import { antdLocale } from '#/locales';

import NoticePopup from '#/components/notice/notice-popup.vue';

defineOptions({ name: 'App' });

const { isDark } = usePreferences();
const { tokens } = useAntdDesignTokens();

const tokenTheme = computed(() => {
  const algorithm = isDark.value
    ? [theme.darkAlgorithm]
    : [theme.defaultAlgorithm];

  // antd 컴팩트 모드 알고리즘
  if (preferences.app.compact) {
    algorithm.push(theme.compactAlgorithm);
  }

  return {
    algorithm,
    token: tokens,
  };
});
</script>

<template>
  <ConfigProvider :locale="antdLocale" :theme="tokenTheme">
    <App>
      <RouterView />
      <!--
        공지 팝업. 포털이 관리하는 공통 공지를 모든 화면 위에 띄운다.
        로그인 전에는 공개 공지만, 로그인하면 전체를 보여준다.
        로그인 화면에서도 떠야 하므로 라우터 바깥이 아니라 여기(앱 최상단)에 둔다.
      -->
      <NoticePopup />
    </App>
  </ConfigProvider>
</template>
