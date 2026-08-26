import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';

import { MESSAGES, normalizeLocale } from './src/i18n/messages';

/**
 * 회사 소개 사이트.
 *
 * 포털(`apps/jsini-portal`)과 달리 `@vben/vite-config` 를 쓰지 않는다.
 * 그쪽은 인증 후 SPA 를 위한 설정 묶음이고, 이 앱은 정적 프리렌더가 목적이라
 * 요구되는 것이 반대다. 상위 vben 동기화가 이 앱을 흔들지 않게 하려는 뜻도 있다.
 *
 * 빌드는 `vite-ssg build` 다 — 라우트를 돌며 HTML 을 미리 만든다.
 * 결과는 정적 파일 묶음이고 서버 런타임이 없다 (결정 D-S1).
 */
export default defineConfig({
  plugins: [vue(), tailwindcss()],

  resolve: {
    alias: {
      '#': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },

  server: {
    port: 5556,
    // 개발 중에는 게이트웨이로 넘긴다. 운영에서는 같은 도메인 아래에 붙이거나
    // SiteServer 의 Cors:AllowedOrigins 에 사이트 도메인을 넣는다.
    proxy: {
      '/api': {
        changeOrigin: true,
        target: 'http://127.0.0.1:5265',
      },
    },
  },

  build: {
    // 정적 호스팅에 올릴 것이라 자산 이름에 해시를 남겨 둔다(기본값).
    // 청크 경고 기준만 낮춰서, 소개 사이트가 무거워지는 것을 빌드 때 알아채게 한다.
    chunkSizeWarningLimit: 300,
  },

  /**
   * 프리렌더된 HTML 의 `<html lang>` 과 `<title>` · `description` 을 언어별로 바꾼다.
   *
   * 왜 여기서 하는가. 이 값들은 **자바스크립트가 돌기 전에** 맞아 있어야 한다.
   * 검색 엔진과 스크린 리더가 읽는 시점이 그때이고, SNS 미리보기는 JS 를 아예 돌리지 않는다.
   * 화면 코드에서 `document.title` 을 바꾸는 것으로는 늦다 (그건 화면 전환용이다).
   *
   * `@unhead/vue` 를 얹으면 컴포넌트 안에서 선언할 수 있지만, 이 사이트가 다루는 것은
   * 언어 두 벌 × 화면 다섯이라 표 하나로 끝난다. 의존성을 늘릴 만한 일이 아니다.
   */
  ssgOptions: {
    onPageRendered(route: string, html: string) {
      const locale = normalizeLocale(route.split('/')[1]);
      const t = MESSAGES[locale];

      // 경로의 마지막 조각으로 화면을 알아낸다. `/ko` 는 홈이다.
      const last = route.replace(/\/$/, '').split('/')[2] ?? '';
      const pageTitle =
        {
          about: t.nav.about,
          news: t.nav.news,
          downloads: t.nav.downloads,
          contact: t.nav.contact,
        }[last] ?? '';

      const title = pageTitle ? `${pageTitle} — JSINI` : 'JSINI';
      const description = pageTitle ? `JSINI — ${pageTitle}` : t.hero.lead;

      return html
        .replace(/<html lang="[^"]*"/, `<html lang="${locale}"`)
        .replace(/<title>[^<]*<\/title>/, `<title>${title}</title>`)
        .replace(
          /(<meta name="description" content=")[^"]*(")/,
          `$1${description.replaceAll('"', '&quot;')}$2`,
        )
        .replace(/(<meta property="og:title" content=")[^"]*(")/, `$1${title}$2`)
        .replace(/(<meta property="og:site_name" content=")[^"]*(")/, `$1JSINI$2`);
    },
  },
});
