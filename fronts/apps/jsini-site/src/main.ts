import { ViteSSG } from 'vite-ssg';

import App from '#/App.vue';
import { normalizeLocale } from '#/i18n/messages';
import { prerenderRoutes, routes } from '#/router/routes';

import '#/styles/index.css';

/**
 * 진입점.
 *
 * `createApp` 이 아니라 `ViteSSG` 다. 개발에서는 평범한 SPA 로 돌고,
 * `vite-ssg build` 때는 같은 코드로 라우트를 돌며 HTML 을 미리 만든다 (결정 D-S1).
 * 결과는 정적 파일 묶음이고 서버 런타임이 없다.
 */
export const createApp = ViteSSG(
  App,
  { routes },
  ({ router, isClient }) => {
    // 언어를 <html lang> 에 반영한다. 스크린 리더와 검색 엔진이 이 값을 읽는다.
    router.afterEach((to) => {
      const locale = normalizeLocale(to.params.locale as string | undefined);

      if (isClient) {
        document.documentElement.lang = locale;
        // 뉴스 상세는 자기 제목을 스스로 넣는다(그 화면 참고).
        if (to.name !== 'news-detail') {
          document.title = 'JSINI';
        }
      }
    });
  },
);

/** vite-ssg 가 프리렌더할 주소. 목록을 정하는 근거는 routes.ts 에 있다. */
export const includedRoutes = () => prerenderRoutes;
