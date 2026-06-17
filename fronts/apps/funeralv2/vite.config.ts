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
        __VUE_PROD_DEVTOOLS__: true,
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
