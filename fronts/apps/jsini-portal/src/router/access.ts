import type { Router } from 'vue-router';

import type {
  ComponentRecordType,
  GenerateMenuAndRoutesOptions,
} from '@vben/types';

import { generateAccessible } from '@vben/access';
import { preferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';

import { message } from 'ant-design-vue';

import { getAllMenusApi } from '#/api';
import { BasicLayout, IFrameView } from '#/layouts';
import { $t } from '#/locales';
import { accessRoutes } from '#/router/routes';
import { useMenuPermissionStore } from '#/store/menu-permission';

import { collectMenuRules, setVisibleMenus } from './menu-visibility';

const forbiddenComponent = () => import('#/views/_core/fallback/forbidden.vue');

async function generateAccess(options: GenerateMenuAndRoutesOptions) {
  const globMap: ComponentRecordType = import.meta.glob('../views/**/*.vue');
  const pageMap: ComponentRecordType = {};

  Object.keys(globMap).forEach((key) => {
    // '../views/portal/dashboard/analytics/index.vue' -> '#/views/portal/dashboard/analytics/index.vue'
    const componentPath = key.replace('../views/', '#/views/');
    pageMap[componentPath] = globMap[key];
  });

  const layoutMap: ComponentRecordType = {
    BasicLayout,
    IFrameView,
  };

  return await generateAccessible(preferences.app.accessMode, {
    ...options,
    fetchMenuListAsync: async () => {
      message.loading({
        content: `${$t('common.loadingMenu')}...`,
        duration: 1.5,
      });
      const menus = await getAllMenusApi();

      // 사이드바 거르기에 쓸 것(meta.type · useMobile · useTablet)을 여기서 걷어 둔다.
      // 라우트·메뉴로 옮겨지는 과정에서 meta 의 이 값들은 떨어져 나가기 때문이다.
      collectMenuRules(menus);

      // 재귀적으로 메뉴를 순회하며 컴포넌트 경로 및 이름 중복 검증
      const usedNames = new Set<string>();
      const sanitizeMenus = (menuList: any[]) => {
        menuList.forEach((menu) => {
          // 1. 이름 중복 검증 (name 또는 title 사용)
          const menuName = menu.name || menu.title;
          if (menuName && usedNames.has(menuName)) {
            console.warn(`[Router Access] 중복된 메뉴 이름 발견: ${menuName}, 라우트 등록에서 제외합니다.`);
            menu.name = ''; // 중복 발생 시 name을 비워 라우트 중복 에러 방지
            menu.component = ''; // component도 함께 비워 등록 방지
          } else if (menuName) {
            usedNames.add(menuName);
          }

          // 2. 컴포넌트 경로 검증
          if (menu.component && !pageMap[menu.component] && menu.component !== 'BasicLayout' && menu.component !== 'IFrameView') {
            console.warn(`[Router Access] 존재하지 않는 컴포넌트 경로: ${menu.component}, 라우트 등록에서 제외합니다.`);
            menu.component = ''; // 컴포넌트 경로를 비워 라우트 등록 방지
          }
          
          if (menu.children && menu.children.length > 0) {
            sanitizeMenus(menu.children);
          }
        });
      };
      
      sanitizeMenus(menus);
      return menus;
    },
    // 권한이 없을 경우 403 페이지로 이동하도록 지정할 수 있습니다.
    forbiddenComponent,
    // route.meta.menuVisibleWithForbidden = true 인 경우
    layoutMap,
    pageMap,
  });
}

/**
 * 메뉴를 다시 읽어 사이드바와 라우트를 갱신한다. **화면을 새로 열지 않는다.**
 *
 * 메뉴는 백엔드 주도(`accessMode: 'backend'`)라 [메뉴 관리]에서 고친 내용이
 * 화면에 반영되려면 `/auth/menu/all` 을 다시 읽어 라우트를 재구성해야 한다.
 * 그 일을 하는 곳은 원래 라우터 가드 한 곳뿐이었고, 가드는 `isAccessChecked`
 * 가 false 일 때만 돈다. 그래서 예전 리로드 버튼은 `window.location.reload()`
 * 로 페이지를 새로 열어 그 플래그를 초기화하는 방식이었다 —
 * 결과는 맞지만 앱 전체가 다시 뜨고 열린 탭·스크롤·입력 중이던 값이 날아간다.
 *
 * 여기서는 가드가 하던 일을 그대로 떼어내 그 자리에서 수행한다.
 * `generateAccessible` 은 같은 이름의 라우트를 덮어쓰고 새 이름은 추가하도록
 * 만들어져 있어 여러 번 불러도 안전하다.
 *
 * @param router 지금 쓰는 라우터 인스턴스
 * @returns 새로 만든 메뉴와 라우트
 */
async function refreshAccessMenus(router: Router) {
  const accessStore = useAccessStore();
  const userStore = useUserStore();
  const permissionStore = useMenuPermissionStore();

  // 갱신 전 최상위 라우트 이름. 아래에서 사라진 것을 골라내는 데 쓴다.
  const previousNames = new Set(
    (accessStore.accessRoutes ?? [])
      .map((route) => route.name)
      .filter(Boolean) as string[],
  );

  const { accessibleMenus, accessibleRoutes } = await generateAccess({
    roles: userStore.userInfo?.roles ?? [],
    router,
    routes: accessRoutes,
  });

  // 메뉴에서 지워진 화면은 라우트도 걷어낸다.
  // `generateAccessible` 은 추가·갱신만 하고 삭제는 하지 않기 때문이다.
  const currentNames = new Set(
    accessibleRoutes.map((route) => route.name).filter(Boolean) as string[],
  );
  previousNames.forEach((name) => {
    if (!currentNames.has(name) && router.hasRoute(name)) {
      router.removeRoute(name);
    }
  });

  // 화면별 동작 권한(v-perm · useMenuPermission)도 같은 표에서 온다.
  // 메뉴가 바뀌면 권한도 바뀌었을 가능성이 크므로 강제로 다시 읽는다.
  //
  // **메뉴를 넣기 전에 읽는다.** 사이드바가 이 권한으로 걸러지기 때문이다 —
  // 나중에 읽으면 걸러지지 않은 목록이 한 번 보였다가 바뀐다.
  await permissionStore.load(true).catch(() => undefined);

  // 열람 권한과 화면 크기로 걸러 사이드바에 넣는다(라우트는 그대로 둔다).
  setVisibleMenus(
    accessibleMenus,
    (menus) => accessStore.setAccessMenus(menus),
    permissionStore.canViewMenu,
  );
  accessStore.setAccessRoutes(accessibleRoutes);
  accessStore.setIsAccessChecked(true);

  return { accessibleMenus, accessibleRoutes };
}

export { generateAccess, refreshAccessMenus };
