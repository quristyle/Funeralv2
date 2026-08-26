import type { RouteRecordRaw } from 'vue-router';

import { LOCALES } from '#/i18n/messages';

/**
 * 언어를 주소 앞에 둔다 (`/ko/about` · `/en/about`).
 *
 * 왜 주소인가. 정적 프리렌더라 빌드 시점에는 스토어도 브라우저도 없고 주소만 있다.
 * 주소에 언어가 있으면 그 두 벌이 각각 HTML 로 떨어지고, 검색 엔진도 두 벌로 인식한다.
 * 쿠키나 Accept-Language 로 고르면 정적 파일 하나로는 두 언어를 담을 수 없다.
 *
 * 언어를 늘릴 때는 `i18n/messages.ts` 의 `LOCALES` 에 코드를 더하면 라우트도 함께 늘어난다.
 */
export const routes: RouteRecordRaw[] = [
  // 언어 없이 들어오면 기본 언어로 넘긴다.
  { path: '/', redirect: '/ko' },

  {
    path: `/:locale(${LOCALES.join('|')})`,
    component: () => import('#/layouts/site-layout.vue'),
    children: [
      { path: '', name: 'home', component: () => import('#/views/home.vue') },
      { path: 'about', name: 'about', component: () => import('#/views/about.vue') },
      { path: 'news', name: 'news', component: () => import('#/views/news.vue') },
      {
        // 자료에 달린 주소라 프리렌더하지 않는다. 브라우저에서 받아 그린다.
        path: 'news/:slug',
        name: 'news-detail',
        component: () => import('#/views/news-detail.vue'),
      },
      { path: 'downloads', name: 'downloads', component: () => import('#/views/downloads.vue') },
      { path: 'contact', name: 'contact', component: () => import('#/views/contact.vue') },
    ],
  },

  // 아는 언어가 아니거나 없는 경로. 기본 언어의 홈으로 보낸다.
  { path: '/:pathMatch(.*)*', redirect: '/ko' },
];

/**
 * 프리렌더할 주소 목록. `vite-ssg` 가 이 값으로 HTML 을 만든다.
 *
 * 목록에서 뉴스 상세를 뺐다. 넣으려면 빌드 때 API 를 불러 slug 를 받아야 하는데,
 * 그러면 **배포가 API 상태에 묶인다.** 서버가 잠깐 죽어 있으면 빌드가 실패한다.
 * 검색 노출이 중요해지면 그 대가를 받아들이고 여기에 넣는다.
 */
export const prerenderRoutes: string[] = LOCALES.flatMap((l) => [
  `/${l}`,
  `/${l}/about`,
  `/${l}/news`,
  `/${l}/downloads`,
  `/${l}/contact`,
]);
