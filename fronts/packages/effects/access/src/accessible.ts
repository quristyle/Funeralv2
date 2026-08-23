import type { Component, DefineComponent } from 'vue';

import type {
  AccessModeType,
  GenerateMenuAndRoutesOptions,
  RouteRecordRaw,
} from '@vben/types';

import { defineComponent, h } from 'vue';

import {
  cloneDeep,
  generateMenus,
  generateRoutesByBackend,
  generateRoutesByFrontend,
  isFunction,
  isString,
  mapTree,
} from '@vben/utils';

async function generateAccessible(
  mode: AccessModeType,
  options: GenerateMenuAndRoutesOptions,
) {
  const { router } = options;

  options.routes = cloneDeep(options.routes);

  // 라우트 생성
  const accessibleRoutes = await generateRoutes(mode, options);

  const root = router.getRoutes().find((item) => item.path === '/');

  // 기존 라우트 이름 목록 가져오기
  const names = root?.children?.map((item) => item.name) ?? [];

  // router 인스턴스에 동적으로 추가
  accessibleRoutes.forEach((route) => {
    if (root && !route.meta?.noBasicLayout) {
      // 이전 버전과의 호환성을 위해, 하위 라우트가 포함된 경우 다중 BasicLayout이 나타나지 않도록 component를 제거합니다.
      // 프로젝트가 이번 수정사항을 반영하여 모든 사용자 정의 메뉴 최상위의 BasicLayout을 제거했다면, 이 if 코드를 삭제할 수 있습니다.
      if (route.children && route.children.length > 0) {
        delete route.component;
      }
      // router name을 기준으로 라우트가 이미 존재하는 경우 추가하지 않습니다.
      if (names?.includes(route.name)) {
        // 이미 존재하는 라우트 인덱스를 찾아 업데이트합니다. 업데이트하지 않으면 사용자 전환 시 1단계 디렉토리가 업데이트되지 않아 homePath가 2단계 디렉토리에 있을 때 404 문제가 발생할 수 있습니다.
        const index = root.children?.findIndex(
          (item) => item.name === route.name,
        );
        if (index !== undefined && index !== -1 && root.children) {
          root.children[index] = route;
        }
      } else {
        root.children?.push(route);
      }
    } else {
      router.addRoute(route);
    }
  });

  if (root) {
    if (root.name) {
      router.removeRoute(root.name);
    }
    router.addRoute(root);
  }

  // 메뉴 생성
  const accessibleMenus = generateMenus(accessibleRoutes, options.router);

  return { accessibleMenus, accessibleRoutes };
}

/**
 * 라우트 생성
 * @param mode
 * @param options
 */
