import type { Router, RouteRecordRaw } from 'vue-router';

import type {
  ExRouteRecordRaw,
  MenuRecordRaw,
  RouteMeta,
} from '@vben-core/typings';

import { filterTree, mapTree, sortTree } from '@vben-core/shared/utils';

/**
 * routes에 따라 메뉴 목록을 생성합니다.
 * @param routes - 라우트 설정 목록
 * @param router - Vue Router 인스턴스
 * @returns 생성된 메뉴 목록
 */
function generateMenus(
  routes: RouteRecordRaw[],
  router: Router,
): MenuRecordRaw[] {
  // 라우트 목록을 name을 키로 하는 객체 맵으로 변환합니다.
  const finalRoutesMap: { [key: string]: string } = Object.fromEntries(
    router.getRoutes().map(({ name, path }) => [name, path]),
  );

  let menus = mapTree<ExRouteRecordRaw, MenuRecordRaw>(routes, (route) => {
    // 최종 라우트 경로를 가져옵니다.
    const path = finalRoutesMap[route.name as string] ?? route.path ?? '';

    const {
      meta = {} as RouteMeta,
      name: routeName,
      redirect,
      children = [],
    } = route;
    const {
      activeIcon,
      badge,
      badgeType,
      badgeVariants,
      hideChildrenInMenu = false,
      icon,
      link,
      order,
      title = '',
      query,
    } = meta;

    // 메뉴 이름이 비어 있지 않은지 확인합니다.
    const name = (title || routeName || '') as string;

    // 하위 메뉴 처리
    const resultChildren = hideChildrenInMenu
      ? []
      : ((children as MenuRecordRaw[]) ?? []);

    // 하위 메뉴의 부모-자식 관계를 설정합니다.
    if (resultChildren.length > 0) {
      resultChildren.forEach((child) => {
        child.parents = [...(route.parents ?? []), path];
        child.parent = path;
      });
    }

    // 최종 경로를 결정합니다.
    const resultPath = hideChildrenInMenu ? redirect || path : link || path;

    return {
      activeIcon,
      badge,
      badgeType,
      badgeVariants,
      icon,
      name,
      query,
      order,
      parent: route.parent,
      parents: route.parents,
      path: resultPath,
      show: !meta.hideInMenu,
      children: resultChildren,
    };
  });

  // order=0일 때 999로 대체되는 문제를 피하기 위해 메뉴를 정렬합니다.
  menus = sortTree(menus, (a, b) => (a?.order ?? 999) - (b?.order ?? 999));

  // 숨겨진 메뉴 항목을 필터링합니다.
  return filterTree(menus, (menu) => !!menu.show);
}

export { generateMenus };
