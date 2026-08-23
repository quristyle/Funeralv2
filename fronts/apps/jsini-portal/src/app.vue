<script lang="ts" setup>
import { computed, watchEffect } from 'vue';

import { useAntdDesignTokens } from '@vben/hooks';
import { preferences, usePreferences } from '@vben/preferences';

import { App, ConfigProvider, theme } from 'ant-design-vue';

import { antdLocale } from '#/locales';
import { resolveFontFamily } from '#/styles/font';

import NoticePopup from '#/components/notice/notice-popup.vue';

defineOptions({ name: 'App' });

const { isDark } = usePreferences();
const { tokens } = useAntdDesignTokens();

/**
 * 사용자가 환경설정에서 고른 글꼴.
 *
 * 고른 값은 열쇠일 뿐이고 실제 글꼴 목록은 `styles/font.ts` 가 정한다.
 */
const fontFamily = computed(() => resolveFontFamily(preferences.app.fontFamily));

/**
 * CSS 변수에 반영한다.
 *
 * `styles/index.css` 가 `:root` 에 기본값을 적어 두었지만, 환경설정으로 바꾼 값은
 * 그보다 뒤에 와야 한다. 인라인 스타일로 얹으면 항상 이긴다.
 */
watchEffect(() => {
  document.documentElement.style.setProperty('--font-family', fontFamily.value);
});

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
    token: {
      ...tokens,
      // [준수사항 5] 글꼴은 S-CoreDream 이 최우선이다.
      //
      // ant-design-vue 는 CSS-in-JS 로 거의 모든 컴포넌트 규칙에 자기 font-family 를 넣는다.
      // body 에만 글꼴을 걸어 두면 화면의 26,000개 요소가 전부 antd 기본값으로 그려진다.
      // 토큰을 바꿔야 antd 가 만들어 내는 규칙까지 함께 바뀐다.
      fontFamily: fontFamily.value,
    },
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
