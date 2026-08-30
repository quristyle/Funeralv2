import type { RouteRecordRaw } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { preferences } from '@vben/preferences';


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




/**
 * `/403` 을 이루는 라우트 이름들.
 *
 * **일부러 `coreRouteNames` 에서 빼는 데 쓴다**(routes/index.ts).
 * 코어 라우트는 권한 가드가 아무 일도 하지 않고 지나쳐서 메뉴·라우트를 만들지 않는다.
 * 그러면 이 화면에서 새로고침했을 때 사이드바가 텅 빈 채로 뜬다 —
 * 로그아웃하러 들어온 화면에서 로그아웃 단추가 없어지는 셈이다.
 * 빼 두면 가드가 평소대로 메뉴를 만들어 준다. 로그인하지 않은 사람은
 * 아래 `ignoreAccess` 가 통과시키므로 로그인 화면으로 튕기지도 않는다.
 */
const forbiddenRouteNames = ['ForbiddenLayout', 'Forbidden'];

const ForbiddenRoute: RouteRecordRaw =
/**
 * 권한 없음 페이지.
 *
 * **BasicLayout 안에서 그린다.** 예전에는 레이아웃 밖의 최상위 라우트여서
 * 사이드바도 헤더도 없는 맨 화면이 떴다. 로그인 직후 홈 화면의 열람 권한이
 * 없으면 곧바로 이 화면이 뜨는데, 그 상태에서는 다른 화면으로 갈 수도
 * **로그아웃할 수도** 없었다 — 브라우저 저장소를 지우는 것 말고는 길이 없었다.
 * 레이아웃 안에 두면 사이드바와 오른쪽 위 사용자 메뉴가 함께 나온다.
 */
{
    path: '/403',
    name: 'ForbiddenLayout',
    component: BasicLayout,
    meta: {
      hideInBreadcrumb: true,
      hideInMenu: true,
      hideInTab: true,
      title: '접근 권한 없음',
      // 로그인은 되어 있으나 그 메뉴의 열람 권한이 없을 때 보내는 화면이다.
      // 이 화면 자체를 다시 권한으로 막으면 무한 이동이 된다.
      // 부모에도 달아 둔다 — 로그인 전에 들어와도 로그인 화면으로 튕기지 않게.
      ignoreAccess: true,
    },
    children: [
      {
        path: '',
        name: 'Forbidden',
        component: () => import('#/views/_core/fallback/forbidden.vue'),
        meta: {
          hideInBreadcrumb: true,
          hideInMenu: true,
          hideInTab: true,
          title: '접근 권한 없음',
          ignoreAccess: true,
        },
      },
    ],
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
          title: 'page.auth.login',
        },
      },
      {
        name: 'CodeLogin',
        path: 'code-login',
        component: () => import('#/views/_core/authentication/code-login.vue'),
        meta: {
          title: 'page.auth.codeLogin',
        },
      },
      {
        name: 'QrCodeLogin',
        path: 'qrcode-login',
        component: () =>
          import('#/views/_core/authentication/qrcode-login.vue'),
        meta: {
          title: 'page.auth.qrcodeLogin',
        },
      },
      {
        name: 'ForgetPassword',
        path: 'forget-password',
        component: () =>
          import('#/views/_core/authentication/forget-password.vue'),
        meta: {
          title: 'page.auth.forgetPassword',
        },
      },
      {
        name: 'Register',
        path: 'register',
        component: () => import('#/views/_core/authentication/register.vue'),
        meta: {
          title: 'page.auth.register',
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

export {
  coreRoutes,
  fallbackNotFoundRoute,
  forbiddenRouteNames,
  ForbiddenRoute,
  MaintenanceRoute,
};
