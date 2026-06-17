import type { Options as PwaPluginOptions } from 'vite-plugin-pwa';

import type { ImportmapPluginOptions } from './typing';

const isDevelopment = process.env.NODE_ENV === 'development';

const getDefaultPwaOptions = (name: string): Partial<PwaPluginOptions> => ({
  manifest: {
    description:
      'Vben Admin is a modern admin dashboard template based on Vue 3. ',
    icons: [
      {
        sizes: '192x192',
        src: 'https://unpkg.com/@vbenjs/static-source@0.1.7/source/pwa-icon-192.png',
        type: 'image/png',
      },
      {
        sizes: '512x512',
        src: 'https://unpkg.com/@vbenjs/static-source@0.1.7/source/pwa-icon-512.png',
        type: 'image/png',
      },
    ],
    name: `${name}${isDevelopment ? ' dev' : ''}`,
    short_name: `${name}${isDevelopment ? ' dev' : ''}`,
  },
});

/**
 * 일부 패키지가 지원하지 않고 네트워크가 불안정하여 importmap CDN은 당분간 활성화하지 않음
 */
const defaultImportmapOptions: ImportmapPluginOptions = {
  // Importmap CDN 방식으로 도입,
  // 현재 esm.sh 소스만 호환성이 좋으며, jspm.io는 esm 엔트리에 대한 요구사항이 높음
  defaultProvider: 'esm.sh',
  importmap: [
    { name: 'vue' },
    { name: 'pinia' },
    { name: 'vue-router' },
    // { name: 'vue-i18n' },
    { name: 'dayjs' },
    { name: 'vue-demi' },
  ],
};

export { defaultImportmapOptions, getDefaultPwaOptions };
