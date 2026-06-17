import type { RouteRecordRaw } from 'vue-router';

// 모듈 타입 정의
interface RouteModuleType {
  default: RouteRecordRaw[];
}

/**
 * 동적 라우트 모듈의 기본 내보내기를 병합합니다.
 * @param routeModules 동적으로 가져온 라우트 모듈 객체
 * @returns 병합된 라우트 설정 배열
 */
function mergeRouteModules(
  routeModules: Record<string, unknown>,
): RouteRecordRaw[] {
  const mergedRoutes: RouteRecordRaw[] = [];

  for (const routeModule of Object.values(routeModules)) {
    const moduleRoutes = (routeModule as RouteModuleType)?.default ?? [];
    mergedRoutes.push(...moduleRoutes);
  }

  return mergedRoutes;
}

export { mergeRouteModules };

export type { RouteModuleType };
