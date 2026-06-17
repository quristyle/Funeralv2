import type {
  DrawerApiOptions,
  DrawerProps,
  ExtendedDrawerApi,
} from './drawer';

import {
  defineComponent,
  h,
  inject,
  nextTick,
  provide,
  reactive,
  ref,
} from 'vue';

import { useStore } from '@vben-core/shared/store';

import { DrawerApi } from './drawer-api';
import VbenDrawer from './drawer.vue';

const USER_DRAWER_INJECT_KEY = Symbol('VBEN_DRAWER_INJECT');

const DEFAULT_DRAWER_PROPS: Partial<DrawerProps> = {};

export function setDefaultDrawerProps(props: Partial<DrawerProps>) {
  Object.assign(DEFAULT_DRAWER_PROPS, props);
}

export function useVbenDrawer<
  TParentDrawerProps extends DrawerProps = DrawerProps,
>(options: DrawerApiOptions = {}) {
  // 드로어는 일반적으로 분리되므로, connectedComponent가 전달되면 외부 호출로 간주하여 내부 컴포넌트와 연결함
  // 외부 드로어는 provide/inject를 통해 API를 전달함

  const { connectedComponent } = options;
  if (connectedComponent) {
    const extendedApi = reactive({});
    const isDrawerReady = ref(true);
    const Drawer = defineComponent(
      (props: TParentDrawerProps, { attrs, slots }) => {
        provide(USER_DRAWER_INJECT_KEY, {
          extendApi(api: ExtendedDrawerApi) {
            // reactive에 직접 값을 할당할 수 없으며, 그러면 반응성을 잃게 됨
            // Object.assign을 사용할 수 없으며, 그러면 API의 프로토타입 함수를 잃게 됨
            Object.setPrototypeOf(extendedApi, api);
          },
          options,
          async reCreateDrawer() {
            isDrawerReady.value = false;
            await nextTick();
            isDrawerReady.value = true;
          },
        });
        checkProps(extendedApi as ExtendedDrawerApi, {
          ...props,
          ...attrs,
          ...slots,
        });
        return () =>
          h(
            isDrawerReady.value ? connectedComponent : 'div',
            { ...props, ...attrs },
            slots,
          );
      },
      // eslint-disable-next-line vue/one-component-per-file
      {
        name: 'VbenParentDrawer',
        inheritAttrs: false,
      },
    );

    return [Drawer, extendedApi as ExtendedDrawerApi] as const;
  }

  const injectData = inject<any>(USER_DRAWER_INJECT_KEY, {});

  const mergedOptions = {
    ...DEFAULT_DRAWER_PROPS,
    ...injectData.options,
    ...options,
  } as DrawerApiOptions;

  mergedOptions.onOpenChange = (isOpen: boolean) => {
    options.onOpenChange?.(isOpen);
    injectData.options?.onOpenChange?.(isOpen);
  };

  const onClosed = mergedOptions.onClosed;
  mergedOptions.onClosed = () => {
    onClosed?.();
    if (mergedOptions.destroyOnClose) {
      injectData.reCreateDrawer?.();
    }
  };
  const api = new DrawerApi(mergedOptions);

  const extendedApi: ExtendedDrawerApi = api as never;

  extendedApi.useStore = (selector) => {
    return useStore(api.store, selector);
  };

  const Drawer = defineComponent(
    (props: DrawerProps, { attrs, slots }) => {
      return () =>
        h(VbenDrawer, { ...props, ...attrs, drawerApi: extendedApi }, slots);
    },
    // eslint-disable-next-line vue/one-component-per-file
    {
      name: 'VbenDrawer',
      inheritAttrs: false,
    },
  );
  injectData.extendApi?.(extendedApi);
  return [Drawer, extendedApi] as const;
}

async function checkProps(api: ExtendedDrawerApi, attrs: Record<string, any>) {
  if (!attrs || Object.keys(attrs).length === 0) {
    return;
  }
  await nextTick();

  const state = api?.store?.state;

  if (!state) {
    return;
  }

  const stateKeys = new Set(Object.keys(state));

  for (const attr of Object.keys(attrs)) {
    if (stateKeys.has(attr) && !['class'].includes(attr)) {
      // connectedComponent가 존재할 때 드로어의 props를 전달하지 마십시오. 복잡도가 증가할 수 있습니다. 드로어의 props를 수정해야 하는 경우 useVbenDrawer 또는 API를 사용하십시오.
      console.warn(
        `[Vben Drawer]: When 'connectedComponent' exists, do not set props or slots '${attr}', which will increase complexity. If you need to modify the props of Drawer, please use useVbenDrawer or api.`,
      );
    }
  }
}
