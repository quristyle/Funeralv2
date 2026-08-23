import type { RouteLocationNormalizedGeneric } from 'vue-router';

import type { TabDefinition } from '@vben/types';

import type { IContextMenuItem } from '@vben-core/tabs-ui';

import { computed, inject, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { useContentMaximize, useTabs } from '@vben/hooks';
import {
  ArrowLeftToLine,
  ArrowRightLeft,
  ArrowRightToLine,
  ExternalLink,
  FoldHorizontal,
  Fullscreen,
  Minimize2,
  Pin,
  PinOff,
  RotateCw,
  Star,
  StarOff,
  X,
} from '@vben/icons';
import { $t, $tIfKey, useI18n } from '@vben/locales';
import { getTabKey, useAccessStore, useTabbarStore } from '@vben/stores';
import { filterTree } from '@vben/utils';

/**
 * 즐겨찾기를 다루는 창구. 앱이 주입한다.
 *
 * 이 레이아웃은 프레임워크 패키지라 앱의 API·스토어를 직접 알지 못한다.
 * 메뉴 다시 읽기(`MENU_RELOAD_HANDLER`)와 같은 방식으로 앱이 배선해 준다.
 * 주입되지 않은 앱에서는 즐겨찾기 항목이 아예 나타나지 않는다.
 */
export interface TabFavoriteHandler {
  /** 즐겨찾기에 담는다 */
  add: (path: string) => Promise<void> | void;
  /** 이 경로가 즐겨찾기인가 */
  isFavorite: (path: string) => boolean;
  /** 즐겨찾기에서 뺀다 */
  remove: (path: string) => Promise<void> | void;
}

export function useTabbar() {
  const router = useRouter();
  const route = useRoute();
  const accessStore = useAccessStore();
  const tabbarStore = useTabbarStore();
  const { contentIsMaximize, toggleMaximize } = useContentMaximize();
  const {
    closeAllTabs,
    closeCurrentTab,
    closeLeftTabs,
    closeOtherTabs,
    closeRightTabs,
    closeTabByKey,
    getTabDisableState,
    openTabInNewWindow,
    refreshTab,
    toggleTabPin,
  } = useTabs();

  /**
   * 현재 경로에 해당하는 탭의 키
   */
  const currentActive = computed(() => {
    return getTabKey(route);
  });

  /** 앱이 주입한 즐겨찾기 창구. 없으면 즐겨찾기 항목을 넣지 않는다. */
  const favoriteHandler = inject<null | TabFavoriteHandler>(
    'TAB_FAVORITE_HANDLER',
    null,
  );

  const { locale } = useI18n();
  const currentTabs = ref<RouteLocationNormalizedGeneric[]>();
  watch(
    [
      () => tabbarStore.getTabs,
      () => tabbarStore.updateTime,
      () => locale.value,
    ],
    ([tabs]) => {
      currentTabs.value = tabs.map((item) => wrapperTabLocale(item));
    },
  );

  /**
   * 고정 탭 초기화
   */
  const initAffixTabs = () => {
    const affixTabs = filterTree(router.getRoutes(), (route) => {
      return !!route.meta?.affixTab;
    });
    tabbarStore.setAffixTabs(affixTabs);
  };

  // 탭 클릭, 라우트 이동
  const handleClick = (key: string) => {
    const { fullPath, path } = tabbarStore.getTabByKey(key);
    router.push(fullPath || path);
  };

  // 탭 닫기
  const handleClose = async (key: string) => {
    await closeTabByKey(key);
  };

  function wrapperTabLocale(tab: RouteLocationNormalizedGeneric) {
    return {
      ...tab,
      meta: {
        ...tab?.meta,
        // 탭 제목도 DB 에서 온 글자일 수 있다 — 키일 때만 번역한다($tIfKey 주석 참고)
        title: $tIfKey(tab?.meta?.title as string),
      },
    };
  }

  watch(
    () => accessStore.accessMenus,
    () => {
      initAffixTabs();
    },
    { immediate: true },
  );

  watch(
    () => route.fullPath,
    () => {
      const meta = route.matched?.[route.matched.length - 1]?.meta;
      tabbarStore.addTab({
        ...route,
        meta: meta || route.meta,
      });
    },
    { immediate: true },
  );

  const createContextMenus = (tab: TabDefinition) => {
    const {
      disabledCloseAll,
      disabledCloseCurrent,
      disabledCloseLeft,
      disabledCloseOther,
      disabledCloseRight,
      disabledRefresh,
    } = getTabDisableState(tab);

    const affixTab = tab?.meta?.affixTab ?? false;

    // 즐겨찾기 대상 경로. 탭의 fullPath 에는 조회 조건이 붙어 오므로 경로만 쓴다
    // (`/a/b?id=3` 을 담아도 메뉴는 `/a/b` 하나뿐이다).
    const favoritePath = tab?.path ?? '';
    const isFavorite = favoriteHandler?.isFavorite(favoritePath) ?? false;

    const menus: IContextMenuItem[] = [
      {
        disabled: disabledCloseCurrent,
        handler: async () => {
          await closeCurrentTab(tab);
        },
        icon: X,
        key: 'close',
        text: $t('preferences.tabbar.contextMenu.close'),
      },
      {
        handler: async () => {
          await toggleTabPin(tab);
        },
        icon: affixTab ? PinOff : Pin,
        key: 'affix',
        text: affixTab
          ? $t('preferences.tabbar.contextMenu.unpin')
          : $t('preferences.tabbar.contextMenu.pin'),
      },
      {
        handler: async () => {
          if (!contentIsMaximize.value) {
            await router.push(tab.fullPath);
          }
          toggleMaximize();
        },
        icon: contentIsMaximize.value ? Minimize2 : Fullscreen,
        key: contentIsMaximize.value ? 'restore-maximize' : 'maximize',
        text: contentIsMaximize.value
          ? $t('preferences.tabbar.contextMenu.restoreMaximize')
          : $t('preferences.tabbar.contextMenu.maximize'),
      },
      {
        disabled: disabledRefresh,
        handler: () => refreshTab(),
        icon: RotateCw,
        key: 'reload',
        text: $t('preferences.tabbar.contextMenu.reload'),
      },
      {
        handler: async () => {
          await openTabInNewWindow(tab);
        },
        icon: ExternalLink,
        key: 'open-in-new-window',
        // 즐겨찾기 항목이 걸러져 없는 앱에서도 이 구분선은 그대로 남아야 한다.
        separator: true,
        text: $t('preferences.tabbar.contextMenu.openInNewWindow'),
      },

      /*
       * 즐겨찾기.
       *
       * 지금 상태에 맞는 한 쪽만 보인다 — 담겨 있으면 '제거', 아니면 '추가'.
       * 두 항목을 늘 보여주고 하나를 흐리게 하는 방식보다, 지금 할 수 있는 일만
       * 보이는 편이 오른쪽 눌러 바로 고르는 흐름에 맞다.
       *
       * 키가 둘로 나뉘어 있으므로 앱의 menuList 에도 둘 다 넣어야 한다
       * (맨 아래 filter 를 보라).
       */
      {
        handler: async () => {
          await favoriteHandler?.add(favoritePath);
        },
        icon: Star,
        key: 'favorite-add',
        separator: true,
        text: $t('preferences.tabbar.contextMenu.favoriteAdd'),
      },
      {
        handler: async () => {
          await favoriteHandler?.remove(favoritePath);
        },
        icon: StarOff,
        key: 'favorite-remove',
        separator: true,
        text: $t('preferences.tabbar.contextMenu.favoriteRemove'),
      },

      {
        disabled: disabledCloseLeft,
        handler: async () => {
          await closeLeftTabs(tab);
        },
        icon: ArrowLeftToLine,
        key: 'close-left',
        text: $t('preferences.tabbar.contextMenu.closeLeft'),
      },
      {
        disabled: disabledCloseRight,
        handler: async () => {
          await closeRightTabs(tab);
        },
        icon: ArrowRightToLine,
        key: 'close-right',
        separator: true,
        text: $t('preferences.tabbar.contextMenu.closeRight'),
      },
      {
        disabled: disabledCloseOther,
        handler: async () => {
          await closeOtherTabs(tab);
        },
        icon: FoldHorizontal,
        key: 'close-other',
        text: $t('preferences.tabbar.contextMenu.closeOther'),
      },
      {
        disabled: disabledCloseAll,
        handler: closeAllTabs,
        icon: ArrowRightLeft,
        key: 'close-all',
        text: $t('preferences.tabbar.contextMenu.closeAll'),
      },
    ];

    return menus.filter((item) => {
      if (!tabbarStore.getMenuList.includes(item.key)) return false;

      // 즐겨찾기는 지금 상태에 맞는 한 쪽만 남긴다.
      // 창구를 주입하지 않은 앱에서는 둘 다 빠진다.
      if (item.key === 'favorite-add') {
        return Boolean(favoriteHandler) && !isFavorite;
      }
      if (item.key === 'favorite-remove') {
        return Boolean(favoriteHandler) && isFavorite;
      }

      return true;
    });
  };

  return {
    createContextMenus,
    currentActive,
    currentTabs,
    handleClick,
    handleClose,
  };
}
