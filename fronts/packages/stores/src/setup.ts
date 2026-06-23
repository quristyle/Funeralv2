import type { Pinia } from 'pinia';

import type { App } from 'vue';

import { createPinia } from 'pinia';
import SecureLS from 'secure-ls';

let pinia: Pinia;

type SecureLSStorage = {
  get(key: string): any;
  set(key: string, value: unknown): void;
};

type SecureLSCtor = new (config?: {
  encodingType?: string;
  encryptionSecret?: string;
  isCompression?: boolean;
  metaKey?: string;
}) => SecureLSStorage;

const secureLSModule = SecureLS as unknown as {
  default?: SecureLSCtor;
  SecureLS?: SecureLSCtor;
};

const SecureLSConstructor =
  secureLSModule.default ??
  secureLSModule.SecureLS ??
  (SecureLS as unknown as SecureLSCtor);

export interface InitStoreOptions {
  /**
   * @ko_KR 애플리케이션 이름. @vben/stores는 공용이므로 향후 여러 앱이 있을 수 있습니다. 여러 앱 간의 캐시 충돌을 방지하기 위해 여기서 애플리케이션 이름을 설정할 수 있으며, 이 이름은 지속성 접두사로 사용됩니다.
   */
  namespace: string;
}

/**
 * @ko_KR Pinia 초기화
 */
export async function initStores(app: App, options: InitStoreOptions) {
  const { createPersistedState } = await import('pinia-plugin-persistedstate');
  pinia = createPinia();
  const { namespace } = options;
  const ls = new SecureLSConstructor({
    encodingType: 'aes',
    encryptionSecret: import.meta.env.VITE_APP_STORE_SECURE_KEY,
    isCompression: true,
    metaKey: `${namespace}-secure-meta`,
  });
  pinia.use(
    createPersistedState({
      // key $appName-$store.id
      key: (storeKey) => `${namespace}-${storeKey}`,
      storage: import.meta.env.DEV
        ? localStorage
        : {
            getItem(key) {
              return ls.get(key);
            },
            setItem(key, value) {
              ls.set(key, value);
            },
          },
    }),
  );
  app.use(pinia);
  return pinia;
}

export function resetAllStores() {
  if (!pinia) {
    console.error('Pinia is not installed');
    return;
  }
  const allStores = (pinia as any)._s;
  for (const [_key, store] of allStores) {
    try {
      if (typeof store.$reset === 'function') {
        store.$reset();
      }
    } catch (error) {
      console.warn(`Failed to reset store ${_key}:`, error);
      // setup 스토어 형식이라 $reset()에서 에러가 발생한 경우 clearCache 등이 있으면 호출
      if (store && typeof (store as any).clearCache === 'function') {
        (store as any).clearCache();
      }
    }
  }
}
