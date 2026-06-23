import type { Router } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { preferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';
import { startProgress, stopProgress } from '@vben/utils';

import { accessRoutes, coreRouteNames } from '#/router/routes';
import { useAuthStore } from '#/store';

import { generateAccess } from './access';

/**
 * 공통 가드 설정
 * @param router
 */
function setupCommonGuard(router: Router) {
  // 이미 로드된 페이지 기록
  const loadedPaths = new Set<string>();


console.log('routerrouterrouterrouterrouterrouter', router);



  router.beforeEach((to) => {
    to.meta.loaded = loadedPaths.has(to.path);

    // 페이지 로딩 진행 표시줄
    if (!to.meta.loaded && preferences.transition.progress) {
      startProgress();
    }
    return true;
  });

  router.afterEach((to) => {
    // 페이지 로드 여부 기록. 이미 로드된 경우 이후의 페이지 전환 애니메이션 등 효과를 반복 실행하지 않음
    loadedPaths.add(to.path);

    // 페이지 로딩 진행 표시줄 닫기
    if (preferences.transition.progress) {
      stopProgress();
    }

    // URL 및 컴포넌트 경로 로깅
    const componentPaths = to.matched
      .map((record) => {
        const components = record.components;
        if (!components) return `[Path: ${record.path} (컴포넌트 없음)]`;
        
        return Object.entries(components).map(([key, comp]: [string, any]) => {
          // 1. Vue 3 및 Vite 환경에서 실제 `.vue` 파일 물리적 경로 추출
          const file = comp.__file || comp.__vccOpts?.__file || comp.__asyncResolved?.__file;
          // 2. 컴포넌트의 내부 name 속성
          const compName = comp.name || comp.__vccOpts?.name || comp.__asyncResolved?.name;
          // 3. DB 원본 (존재할 경우)
          const dbComponent = record.meta?.component || (record as any).component;
          
          //return `\n  - 매치된 라우트: ${record.path}\n    파일 경로: ${file || '알 수 없음'}\n    컴포넌트명: ${compName || '알 수 없음'}\n    DB원본(meta): ${dbComponent || '알 수 없음'}`;
          return `\n  - 매치된 라우트: ${record.path}\n    컴포넌트명: ${compName || ''}\n    DB원본(meta): ${dbComponent || ''}`;
        }).join('');
      })
      .filter(Boolean)
      .join('');

    let logmessage = `\r\n\r\n[Router Log] URL: ${to.fullPath}\r\n`;
    if (to.name) {
      logmessage += `[Router Log] Route: ${String(to.name)}\r\n`;
    }
    logmessage += `[Router Log] Component: ${componentPaths}\r\n\r\n`;

    console.log(logmessage);

    // 만약 컴포넌트 정보가 여전히 부족하다면 아래 주석을 해제하여 라우트 객체를 상세히 살펴볼 수 있습니다.
     console.log('[Router Log] Matched Records Detail:', to.matched);

  });
}

/**
 * 권한 액세스 가드 설정
 * @param router
 */
function setupAccessGuard(router: Router) {
  router.beforeEach(async (to, from) => {
    const accessStore = useAccessStore();
    const userStore = useUserStore();
    const authStore = useAuthStore();
    // 기본 라우트, 이 라우트들은 권한 차단이 필요하지 않음
    if (coreRouteNames.includes(to.name as string)) {
      if (to.path === LOGIN_PATH && accessStore.accessToken) {
        return decodeURIComponent(
          (to.query?.redirect as string) ||
            userStore.userInfo?.homePath ||
            preferences.app.defaultHomePath,
        );
      }
      return true;
    }

    // accessToken 확인
    if (!accessStore.accessToken) {
      // 명시적으로 권한 액세스를 무시하도록 선언된 경우 액세스 가능
      if (to.meta.ignoreAccess) {
        return true;
      }

      // 액세스 권한이 없으면 로그인 페이지로 이동
      if (to.fullPath !== LOGIN_PATH) {
        return {
          path: LOGIN_PATH,
          // 필요하지 않은 경우 query 직접 삭제
          query:
            to.fullPath === preferences.app.defaultHomePath
              ? {}
              : { redirect: encodeURIComponent(to.fullPath) },
          // 현재 이동하려는 페이지를 소지하고, 로그인 후 해당 페이지로 다시 이동
          replace: true,
        };
      }
      return to;
    }

    // 동적 라우트가 이미 생성되었는지 확인
    if (accessStore.isAccessChecked) {
      return true;
    }

    // 라우트 테이블 생성
    // 현재 로그인한 사용자가 보유한 역할 식별자 목록
    const userInfo = userStore.userInfo || (await authStore.fetchUserInfo());
    const userRoles = userInfo?.roles ?? [];

    // 메뉴 및 라우트 생성
    const { accessibleMenus, accessibleRoutes } = await generateAccess({
      roles: userRoles,
      router,
      // 메뉴에는 표시되지만, 액세스 시 403으로 리다이렉트됨
      routes: accessRoutes,
    });

    // 메뉴 정보 및 라우트 정보 저장
    accessStore.setAccessMenus(accessibleMenus);
    accessStore.setAccessRoutes(accessibleRoutes);
    accessStore.setIsAccessChecked(true);
    let redirectPath: string;
    if (from.query.redirect) {
      redirectPath = from.query.redirect as string;
    } else if (to.fullPath === preferences.app.defaultHomePath) {
      redirectPath = preferences.app.defaultHomePath;
    } else if (userInfo?.homePath && to.fullPath === userInfo.homePath) {
      redirectPath = userInfo.homePath;
    } else {
      redirectPath = to.fullPath;
    }
    return {
      ...router.resolve(decodeURIComponent(redirectPath)),
      replace: true,
    };
  });
}

/**
 * 프로젝트 가드 설정
 * @param router
 */
function createRouterGuard(router: Router) {
  /** 공통 */
  setupCommonGuard(router);
  /** 권한 액세스 */
  setupAccessGuard(router);
}

export { createRouterGuard };
