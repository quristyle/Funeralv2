<script lang="ts" setup>
import type { SetupContext } from 'vue';
import type { RouteLocationNormalizedLoaded } from 'vue-router';

import type { MenuRecordRaw } from '@vben/types';

import { computed, onMounted, useSlots, watch, ref } from 'vue';
import { useRoute } from 'vue-router';

import { useRefresh } from '@vben/hooks';
import { $t, i18n } from '@vben/locales';
import {
  preferences,
  updatePreferences,
  usePreferences,
} from '@vben/preferences';
import { useAccessStore, useTabbarStore, useTimezoneStore } from '@vben/stores';
import { cloneDeep, mapTree } from '@vben/utils';

import { VbenAdminLayout } from '@vben-core/layout-ui';
import { Input, VbenBackTop, VbenLogo } from '@vben-core/shadcn-ui';

import { isAiChatPinned } from '../widgets/ai-chat/state';
import AiChatContent from '../widgets/ai-chat/ai-chat-content.vue';

import { Breadcrumb, CheckUpdates, Preferences } from '../widgets';
import { LayoutContent, LayoutContentSpinner } from './content';
import { Copyright } from './copyright';
import { LayoutFooter } from './footer';
import { LayoutHeader } from './header';
import {
  LayoutExtraMenu,
  LayoutMenu,
  LayoutMixedMenu,
  useExtraMenu,
  useMixedMenu,
} from './menu';
import { LayoutTabbar } from './tabbar';

defineOptions({ name: 'BasicLayout' });

const emit = defineEmits<{ clearPreferencesAndLogout: []; clickLogo: [] }>();

const {
  isDark,
  isHeaderNav,
  isMixedNav,
  isMobile,
  isSideMixedNav,
  isHeaderMixedNav,
  isHeaderSidebarNav,
  layout,
  preferencesButtonPosition,
  sidebarCollapsed,
  theme,
} = usePreferences();
const accessStore = useAccessStore();
const timezoneStore = useTimezoneStore();
const { refresh } = useRefresh();

const sidebarTheme = computed(() => {
  const dark = isDark.value || preferences.theme.semiDarkSidebar;
  return dark ? 'dark' : 'light';
});

const sidebarThemeSub = computed(() => {
  const dark = isDark.value || preferences.theme.semiDarkSidebarSub;
  return dark ? 'dark' : 'light';
});

const headerTheme = computed(() => {
  const dark = isDark.value || preferences.theme.semiDarkHeader;
  return dark ? 'dark' : 'light';
});

const logoClass = computed(() => {
  const { collapsedShowTitle } = preferences.sidebar;
  const classes: string[] = [];

  if (collapsedShowTitle && sidebarCollapsed.value && !isMixedNav.value) {
    classes.push('mx-auto');
  }

  if (isSideMixedNav.value) {
    classes.push('flex-center');
  }

  return classes.join(' ');
});

const isMenuRounded = computed(() => {
  return preferences.navigation.styleType === 'rounded';
});

const logoCollapsed = computed(() => {
  if (isMobile.value && sidebarCollapsed.value) {
    return true;
  }
  if (isHeaderNav.value || isMixedNav.value || isHeaderSidebarNav.value) {
    return false;
  }
  return (
    sidebarCollapsed.value || isSideMixedNav.value || isHeaderMixedNav.value
  );
});

const showHeaderNav = computed(() => {
  return (
    !isMobile.value &&
    (isHeaderNav.value || isMixedNav.value || isHeaderMixedNav.value)
  );
});

const {
  handleMenuSelect,
  handleMenuOpen,
  headerActive,
  headerMenus,
  sidebarActive,
  sidebarMenus,
  mixHeaderMenus,
  sidebarVisible,
} = useMixedMenu();

// 사이드 다중 열 메뉴
const {
  extraActiveMenu,
  extraMenus,
  handleDefaultSelect,
  handleMenuMouseEnter,
  handleMixedMenuSelect,
  handleSideMouseLeave,
  sidebarExtraVisible,
} = useExtraMenu(mixHeaderMenus);

/**
 * 메뉴 래핑 및 메뉴 이름 번역
 * @param menus 원본 메뉴 데이터
 * @param deep 깊은 래핑 여부. 2열 레이아웃의 경우 확장 메뉴에서 더 깊은 데이터가 다시 래핑되므로 첫 번째 레이어만 래핑하면 됩니다.
 */
