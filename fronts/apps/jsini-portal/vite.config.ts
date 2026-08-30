import { defineConfig } from '@vben/vite-config';

export default defineConfig(async () => {
  return {
    application: {
      nitroMock: false,
      printInfoMap: {
        'welcome to jsini': 'https://jsini.co.kr',
      },
      // ── PWA ────────────────────────────────────────────────
      // 포털은 portal.jsini.co.kr 에서 앱으로 설치되고(홈 화면 추가)
      // 웹푸시를 받는다. vben 기본 pwaOptions 는 아이콘이 unpkg CDN 이라
      // 저장소 규칙(글꼴·자원은 저장소 안의 것만)에 어긋나 전부 우리 것으로 바꾼다.
      // 아이콘 PNG 는 docs/brand/generate.py 가 만든다 — 손으로 그리지 않는다.
      pwa: true,
      pwaOptions: {
        // 등록은 src/pwa.ts 가 한다 (vben 이 injectRegister:false 로 꺼 둔다).
        registerType: 'autoUpdate',
        // 개발(localhost)에서도 서비스워커를 돌린다 — 알림 설정 화면의
        // 구독 버튼이 서비스워커 없이는 동작하지 않는다.
        devOptions: { enabled: true },
        workbox: {
          // 프리캐시는 하지 않는다. 로그인 뒤에만 쓰는 업무 화면이라 오프라인
          // 가치가 낮고, 청크가 커서(수 MB) 설치가 무거워진다.
          globPatterns: [],
          // 웹푸시 수신 처리. 페이로드 키는 NotificationServer 와의 약속이다.
          importScripts: ['push-sw.js'],
        },
        manifest: {
          id: '/',
          lang: 'ko',
          name: 'JSini 포털',
          short_name: 'JSini 포털',
          description: 'JSini 관리 포털 — 장례식장 · 헬프데스크 · 프로젝트관리 · 생활과환경',
          start_url: '/',
          scope: '/',
          display: 'standalone',
          background_color: '#ffffff',
          theme_color: '#0a0a0a',
          icons: [
            { sizes: '192x192', src: '/pwa-icon-192.png', type: 'image/png' },
            { sizes: '512x512', src: '/pwa-icon-512.png', type: 'image/png' },
            {
              purpose: 'maskable',
              sizes: '512x512',
              src: '/pwa-icon-maskable-512.png',
              type: 'image/png',
            },
          ],
        },
      },
    },
    vite: {
      define: {
        // 프로덕션 빌드에서 devtools 를 켜두면 내부 상태(스토어/컴포넌트 트리)가 그대로 노출되고
        // 번들도 커진다. 개발 모드에서만 활성화한다.
        __VUE_PROD_DEVTOOLS__: process.env.NODE_ENV !== 'production',
      },
      server: {
        proxy: {
          '/api': {
            changeOrigin: true,
            target: 'http://127.0.0.1:5265',
            ws: true,
          },
        },
      },
    },
  };
});
