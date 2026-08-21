import { defineConfig } from '@vben/vite-config';

export default defineConfig(async () => {
  return {
    application: {
      nitroMock: false,
      printInfoMap: {
        'welcome to jsini': 'https://jsini.co.kr',
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