function wrapperMenus(menus: MenuRecordRaw[], deep: boolean = true) {
  return deep
    ? mapTree(menus, (item) => {
        return { ...cloneDeep(item), name: $t(item.name) };
      })
    : menus.map((item) => {
        return { ...cloneDeep(item), name: $t(item.name) };
      });
}

/* ---------------------------------------------------------------------------
 * 사이드바 메뉴 검색
 * 입력이 느릴(끊길) 수 있음을 고려해 키워드를 debounce(300ms) 처리하여
 * 필터 재계산이 매 키 입력마다 발생하지 않도록 한다.
 * ------------------------------------------------------------------------- */
const menuSearchKeyword = ref('');
// debounce 적용된 실제 검색어 (필터 재계산 트리거)
const debouncedMenuKeyword = ref('');
let menuSearchTimer: null | ReturnType<typeof setTimeout> = null;
watch(menuSearchKeyword, (value) => {
  if (menuSearchTimer) {
    clearTimeout(menuSearchTimer);
  }
  menuSearchTimer = setTimeout(() => {
    debouncedMenuKeyword.value = value;
  }, 300);
});

// 사이드바가 접히면(아이콘 전용) 검색 입력부가 사라지므로 검색어를 즉시 초기화한다.
watch(sidebarCollapsed, (collapsed) => {
  if (collapsed && menuSearchKeyword.value) {
    if (menuSearchTimer) {
      clearTimeout(menuSearchTimer);
    }
    menuSearchKeyword.value = '';
    debouncedMenuKeyword.value = '';
  }
});

/**
 * 키워드로 메뉴 트리를 필터링한다.
 * - 자기 이름이 매칭되면 하위 메뉴 전체를 유지한다.
 * - 자기 이름은 매칭되지 않아도 하위에 매칭 항목이 있으면 그 가지만 유지한다.
 */
function filterMenusByKeyword(
  menus: MenuRecordRaw[],
  keyword: string,
): MenuRecordRaw[] {
  const result: MenuRecordRaw[] = [];
  for (const menu of menus) {
    const selfMatched = (menu.name ?? '').toLowerCase().includes(keyword);
    const matchedChildren = menu.children?.length
      ? filterMenusByKeyword(menu.children, keyword)
      : [];

    if (selfMatched) {
      // 부모가 매칭되면 원본 하위 메뉴를 그대로 노출
      result.push({ ...menu });
    } else if (matchedChildren.length > 0) {
      result.push({ ...menu, children: matchedChildren });
    }
  }
  return result;
}

// 이름 번역이 반영된 사이드바 메뉴 (검색 대상)
const wrappedSidebarMenus = computed(() => wrapperMenus(sidebarMenus.value));

// 실제 렌더링할 사이드바 메뉴 (검색어 적용)
const filteredSidebarMenus = computed(() => {
  const keyword = debouncedMenuKeyword.value.trim().toLowerCase();
  if (!keyword) {
    return wrappedSidebarMenus.value;
  }
  return filterMenusByKeyword(wrappedSidebarMenus.value, keyword);
});

// 검색 결과에서 하위 메뉴를 가진 노드는 자동으로 펼쳐지도록 경로를 수집
const sidebarSearchOpenPaths = computed<string[]>(() => {
  if (!debouncedMenuKeyword.value.trim()) {
    return [];
  }
  const paths: string[] = [];
  const walk = (nodes: MenuRecordRaw[]) => {
    for (const node of nodes) {
      if (node.children?.length) {
        paths.push(node.path);
        walk(node.children);
      }
    }
  };
  walk(filteredSidebarMenus.value);
  return paths;
});

// 검색 상태가 바뀌면 메뉴 컴포넌트를 재초기화(펼침 상태 반영)하기 위한 key
const sidebarMenuKey = computed(() =>
  debouncedMenuKeyword.value.trim()
    ? `search:${debouncedMenuKeyword.value.trim().toLowerCase()}`
    : 'default',
);

function toggleSidebar() {
  updatePreferences({
    sidebar: {
      hidden: !preferences.sidebar.hidden,
    },
  });
}

function clearPreferencesAndLogout() {
  emit('clearPreferencesAndLogout');
}

function clickLogo() {
  emit('clickLogo');
}

function autoCollapseMenuByRouteMeta(route: RouteLocationNormalizedLoaded) {
  // 2열 모드에서만 유효함
  if (
    ['header-mixed-nav', 'sidebar-mixed-nav'].includes(
      preferences.app.layout,
    ) &&
    route.meta &&
    route.meta.hideInMenu
  ) {
    sidebarExtraVisible.value = false;
  }
}

const route = useRoute();

onMounted(() => {
  autoCollapseMenuByRouteMeta(route);
});

