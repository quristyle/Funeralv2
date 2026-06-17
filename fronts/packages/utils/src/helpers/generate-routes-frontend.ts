import type { RouteRecordRaw } from 'vue-router';

import { filterTree, mapTree } from '@vben-core/shared/utils';

/**
 * 동적으로 라우트 생성 - 프론트엔드 방식
 */
async function generateRoutesByFrontend(
  routes: RouteRecordRaw[],
  roles: string[],
  forbiddenComponent?: RouteRecordRaw['component'],
): Promise<RouteRecordRaw[]> {
  // 역할 식별자에 따라 라우팅 테이블을 필터링하여 현재 사용자에게 지정된 권한이 있는지 확인합니다.
  const finalRoutes = filterTree(routes, (route) => {
    return hasAuthority(route, roles);
  });

  if (!forbiddenComponent) {
    return finalRoutes;
  }

  // 접근이 금지된 페이지가 있는 경우, 해당 페이지를 403 페이지로 교체합니다.
  return mapTree(finalRoutes, (route) => {
    if (menuHasVisibleWithForbidden(route)) {
      route.component = forbiddenComponent;
    }
    return route;
  });
}

/**
 * 라우트에 접근할 권한이 있는지 확인합니다.
 * @param route
 * @param access 역할
 */
function hasAuthority(route: RouteRecordRaw, access: string[]) {
  const authority = route.meta?.authority;
  if (!authority) {
    return true;
  }
  const canAccess = access.some((value) => authority.includes(value));

  return canAccess || (!canAccess && menuHasVisibleWithForbidden(route));
}

/**
 * 라우트가 메뉴에 표시되지만 접근 시 403으로 리디렉션되는지 확인합니다.
 * @param route
 */
function menuHasVisibleWithForbidden(route: RouteRecordRaw) {
  return (
    !!route.meta?.authority &&
    Reflect.has(route.meta || {}, 'menuVisibleWithForbidden') &&
    !!route.meta?.menuVisibleWithForbidden
  );
}

export { generateRoutesByFrontend, hasAuthority };
