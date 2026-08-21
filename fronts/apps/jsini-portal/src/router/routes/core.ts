import type { RouteRecordRaw } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { preferences } from '@vben/preferences';

import { $t } from '#/locales';

const BasicLayout = () => import('#/layouts/basic.vue');
const AuthPageLayout = () => import('#/layouts/auth.vue');
/** 전역 404 페이지 */
const fallbackNotFoundRoute: RouteRecordRaw = {
  component: () => import('#/views/_core/fallback/not-found.vue'),
  meta: {
    hideInBreadcrumb: true,
    hideInMenu: true,
    hideInTab: true,
    title: '404',
  },
  name: 'FallbackNotFound',
  path: '/:path(.*)*',
}
;


const MaintenanceRoute: RouteRecordRaw = 
/** 서버장애 페이지 */
{
    path: '/maintenance',
    name: 'Maintenance',
    component: () => import('#/views/_core/fallback/offline.vue'), // 임시로 생성한 test.vue 연결
    meta: {
      hideInBreadcrumb: true,
      hideInMenu: true,
      hideInTab: true,
      title: '서비스 점검 중',
      ignoreAuth: true, // 권한 체크 무시 (서버가 죽었으므로 권한 체크를 하면 안 됨)
    },
  }
;




const ForbiddenRoute: RouteRecordRaw =
/** 권한 없음 페이지 */
{
    path: '/403',
    name: 'Forbidden',
    component: () => import('#/views/_core/fallback/forbidden.vue'),
    meta: {
      hideInBreadcrumb: true,
      hideInMenu: true,
      hideInTab: true,
      title: '접근 권한 없음',
      // 로그인은 되어 있으나 그 메뉴의 열람 권한이 없을 때 보내는 화면이다.
      // 이 화면 자체를 다시 권한으로 막으면 무한 이동이 된다.
      ignoreAccess: true,
    },
  }
;

/** 기본 라우트, 이 라우트들은 반드시 존재해야 합니다. */
const coreRoutes: RouteRecordRaw[] = [
  /**
   * 루트 라우트
   * 기본 레이아웃을 사용하며 모든 페이지의 부모 컨테이너 역할을 하므로 자식 페이지에서 BasicLayout을 구성할 필요가 없습니다.
   * 이 라우트는 반드시 존재해야 하며 수정해서는 안 됩니다.
   */
  {
    component: BasicLayout,
    meta: {
      hideInBreadcrumb: true,
      title: 'Root',
    },
    name: 'Root',
    path: '/',
    redirect: preferences.app.defaultHomePath,
    children: [],
  },
  {
    component: AuthPageLayout,
    meta: {
      hideInTab: true,
      title: 'Authentication',
    },
    name: 'Authentication',
    path: '/auth',
    redirect: LOGIN_PATH,
    children: [
      {
        name: 'Login',
        path: 'login',
        component: () => import('#/views/_core/authentication/login.vue'),
        meta: {
          title: $t('page.auth.login'),
        },
      },
      {
        name: 'CodeLogin',
        path: 'code-login',
        component: () => import('#/views/_core/authentication/code-login.vue'),
        meta: {
          title: $t('page.auth.codeLogin'),
        },
      },
      {
        name: 'QrCodeLogin',
        path: 'qrcode-login',
        component: () =>
          import('#/views/_core/authentication/qrcode-login.vue'),
        meta: {
          title: $t('page.auth.qrcodeLogin'),
        },
      },
      {
        name: 'ForgetPassword',
        path: 'forget-password',
        component: () =>
          import('#/views/_core/authentication/forget-password.vue'),
        meta: {
          title: $t('page.auth.forgetPassword'),
        },
      },
      {
        name: 'Register',
        path: 'register',
        component: () => import('#/views/_core/authentication/register.vue'),
        meta: {
          title: $t('page.auth.register'),
        },
      },
    ],
  },
  {
    path: '/building/deceased/photo-editor',
    name: 'DeceasedPhotoEditor',
    component: () => import('#/views/funeral/building/deceased/photo-editor.vue'),
    meta: {
      hideInBreadcrumb: true,
      hideInMenu: true,
      hideInTab: true,
      title: '고인 영정사진 편집기',
    },
  },
  MaintenanceRoute,
  ForbiddenRoute,
];

export { coreRoutes, fallbackNotFoundRoute, ForbiddenRoute, MaintenanceRoute };
