import type { VNode } from 'vue';
import type {
  RouteLocationNormalizedLoaded,
  RouteLocationNormalizedLoadedGeneric,
} from 'vue-router';

import { computed } from 'vue';

import { preferences, usePreferences } from '@vben/preferences';

/**
 * 컴포넌트 변환, name 자동 추가
 * @param component
 * @param route
 */
export function transformComponent(
  component: VNode,
  route: RouteLocationNormalizedLoadedGeneric,
) {
  // 컴포넌트 뷰를 찾을 수 없습니다. 폴백 뷰가 설정되어 있으면 폴백 뷰를 반환하고, 그렇지 않으면 오류를 발생시킵니다.
  if (!component) {
    console.error(
      'Component view not found，please check the route configuration',
    );
    return undefined;
  }

  const routeName = route.name as string;
  // 컴포넌트에 name이 없으면 그대로 반환
  if (!routeName) {
    return component;
  }
  const componentName = (component?.type as any)?.name;

  // 이미 name이 설정되어 있으면 그대로 반환
  if (componentName) {
    return component;
  }

  // componentName과 routeName이 일치하면 그대로 반환
  if (componentName === routeName) {
    return component;
  }

  // name 설정
  component.type ||= {};
  (component.type as any).name = routeName;

  return component;
}

/**
 * Layout 관련 hook
 */
export function useLayoutHook() {
  const { keepAlive } = usePreferences();
  /**
   * 애니메이션 사용 여부
   */
  const getEnabledTransition = computed(() => {
    const { transition } = preferences;
    const transitionName = transition.name;
    return transitionName && transition.enable;
  });

  /**
   * 라우트 전환 애니메이션 가져오기
   * @param _route
   */
  function getTransitionName(_route: RouteLocationNormalizedLoaded) {
    // 환경 설정이 되어 있지 않으면 애니메이션을 사용하지 않음
    const { tabbar, transition } = preferences;
    const transitionName = transition.name;
    if (!transitionName || !transition.enable) {
      return;
    }

    // 탭 바가 비활성화되어 있거나 캐시가 켜져 있지 않으면 전역 설정 애니메이션 사용
    if (!tabbar.enable || !keepAlive) {
      return transitionName;
    }

    // 페이지가 이미 로드된 경우 애니메이션을 사용하지 않음
    // if (route.meta.loaded) {
    //   return;
    // }
    // 이미 열려 있고 로드된 페이지는 애니메이션을 사용하지 않음
    // const inTabs = getCachedTabs.value.includes(route.name as string);

    // return inTabs && route.meta.loaded ? undefined : transitionName;
    return transitionName;
  }

  return {
    getEnabledTransition,
    getTransitionName,
  };
}
