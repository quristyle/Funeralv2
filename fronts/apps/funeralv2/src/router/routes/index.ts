import type { RouteRecordRaw } from 'vue-router';

import { mergeRouteModules, traverseTreeValues } from '@vben/utils';

import { coreRoutes, fallbackNotFoundRoute } from './core';

// const dynamicRouteFiles = import.meta.glob('./modules/**/*.ts', {
//   eager: true,
// });

// 필요한 경우 직접 주석을 해제하고 폴더를 생성할 수 있습니다.
// const externalRouteFiles = import.meta.glob('./external/**/*.ts', { eager: true });
// const staticRouteFiles = import.meta.glob('./static/**/*.ts', { eager: true });

/** 동적 라우트 */
// const dynamicRoutes: RouteRecordRaw[] = mergeRouteModules(dynamicRouteFiles);
const dynamicRoutes: RouteRecordRaw[] = [];

/** 외부 라우트 목록, 이 페이지들은 Layout 없이 접근할 수 있으며 다른 시스템에 내장될 때 사용될 수 있습니다(메뉴에 표시되지 않음). */
// const externalRoutes: RouteRecordRaw[] = mergeRouteModules(externalRouteFiles);
// const staticRoutes: RouteRecordRaw[] = mergeRouteModules(staticRouteFiles);
const staticRoutes: RouteRecordRaw[] = [];
const externalRoutes: RouteRecordRaw[] = [];

/** 라우트 목록, 기본 라우트, 외부 라우트 및 404 기본 라우트로 구성됩니다.
 *  권한 검증이 필요 없습니다(항상 메뉴에 표시됩니다). */
const routes: RouteRecordRaw[] = [
  ...coreRoutes,
  ...externalRoutes,
  fallbackNotFoundRoute,
];

/** 기본 라우트 목록, 이 라우트들은 권한 차단에 걸리지 않습니다. */
const coreRouteNames = traverseTreeValues(coreRoutes, (route) => route.name);

/** 권한 검증이 필요한 라우트 목록, 동적 라우트와 정적 라우트를 포함합니다. */
const accessRoutes = [...dynamicRoutes, ...staticRoutes];

const componentKeys: string[] = Object.keys(
  import.meta.glob('../../views/**/*.vue'),
)
  .filter((item) => !item.includes('/modules/'))
  .map((v) => {
    const path = v.replace('../../views/', '/');
    return path.endsWith('.vue') ? path.slice(0, -4) : path;
  });

export { accessRoutes, componentKeys, coreRouteNames, routes };
