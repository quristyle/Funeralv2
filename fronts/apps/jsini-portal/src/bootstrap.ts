import { createApp, watchEffect } from 'vue';

import { registerAccessDirective } from '@vben/access';
import { registerLoadingDirective } from '@vben/common-ui';
import { providePluginsOptions } from '@vben/plugins';
import { preferences } from '@vben/preferences';
import { initStores } from '@vben/stores';
import '@vben/styles';
import '@vben/styles/antd';
import './styles/index.css';

import { useTitle } from '@vueuse/core';

import { $tIfKey, setupI18n } from '#/locales';
import { router } from '#/router';

import { initComponentAdapter } from './adapter/component';
import { initSetupVbenForm, useVbenForm } from './adapter/form';
import App from './app.vue';
import { setupDraggableModal } from './plugins/draggable-modal';
import { initTimezone } from './timezone-init';

async function bootstrap(namespace: string) {
  // [준수사항 3] ant 모달을 헤더로 끌어 옮길 수 있게 만든다.
  // vben 모달은 부품 기본값(draggable)으로 이미 걸려 있다.
  setupDraggableModal();

  // 컴포넌트 어댑터 초기화
  await initComponentAdapter();

  // 폼 컴포넌트 초기화
  await initSetupVbenForm();

  // 플러그인 전역 설정 주입
  providePluginsOptions({
    form: { useVbenForm },
  });

  // 모달 기본 설정
  // setDefaultModalProps({
  //   fullscreenButton: false,
  // });
  // Drawer 기본 설정
  // setDefaultDrawerProps({
  //   zIndex: 1020,
  // });

  const app = createApp(App);

  // v-loading 디렉티브 등록
  registerLoadingDirective(app, {
    loading: 'loading', // 여기에서 디렉티브 이름을 사용자 정의하거나, false를 명시하여 이 디렉티브를 등록하지 않을 수 있습니다.
    spinning: 'spinning',
  });

  // pinia-store 설정 (i18n보다 먼저 호출하여 API 호출 시 스토어 접근 가능하게 함)
  await initStores(app, { namespace });

  // DB 메타데이터 기반 BizSelect 설정 프리로드
  try {
    const { useBizSelectStore } = await import('#/store/biz-select-config');
    const bizSelectStore = useBizSelectStore();
    await bizSelectStore.loadConfigs();
  } catch (error) {
    console.error('Failed to preload BizSelect configs during bootstrap:', error);
  }

  // 국제화 i18n 설정
  await setupI18n(app);

  // 타임존 HANDLER 초기화
  initTimezone();

  // 권한 디렉티브 설치
  registerAccessDirective(app);

  // tippy 초기화
  const { initTippy } = await import('@vben/common-ui/es/tippy');
  initTippy(app);

  // 메뉴 권한 디렉티브(v-perm) 등록.
  // 권한은 JSini 포털 한 곳에서 관리하고 모든 MSA 화면이 이 결과를 따른다.
  const { setupPermDirective } = await import('#/directives/perm');
  setupPermDirective(app);

  // 라우터 및 라우터 가드 설정
  app.use(router);

  // @tanstack/vue-query 설정
  const { VueQueryPlugin } = await import('@tanstack/vue-query');
  app.use(VueQueryPlugin);

  // Motion 플러그인 설정
  const { MotionPlugin } = await import('@vben/plugins/motion');
  app.use(MotionPlugin);

  // 동적으로 제목 업데이트
  watchEffect(() => {
    if (preferences.app.dynamicTitle) {
      const routeTitle = router.currentRoute.value.meta?.title;
      const pageTitle =
        (routeTitle ? `${$tIfKey(routeTitle)} - ` : '') + preferences.app.name;
      useTitle(pageTitle);
    }
  });

  app.mount('#app');
}

export { bootstrap };