async function generateRoutes(
  mode: AccessModeType,
  options: GenerateMenuAndRoutesOptions,
) {
  const { forbiddenComponent, roles, routes } = options;

  let resultRoutes: RouteRecordRaw[] = routes;
  switch (mode) {
    case 'backend': {
      resultRoutes = await generateRoutesByBackend(options);
      break;
    }
    case 'frontend': {
      resultRoutes = await generateRoutesByFrontend(
        routes,
        roles || [],
        forbiddenComponent,
      );
      break;
    }
    case 'mixed': {
      const [frontend_resultRoutes, backend_resultRoutes] = await Promise.all([
        generateRoutesByFrontend(routes, roles || [], forbiddenComponent),
        generateRoutesByBackend(options),
      ]);
      resultRoutes = mergeRoutesByName(
        backend_resultRoutes,
        frontend_resultRoutes,
      );
      break;
    }
  }

  /**
   * 라우트 트리를 조정하여 다음 처리를 수행합니다:
   * 1. redirect가 추가되지 않은 라우트에 redirect 추가
   * 2. 지연 로딩(lazy loading)된 컴포넌트 이름을 현재 라우트 이름으로 수정합니다 (keep-alive가 활성화된 경우)
   */
  resultRoutes = mapTree(resultRoutes, (route, parent) => {
    // 重新包装component，使用与路由名称相同的name以支持keep-alive的条件缓存。
    if (
      route.meta?.keepAlive &&
      isFunction(route.component) &&
      route.name &&
      isString(route.name)
    ) {
      const originalComponent = route.component as () => Promise<{
        default: Component | DefineComponent;
      }>;
      route.component = async () => {
        const component = await originalComponent();
        if (!component.default) return component;
        return defineComponent({
          name: route.name as string,
          setup(props, { attrs, slots }) {
            return () => h(component.default, { ...props, ...attrs }, slots);
          },
        });
      };
    }

    // redirect가 있거나 하위 라우트가 없으면 바로 반환합니다.
    if (route.redirect || !route.children || route.children.length === 0) {
      return route;
    }
    const firstChild = route.children[0];

    if (!firstChild?.path || firstChild.path.startsWith('/')) {
      return route;
    }

    // fork 定制：如果第一个子路由是动态路由（如 :id），说明当前路由本身是一个
    // “列表+详情”页面（渲染自身组件），不应自动重定向到未填充的动态参数，
    // 否则地址栏会出现字面量 ":id" 或匹配失败导致 404。
    // 详见对上游重构 commit f00a8812 的修复。
    if (firstChild.path.startsWith(':')) {
      return route;
    }

    // 拼接子路由的重定向绝对路径。
    // - 当 parent.redirect 为字符串时，它已经是累计好的绝对路径，直接替换最后一段
    //   即可正确支持任意层级的深层嵌套（如 /demos/nested/menu2/menu2-1）。
    // - fork 定制：后端菜单可能传入对象形式的 redirect（如 { name }），无法 split，
    //   此时回退到使用 parent.path 拼接（这类 parent 为顶级路由，path 为绝对路径）。
    if (parent && parent.redirect && isString(parent.redirect)) {
      const parentSplit = parent.redirect.split('/');
      parentSplit.splice(-1, 2, route.path, firstChild.path);
      const redirectPath = parentSplit.join('/');
      route.redirect = redirectPath;
    } else if (parent && parent.redirect) {
      route.redirect = `${parent.path}/${route.path}/${firstChild.path}`;
    } else {
      route.redirect = `${route.path}/${firstChild.path}`;
    }

    return route;
  });

  return resultRoutes;
}

/**
 * name을 기반으로 프론트엔드와 백엔드 라우트 병합
 * @param baseRoutes 백엔드 라우트
 * @param extraRoutes 프론트엔드 라우트
 */
function mergeRoutesByName(
  baseRoutes: RouteRecordRaw[],
  extraRoutes: RouteRecordRaw[],
): RouteRecordRaw[] {
  const result: RouteRecordRaw[] = [];
  const routeMap = new Map<string, RouteRecordRaw>();

  for (const route of baseRoutes) {
    const clone = { ...route } as RouteRecordRaw;
    result.push(clone);
    if (clone.name && isString(clone.name)) {
      routeMap.set(clone.name as string, clone);
    }
  }

  for (const route of extraRoutes) {
    if (
      route.name &&
      isString(route.name) &&
      routeMap.has(route.name as string)
    ) {
      const existing = routeMap.get(route.name as string);
      if (!existing) {
        continue;
      }
      const existingChildren = existing.children ?? [];
      const routeChildren = route.children ?? [];

      const merged = {
        ...route,
        ...existing, // 백엔드를 기본으로 유지
        meta: {
          ...route.meta,
          ...existing.meta, // 충돌 시 백엔드 meta 우선
        },
      } as RouteRecordRaw;

      if (existingChildren.length > 0 || routeChildren.length > 0) {
        merged.children = mergeRoutesByName(existingChildren, routeChildren);
      }

      Object.assign(existing, merged);
    } else {
      const clone = { ...route } as RouteRecordRaw;
      result.push(clone);
      if (clone.name && isString(clone.name)) {
        routeMap.set(clone.name as string, clone);
      }
    }
  }

  return result;
}

export { generateAccessible };
