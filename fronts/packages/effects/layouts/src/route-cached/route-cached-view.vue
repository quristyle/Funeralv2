<script setup lang="ts">
import { computed, unref, watch } from 'vue';
import { useRoute } from 'vue-router';

import { preferences } from '@vben/preferences';
import { getTabKey, storeToRefs, useTabbarStore } from '@vben/stores';

import { transformComponent, useLayoutHook } from '../hooks';

const route = useRoute();

const tabbarStore = useTabbarStore();

const { getTabs, getCachedRoutes, getExcludeCachedTabs } =
  storeToRefs(tabbarStore);
const { removeCachedRoute } = tabbarStore;

const { getEnabledTransition, getTransitionName } = useLayoutHook();

/**
 * 탭 활성화 여부
 */
const enableTabbar = computed(() => preferences.tabbar.enable);

const computedCachedRouteKeys = computed(() => {
  if (!unref(enableTabbar)) {
    return [];
  }
  return unref(getTabs)
    .filter((item) => item.meta.domCached)
    .map((item) => getTabKey(item));
});

/**
 * 캐시된 라우트 변화 감시 및 존재하지 않는 캐시 라우트 삭제
 */
watch(computedCachedRouteKeys, (keys) => {
  unref(getCachedRoutes).forEach((item) => {
    if (!keys.includes(item.key)) {
      removeCachedRoute(item.key);
    }
  });
});

/**
 * 모든 캐시된 라우트
 */
const computedCachedRoutes = computed(() => {
  if (!unref(enableTabbar)) {
    return [];
  }
  // 라우트 새로고침으로 캐시 갱신 가능
  const excludeCachedTabKeys = unref(getExcludeCachedTabs);
  return [...unref(getCachedRoutes).values()].filter((item) => {
    const componentType: any = item.component.type || {};
    let componentName = componentType.name;
    if (!componentName) {
      componentName = item.route.name;
    }
    return !excludeCachedTabKeys.includes(componentName);
  });
});

/**
 * 표시 여부
 */
const computedShowView = computed(() => unref(computedCachedRoutes).length > 0);

const computedCurrentRouteKey = computed(() => {
  return getTabKey(route);
});
</script>

<template>
  <template v-if="computedShowView">
    <template v-for="item in computedCachedRoutes" :key="item.key">
      <Transition
        v-if="getEnabledTransition"
        appear
        mode="out-in"
        :name="getTransitionName(item.route)"
      >
        <component
          v-show="item.key === computedCurrentRouteKey"
          :is="transformComponent(item.component, item.route)"
        />
      </Transition>
      <template v-else>
        <component
          v-show="item.key === computedCurrentRouteKey"
          :is="transformComponent(item.component, item.route)"
        />
      </template>
    </template>
  </template>
</template>

<style scoped></style>
