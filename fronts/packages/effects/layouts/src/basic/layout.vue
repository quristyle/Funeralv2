<script lang="ts" setup>
import type { SetupContext } from 'vue';
import type { RouteLocationNormalizedLoaded } from 'vue-router';

import type { MenuRecordRaw } from '@vben/types';

import { computed, onMounted, useSlots, watch } from 'vue';
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
import { VbenBackTop, VbenLogo } from '@vben-core/shadcn-ui';

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
      <LayoutMenu
        :accordion="preferences.navigation.accordion"
        :collapse="preferences.sidebar.collapsed"
        :collapse-show-title="preferences.sidebar.collapsedShowTitle"
        :default-active="sidebarActive"
        :menus="wrapperMenus(sidebarMenus)"
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
      <LayoutContent />
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