watch(
  () => preferences.app.layout,
  async (val) => {
    if (val === 'sidebar-mixed-nav' && preferences.sidebar.hidden) {
      updatePreferences({
        sidebar: {
          hidden: false,
        },
      });
    }
  },
);

const tabbarStore = useTabbarStore();

function refreshAll() {
  tabbarStore.cachedTabs.clear();
  refresh();
}

// 언어 업데이트 후 페이지 새로고침
// i18n.global.locale은 preference.app.locale이 변경된 후에 업데이트되므로 preference.app.locale을 감시하는 것은 부적절합니다. 페이지를 새로 고칠 때 언어 설정이 아직 완전히 로드되지 않았을 수 있습니다.
watch(i18n.global.locale, refreshAll, { flush: 'post' });

// 시간대 업데이트 후 페이지 새로고침
watch(() => timezoneStore.timezone, refreshAll, { flush: 'post' });

const slots: SetupContext['slots'] = useSlots();
const headerSlots = computed(() => {
  return Object.keys(slots).filter((key) => key.startsWith('header-'));
});

// 헤더와 탭바 높이를 감산한 동적 뷰포트 높이 계산식
const contentHeightStyle = computed(() => {
  const headerHeight = preferences.header.enable ? preferences.header.height : 0;
  const tabbarHeight = preferences.tabbar.enable ? preferences.tabbar.height : 0;
  return {
    height: `calc(100vh - ${headerHeight + tabbarHeight}px)`,
  };
});

// AI 고정 사이드바 마우스 드래그 너비 조절 로직
const STORAGE_KEY = 'vben_ai_chat_sidebar_width';

function getSavedWidth(): number {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      const width = parseInt(saved, 10);
      if (!isNaN(width) && width >= 280 && width <= 800) {
        return width;
      }
    }
  } catch (e) {
    console.error('Failed to read AI chat sidebar width from localStorage:', e);
  }
  return 384; // 기본 너비 384px (w-96)
}

const aiChatWidth = ref(getSavedWidth());
const isResizing = ref(false);

function startResize(e: MouseEvent) {
  isResizing.value = true;
  const startX = e.clientX;
  const startWidth = aiChatWidth.value;

  function doResize(moveEvent: MouseEvent) {
    if (!isResizing.value) return;
    // 우측 고정형이므로 마우스가 왼쪽으로 갈수록 사이드바 너비가 커짐
    const deltaX = startX - moveEvent.clientX;
    const newWidth = startWidth + deltaX;

    // 최소 280px, 최대 800px 범위 제한
    if (newWidth >= 280 && newWidth <= 800) {
      aiChatWidth.value = newWidth;
    }
  }

  function stopResize() {
    isResizing.value = false;
    window.removeEventListener('mousemove', doResize);
    window.removeEventListener('mouseup', stopResize);

    // 드래그가 완료되었을 때 최종 너비 상태를 로컬 저장소에 영속화
    try {
      localStorage.setItem(STORAGE_KEY, String(aiChatWidth.value));
    } catch (e) {
      console.error('Failed to save AI chat sidebar width to localStorage:', e);
    }
  }

  window.addEventListener('mousemove', doResize);
  window.addEventListener('mouseup', stopResize);
}
</script>

