import type { Router, RouteRecordName, RouteRecordRaw } from 'vue-router';

import { traverseTreeValues } from '@vben-core/shared/utils';

/**
 * @zh_CN 지정된 화이트리스트를 제외한 모든 라우트를 재설정합니다.
 */
export function resetStaticRoutes(router: Router, routes: RouteRecordRaw[]) {
  // 정적 라우트의 모든 노드(자식 노드 포함)의 name을 가져오고, name 필드가 없는 라우트는 제외합니다.
  const staticRouteNames = traverseTreeValues<
    RouteRecordRaw,
    RouteRecordName | undefined
  >(routes, (route) => {
    // 라우트 재설정 시 name이 지정되지 않은 라우트를 삭제할 수 없는 경우를 방지하기 위해, 이러한 라우트에는 name을 지정해야 합니다.
    if (!route.name) {
      console.warn(
        `The route with the path ${route.path} needs to have the field name specified.`,
      );
    }
    return route.name;
  });

  const { getRoutes, hasRoute, removeRoute } = router;
  const allRoutes = getRoutes();
  allRoutes.forEach(({ name }) => {
    // 라우팅 테이블에 존재하고 화이트리스트에 없는 경우에만 삭제해야 합니다.
    if (name && !staticRouteNames.includes(name) && hasRoute(name)) {
      removeRoute(name);
    }
  });
}
