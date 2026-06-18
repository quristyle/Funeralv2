import type {
  ComponentRecordType,
  GenerateMenuAndRoutesOptions,
} from '@vben/types';

import { generateAccessible } from '@vben/access';
import { preferences } from '@vben/preferences';

import { message } from 'ant-design-vue';

import { getAllMenusApi } from '#/api';
import { BasicLayout, IFrameView } from '#/layouts';
import { $t } from '#/locales';

const forbiddenComponent = () => import('#/views/_core/fallback/forbidden.vue');

async function generateAccess(options: GenerateMenuAndRoutesOptions) {
  const globMap: ComponentRecordType = import.meta.glob('../views/**/*.vue');
  const pageMap: ComponentRecordType = {};

  Object.keys(globMap).forEach((key) => {
    // '../views/dashboard/analytics/index.vue' -> '#/views/dashboard/analytics/index.vue'
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

export { generateAccess };