<template>
  <VbenAdminLayout
    v-model:sidebar-extra-visible="sidebarExtraVisible"
    :content-compact="preferences.app.contentCompact"
    :content-compact-width="preferences.app.contentCompactWidth"
    :content-padding="preferences.app.contentPadding"
    :content-padding-bottom="preferences.app.contentPaddingBottom"
    :content-padding-left="preferences.app.contentPaddingLeft"
    :content-padding-right="preferences.app.contentPaddingRight"
    :content-padding-top="preferences.app.contentPaddingTop"
    :footer-enable="preferences.footer.enable"
    :footer-fixed="preferences.footer.fixed"
    :footer-height="preferences.footer.height"
    :header-height="preferences.header.height"
    :header-hidden="preferences.header.hidden"
    :header-mode="preferences.header.mode"
    :header-theme="headerTheme"
    :header-toggle-sidebar-button="preferences.widget.sidebarToggle"
    :header-visible="preferences.header.enable"
    :is-mobile="preferences.app.isMobile"
    :layout="layout"
    :sidebar-draggable="preferences.sidebar.draggable"
    :sidebar-collapse="preferences.sidebar.collapsed"
    :sidebar-collapse-show-title="preferences.sidebar.collapsedShowTitle"
    :sidebar-enable="sidebarVisible"
    :sidebar-collapsed-button="preferences.sidebar.collapsedButton"
    :sidebar-fixed-button="preferences.sidebar.fixedButton"
    :sidebar-expand-on-hover="preferences.sidebar.expandOnHover"
    :sidebar-extra-collapse="preferences.sidebar.extraCollapse"
    :sidebar-extra-collapsed-width="preferences.sidebar.extraCollapsedWidth"
    :sidebar-hidden="preferences.sidebar.hidden"
    :sidebar-mixed-width="preferences.sidebar.mixedWidth"
    :sidebar-theme="sidebarTheme"
    :sidebar-theme-sub="sidebarThemeSub"
    :sidebar-width="preferences.sidebar.width"
    :side-collapse-width="preferences.sidebar.collapseWidth"
    :tabbar-enable="preferences.tabbar.enable"
    :tabbar-height="preferences.tabbar.height"
    :z-index="preferences.app.zIndex"
    @side-mouse-leave="handleSideMouseLeave"
    @toggle-sidebar="toggleSidebar"
    @update:sidebar-collapse="
      (value: boolean) => updatePreferences({ sidebar: { collapsed: value } })
    "
    @update:sidebar-enable="
      (value: boolean) => updatePreferences({ sidebar: { enable: value } })
    "
    @update:sidebar-expand-on-hover="
      (value: boolean) =>
        updatePreferences({ sidebar: { expandOnHover: value } })
    "
    @update:sidebar-extra-collapse="
      (value: boolean) =>
        updatePreferences({ sidebar: { extraCollapse: value } })
    "
    @update:sidebar-width="
      (value: number) => updatePreferences({ sidebar: { width: value } })
    "
  >
    <!-- 로고 -->
    <template #logo>
      <VbenLogo
        v-if="preferences.logo.enable"
        :fit="preferences.logo.fit"
        :class="logoClass"
        :collapsed="logoCollapsed"
        :src="preferences.logo.source"
        :src-dark="preferences.logo.sourceDark"
        :text="preferences.app.name"
        :theme="showHeaderNav ? headerTheme : theme"
        @click="clickLogo"
      >
        <template v-if="$slots['logo-text']" #text>
          <slot name="logo-text"></slot>
        </template>
      </VbenLogo>
    </template>
    <!-- 헤더 영역 -->
    <template #header>
      <LayoutHeader
        :theme="theme"
        @clear-preferences-and-logout="clearPreferencesAndLogout"
      >
        <template
          v-if="!showHeaderNav && preferences.breadcrumb.enable"
          #breadcrumb
        >
          <Breadcrumb
            :hide-when-only-one="preferences.breadcrumb.hideOnlyOne"
            :show-home="preferences.breadcrumb.showHome"
            :show-icon="preferences.breadcrumb.showIcon"
            :type="preferences.breadcrumb.styleType"
          />
        </template>
        <template v-if="showHeaderNav" #menu>
          <LayoutMenu
            :default-active="headerActive"
            :menus="wrapperMenus(headerMenus)"
            :rounded="isMenuRounded"
            :theme="headerTheme"
            class="w-full"
            mode="horizontal"
            @select="handleMenuSelect"
          />
        </template>
        <template #user-dropdown>
          <slot name="user-dropdown"></slot>
        </template>
        <template #notification>
          <slot name="notification"></slot>
        </template>
        <template #timezone>
          <slot name="timezone"></slot>
        </template>
        <template v-for="item in headerSlots" #[item]>
          <slot :name="item"></slot>
        </template>
      </LayoutHeader>
    </template>
    <!-- 사이드 메뉴 영역 -->
    <template #menu>
      <!-- 메뉴 검색 입력부: 접힌 사이드바에서는 숨김, 스크롤 시 상단 고정 -->
      <div
        v-if="!sidebarCollapsed"
        class="bg-sidebar sticky top-0 z-20 -mt-2 px-2 pb-2 pt-2"
      >
        <div class="relative">
          <svg
            class="text-muted-foreground pointer-events-none absolute left-2 top-1/2 size-4 -translate-y-1/2"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            viewBox="0 0 24 24"
          >
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.3-4.3" stroke-linecap="round" />
          </svg>
          <Input
            v-model="menuSearchKeyword"
            :placeholder="$t('common.search')"
            class="h-8 pl-8 pr-8"
            spellcheck="false"
          />
          <button
            v-if="menuSearchKeyword"
            :aria-label="$t('common.reset')"
            class="text-muted-foreground hover:text-foreground absolute right-2 top-1/2 -translate-y-1/2"
            type="button"
            @click="menuSearchKeyword = ''"
          >
            <svg
              class="size-4"
              fill="none"
              stroke="currentColor"
              stroke-linecap="round"
              stroke-width="2"
              viewBox="0 0 24 24"
            >
              <path d="M18 6 6 18M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>
      <LayoutMenu
        :key="sidebarMenuKey"
        :accordion="preferences.navigation.accordion"
        :collapse="preferences.sidebar.collapsed"
        :collapse-show-title="preferences.sidebar.collapsedShowTitle"
        :default-active="sidebarActive"
        :default-openeds="sidebarSearchOpenPaths"
        :menus="filteredSidebarMenus"
        :rounded="isMenuRounded"
        :theme="sidebarTheme"
        mode="vertical"
        @open="handleMenuOpen"
        @select="handleMenuSelect"
      />
    </template>
    <template #mixed-menu>
      <LayoutMixedMenu
        :active-path="extraActiveMenu"
        :menus="wrapperMenus(mixHeaderMenus, false)"
        :rounded="isMenuRounded"
        :theme="sidebarTheme"
        @default-select="handleDefaultSelect"
        @enter="handleMenuMouseEnter"
        @select="handleMixedMenuSelect"
      />
    </template>
    <!-- 사이드 추가 영역 -->
    <template #side-extra>
      <LayoutExtraMenu
        :accordion="preferences.navigation.accordion"
        :collapse="preferences.sidebar.extraCollapse"
        :menus="wrapperMenus(extraMenus)"
        :rounded="isMenuRounded"
        :theme="sidebarThemeSub"
      />
    </template>
    <template #side-extra-title>
      <VbenLogo
        v-if="preferences.logo.enable"
        :fit="preferences.logo.fit"
        :text="preferences.app.name"
        :theme="sidebarThemeSub"
      >
        <template v-if="$slots['logo-text']" #text>
          <slot name="logo-text"></slot>
        </template>
      </VbenLogo>
    </template>

    <template #tabbar>
      <LayoutTabbar
        v-if="preferences.tabbar.enable"
        :show-icon="preferences.tabbar.showIcon"
        :theme="theme"
      />
    </template>

    <!-- 본문 내용 -->
    <template #content>
      <div 
        :style="contentHeightStyle" 
        :class="{ 'select-none': isResizing }" 
        class="flex w-full overflow-hidden"
      >
        <!-- 메인 콘텐츠 영역 -->
        <div class="flex-1 min-w-0 h-full overflow-auto">
          <LayoutContent />
        </div>

        <!-- 마우스 드래그를 이용한 사이드바 너비 조절용 스플리터 바 -->
        <div
          v-if="isAiChatPinned"
          :class="[
            'w-1 cursor-col-resize shrink-0 h-full transition-all duration-150',
            isResizing ? 'bg-primary' : 'bg-border/60 hover:bg-primary/50'
          ]"
          @mousedown="startResize"
        ></div>

        <!-- 핀 고정된 AI 채팅 사이드바 (반응형 너비 스타일 바인딩, 드래그 랙 방지를 위해 트랜지션 제외) -->
        <div 
          v-if="isAiChatPinned" 
          :style="{ width: `${aiChatWidth}px` }"
          class="border-l bg-background shrink-0 h-full flex flex-col shadow-md"
        >
          <!-- 핀 고정 모드로 AiChatContent를 렌더링 -->
          <AiChatContent mode="pinned" />
        </div>
      </div>
    </template>

    <template v-if="preferences.transition.loading" #content-overlay>
      <LayoutContentSpinner />
    </template>

    <!-- 푸터 -->
    <template v-if="preferences.footer.enable" #footer>
      <LayoutFooter>
        <Copyright
          v-if="preferences.copyright.enable"
          v-bind="preferences.copyright"
        />
      </LayoutFooter>
    </template>

    <template #extra>
      <slot name="extra"></slot>
      <CheckUpdates
        v-if="preferences.app.enableCheckUpdates"
        :check-updates-interval="preferences.app.checkUpdatesInterval"
      />

      <Transition v-if="preferences.widget.lockScreen" name="slide-up">
        <slot v-if="accessStore.isLockScreen" name="lock-screen"></slot>
      </Transition>

      <template v-if="preferencesButtonPosition.fixed">
        <Preferences
          class="fixed top-1/2 right-0 z-100 -translate-y-1/2 transform"
          @clear-preferences-and-logout="clearPreferencesAndLogout"
        />
      </template>
      <VbenBackTop />
    </template>
  </VbenAdminLayout>
</template>
