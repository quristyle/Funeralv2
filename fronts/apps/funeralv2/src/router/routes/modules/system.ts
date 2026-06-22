import type { RouteRecordRaw } from 'vue-router';

import { $t } from '#/locales';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      icon: 'ion:settings-outline',
      order: 9997,
      title: $t('system.title'),
    },
    name: 'System',
    path: '/system',
    children: [
      {
        path: '/system/role',
        name: 'SystemRole',
        meta: {
          icon: 'mdi:account-group',
          title: $t('system.role.title'),
        },
        component: () => import('#/views/system/role/list.vue'),
      },
      {
        path: '/system/menu',
        name: 'SystemMenu',
        meta: {
          icon: 'mdi:menu',
          title: $t('system.menu.title'),
        },
        component: () => import('#/views/system/menu/list.vue'),
      },
      {
        path: '/system/dept',
        name: 'SystemDept',
        meta: {
          icon: 'charm:organisation',
          title: $t('system.dept.title'),
        },
        component: () => import('#/views/system/dept/list.vue'),
      },
      {
        path: '/system/company',
        name: 'SystemCompany',
        meta: {
          icon: 'mdi:office-building',
          title: $t('system.company.title'),
        },
        component: () => import('#/views/system/company/list.vue'),
      },
      {
        path: '/system/biz-select-config',
        name: 'SystemBizSelectConfig',
        meta: {
          icon: 'mdi:format-list-bulleted-type',
          title: 'BizSelect 설정',
        },
        component: () => import('#/views/system/biz-select-config/list.vue'),
      },
    ],
  },
];

export default routes;
