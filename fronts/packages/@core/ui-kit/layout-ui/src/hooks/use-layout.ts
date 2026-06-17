import type { LayoutType } from '@vben-core/typings';

import type { VbenLayoutProps } from '../vben-layout';

import { computed } from 'vue';

export function useLayout(props: VbenLayoutProps) {
  const currentLayout = computed(() =>
    props.isMobile ? 'sidebar-nav' : (props.layout as LayoutType),
  );

  /**
   * 콘텐츠를 전체 화면으로 표시할지 여부 (사이드, 하단, 상단, 탭 영역 제외)
   */
  const isFullContent = computed(() => currentLayout.value === 'full-content');

  /**
   * 사이드 혼합 모드 여부
   */
  const isSidebarMixedNav = computed(
    () => currentLayout.value === 'sidebar-mixed-nav',
  );

  /**
   * 헤더 내비게이션 모드 여부
   */
  const isHeaderNav = computed(() => currentLayout.value === 'header-nav');

  /**
   * 혼합 내비게이션 모드 여부
   */
  const isMixedNav = computed(
    () =>
      currentLayout.value === 'mixed-nav' ||
      currentLayout.value === 'header-sidebar-nav',
  );

  /**
   * 헤더 혼합 모드 여부
   */
  const isHeaderMixedNav = computed(
    () => currentLayout.value === 'header-mixed-nav',
  );

  return {
    currentLayout,
    isFullContent,
    isHeaderMixedNav,
    isHeaderNav,
    isMixedNav,
    isSidebarMixedNav,
  };
}
