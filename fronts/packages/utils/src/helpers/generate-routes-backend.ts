import type { RouteRecordRaw } from 'vue-router';

import type {
  ComponentRecordType,
  GenerateMenuAndRoutesOptions,
  RouteRecordStringComponent,
} from '@vben-core/typings';

import { mapTree } from '@vben-core/shared/utils';

/**
 * 라우트가 메뉴에 표시되지만 접근 시 403을 표시할지 여부를 판단합니다(사용자가 기능을 인지하고 권한을 신청하도록 유도).
 */
function menuHasVisibleWithForbidden(route: RouteRecordRaw): boolean {
  return !!route.meta?.menuVisibleWithForbidden;
}

/**
 * 동적으로 라우트 생성 - 백엔드 방식
 * meta.menuVisibleWithForbidden이 true인 항목은 403 컴포넌트로 직접 교체하여 사용자가 기능을 인지하고 권한을 신청하도록 합니다.
 */
async function generateRoutesByBackend(
  options: GenerateMenuAndRoutesOptions,
): Promise<RouteRecordRaw[]> {
  const {
    fetchMenuListAsync,
    layoutMap = {},
    pageMap = {},
    forbiddenComponent,
  } = options;

  try {
    const menuRoutes = await fetchMenuListAsync?.();
    if (!menuRoutes) {
      return [];
    }

    const normalizePageMap: ComponentRecordType = {};

    for (const [key, value] of Object.entries(pageMap)) {
      normalizePageMap[normalizeViewPath(key)] = value;
    }

    let routes = convertRoutes(menuRoutes, layoutMap, normalizePageMap);

    if (forbiddenComponent) {
      routes = mapTree(routes, (route) => {
        if (menuHasVisibleWithForbidden(route)) {
          route.component = forbiddenComponent;
        }
        return route;
      });
    }

    return routes;
  } catch (error) {
    console.error(error);
    throw error;
  }
}

function convertRoutes(
  routes: RouteRecordStringComponent[],
  layoutMap: ComponentRecordType,
  pageMap: ComponentRecordType,
): RouteRecordRaw[] {
  return mapTree(routes, (node) => {
    const route = node as unknown as RouteRecordRaw;
    const { component, name } = node;

    if (!name) {
      console.error('route name is required', route);
    }

    // layout 변환
    if (component && layoutMap[component]) {
      route.component = layoutMap[component];
      // 페이지 컴포넌트 변환
    } else if (component) {
      const normalizePath = normalizeViewPath(component);
      const pageKey = normalizePath.endsWith('.vue')
        ? normalizePath
        : `${normalizePath}.vue`;
      if (pageMap[pageKey]) {
        route.component = pageMap[pageKey];
      } else {
        console.error(`route component is invalid: ${pageKey}`, route);
        route.component = pageMap['/_core/fallback/not-found.vue'];
      }
    }

    return route;
  });
}

function normalizeViewPath(path: string): string {
  // 상대 경로 접두사 제거
  const normalizedPath = path.replace(/^(\.\/|\.\.\/)+/, '');

  // 경로가 '/'로 시작하는지 확인
  const viewPath = normalizedPath.startsWith('/')
    ? normalizedPath
    : `/${normalizedPath}`;

  // 여기는 vben-admin의 디렉토리 구조와 결합되어 있습니다.
  return viewPath.replace(/^\/views/, '');
}
export { generateRoutesByBackend };
