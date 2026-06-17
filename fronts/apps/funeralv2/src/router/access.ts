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
      return await getAllMenusApi();
    },
    // 권한이 없을 경우 403 페이지로 이동하도록 지정할 수 있습니다.
    forbiddenComponent,
    // route.meta.menuVisibleWithForbidden = true 인 경우
    layoutMap,
    pageMap,
  });
}

export { generateAccess };
