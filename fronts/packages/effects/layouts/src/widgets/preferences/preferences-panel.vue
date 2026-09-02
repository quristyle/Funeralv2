<script setup lang="ts">
/**
 * [환경설정 내용]
 *
 * 탭과 설정 블록만 갖는다. **껍데기(드로어냐 페이지냐)는 모른다.**
 *
 * 예전에는 이 내용이 `preferences-drawer.vue` 안에 `<Drawer>` 와 함께 있었다.
 * 그래서 같은 설정을 `/setting/environment` 페이지에서도 다루려면 통째로
 * 복사해야 했고, 복사하면 설정이 하나 늘 때마다 두 곳을 고쳐야 한다.
 * 껍데기만 벗겨 내용을 공유한다.
 *
 *   preferences-drawer.vue  = <Drawer> + 이 패널   (헤더 톱니)
 *   preferences-view.vue    = 페이지   + 이 패널   (/setting/environment)
 *
 * [설정 값 주고받기]
 * 값은 `defineModel` 로 받는다. 껍데기는 이 목록을 알 필요가 없다 —
 * `usePreferencesBinding()` 이 스토어를 훑어 만든 props·listener 를
 * `v-bind="$attrs"` 로 그대로 흘려보내면 여기서 `v-model` 이 잡는다.
 * 그래서 **설정 항목이 늘어도 고칠 곳은 이 파일 하나**다.
 */
import type { SupportedLanguagesType } from '@vben/locales';
import type { CustomPreferencesRecord } from '@vben/preferences';
import type {
  BreadcrumbStyleType,
  BuiltinThemeType,
  ContentCompactType,
  LayoutHeaderMenuAlignType,
  LayoutHeaderModeType,
  LayoutType,
  NavigationStyleType,
  PreferencesButtonPositionType,
  SidebarMenuSelectBehavior,
  ThemeModeType,
} from '@vben/types';

import type { SegmentedItem } from '@vben-core/shadcn-ui';

import { computed, ref } from 'vue';

import { $t } from '@vben/locales';
import {
  preferences,
  updateCustomPreferences,
  usePreferences,
} from '@vben/preferences';

import { VbenSegmented } from '@vben-core/shadcn-ui';

import {
  Animation,
  Block,
  Breadcrumb,
  BuiltinTheme,
  ColorMode,
  Content,
  Custom,
  FontFamily,
  FontSize,
  Footer,
  General,
  GlobalShortcutKeys,
  Header,
  Layout,
  Navigation,
  Radius,
  Sidebar,
  Tabbar,
  Theme,
  Widget,
} from './blocks';

interface Props {
  /**
   * 탭 머리를 스크롤에 붙여 둘지.
   *
   * 드로어는 사용자 설정(`app.enableStickyPreferencesNavigationBar`)을 따르고,
   * 페이지는 화면이 넓어 굳이 붙일 필요가 없다.
   */
  stickyTabs?: boolean;
}

withDefaults(defineProps<Props>(), { stickyTabs: false });

const appLocale = defineModel<SupportedLanguagesType>('appLocale');
const appTimezone = defineModel<string>('appTimezone');
const appDynamicTitle = defineModel<boolean>('appDynamicTitle');
const appFontFamily = defineModel<string>('appFontFamily');
const appLayout = defineModel<LayoutType>('appLayout');
const appColorGrayMode = defineModel<boolean>('appColorGrayMode');
const appColorWeakMode = defineModel<boolean>('appColorWeakMode');
const appContentCompact = defineModel<ContentCompactType>('appContentCompact');
const appWatermark = defineModel<boolean>('appWatermark');
const appWatermarkContent = defineModel<string>('appWatermarkContent');
const appEnableCheckUpdates = defineModel<boolean>('appEnableCheckUpdates');
const appEnableCopyPreferences = defineModel<boolean>(
  'appEnableCopyPreferences',
);
const appPreferencesButtonPosition = defineModel<PreferencesButtonPositionType>(
  'appPreferencesButtonPosition',
);

const transitionProgress = defineModel<boolean>('transitionProgress');
const transitionName = defineModel<string>('transitionName');
const transitionLoading = defineModel<boolean>('transitionLoading');
const transitionEnable = defineModel<boolean>('transitionEnable');

const themeColorPrimary = defineModel<string>('themeColorPrimary');
const themeBuiltinType = defineModel<BuiltinThemeType>('themeBuiltinType');
const themeMode = defineModel<ThemeModeType>('themeMode');
const themeRadius = defineModel<string>('themeRadius');
const themeFontSize = defineModel<number>('themeFontSize');
const themeSemiDarkSidebar = defineModel<boolean>('themeSemiDarkSidebar');
const themeSemiDarkSidebarSub = defineModel<boolean>('themeSemiDarkSidebarSub');
const themeSemiDarkHeader = defineModel<boolean>('themeSemiDarkHeader');

const sidebarEnable = defineModel<boolean>('sidebarEnable');
const sidebarWidth = defineModel<number>('sidebarWidth');
const sidebarDraggable = defineModel<boolean>('sidebarDraggable');
const sidebarCollapsed = defineModel<boolean>('sidebarCollapsed');
const sidebarOnMenuSelect =
  defineModel<SidebarMenuSelectBehavior>('sidebarOnMenuSelect');
const sidebarCollapsedShowTitle = defineModel<boolean>(
  'sidebarCollapsedShowTitle',
);
const sidebarAutoActivateChild = defineModel<boolean>(
  'sidebarAutoActivateChild',
);
const sidebarExpandOnHover = defineModel<boolean>('sidebarExpandOnHover');
const sidebarScrollToActive = defineModel<boolean>('sidebarScrollToActive');
const sidebarCollapsedButton = defineModel<boolean>('sidebarCollapsedButton');
const sidebarFixedButton = defineModel<boolean>('sidebarFixedButton');
const headerEnable = defineModel<boolean>('headerEnable');
const headerMode = defineModel<LayoutHeaderModeType>('headerMode');
const headerMenuAlign =
  defineModel<LayoutHeaderMenuAlignType>('headerMenuAlign');

const breadcrumbEnable = defineModel<boolean>('breadcrumbEnable');
const breadcrumbShowIcon = defineModel<boolean>('breadcrumbShowIcon');
const breadcrumbShowHome = defineModel<boolean>('breadcrumbShowHome');
const breadcrumbStyleType = defineModel<BreadcrumbStyleType>(
  'breadcrumbStyleType',
);
const breadcrumbHideOnlyOne = defineModel<boolean>('breadcrumbHideOnlyOne');

const tabbarEnable = defineModel<boolean>('tabbarEnable');
const tabbarShowIcon = defineModel<boolean>('tabbarShowIcon');
const tabbarShowMore = defineModel<boolean>('tabbarShowMore');
const tabbarShowMaximize = defineModel<boolean>('tabbarShowMaximize');
const tabbarPersist = defineModel<boolean>('tabbarPersist');
const tabbarVisitHistory = defineModel<boolean>('tabbarVisitHistory');
const tabbarDraggable = defineModel<boolean>('tabbarDraggable');
const tabbarWheelable = defineModel<boolean>('tabbarWheelable');
const tabbarStyleType = defineModel<string>('tabbarStyleType');
const tabbarMaxCount = defineModel<number>('tabbarMaxCount');
const tabbarMiddleClickToClose = defineModel<boolean>(
  'tabbarMiddleClickToClose',
);

const navigationStyleType = defineModel<NavigationStyleType>(
  'navigationStyleType',
);
const navigationSplit = defineModel<boolean>('navigationSplit');
const navigationAccordion = defineModel<boolean>('navigationAccordion');

const footerEnable = defineModel<boolean>('footerEnable');
const footerFixed = defineModel<boolean>('footerFixed');


const shortcutKeysEnable = defineModel<boolean>('shortcutKeysEnable');
const shortcutKeysGlobalSearch = defineModel<boolean>(
  'shortcutKeysGlobalSearch',
);
const shortcutKeysGlobalLogout = defineModel<boolean>(
  'shortcutKeysGlobalLogout',
);
const shortcutKeysGlobalEscape = defineModel<boolean>(
  'shortcutKeysGlobalEscape',
);
const shortcutKeysGlobalLockScreen = defineModel<boolean>(
  'shortcutKeysGlobalLockScreen',
);
// 이것이 빠져 있었다. 블록에는 스위치가 있는데(`blocks/shortcut-keys/global.vue`)
// 여기서 잇지 않아 눌러도 저장되지 않았다 — 23번 문서 5.5절.
const shortcutKeysGlobalPreferences = defineModel<boolean>(
  'shortcutKeysGlobalPreferences',
);

const widgetGlobalSearchButtonPosition = defineModel<string>(
  'widgetGlobalSearchButtonPosition',
);
const widgetFullscreenButtonPosition = defineModel<string>(
  'widgetFullscreenButtonPosition',
);
const widgetLanguageToggleButtonPosition = defineModel<string>(
  'widgetLanguageToggleButtonPosition',
);
const widgetNotificationButtonPosition = defineModel<string>(
  'widgetNotificationButtonPosition',
);
const widgetThemeToggleButtonPosition = defineModel<string>(
  'widgetThemeToggleButtonPosition',
);
const widgetLockScreenButtonPosition = defineModel<string>(
  'widgetLockScreenButtonPosition',
);
const widgetLogoutButtonPosition = defineModel<string>(
  'widgetLogoutButtonPosition',
);
const widgetOrder = defineModel<string[]>('widgetOrder', { required: true });
const widgetRefreshButtonPosition = defineModel<string>(
  'widgetRefreshButtonPosition',
);
const widgetTimezoneButtonPosition = defineModel<string>(
  'widgetTimezoneButtonPosition',
);

const {
  customPreferences,
  isDark,
  isFullContent,
  isHeaderNav,
  isHeaderSidebarNav,
  isMixedNav,
  preferencesExtension,
  isSideMixedNav,
  isSideMode,
  isSideNav,
} = usePreferences();

const activeTab = ref('appearance');

const customPreferencesTab = computed(() => preferencesExtension.value);

const customTabLabel = computed(() =>
  customPreferencesTab.value?.tabLabel
    ? $t(customPreferencesTab.value.tabLabel)
    : '',
);

const customTabTitle = computed(() => {
  const title =
    customPreferencesTab.value?.title || customPreferencesTab.value?.tabLabel;
  return title ? $t(title) : '';
});

const showCustomTab = computed(
  () => (customPreferencesTab.value?.fields.length ?? 0) > 0,
);

const tabs = computed((): SegmentedItem[] => {
  const items: SegmentedItem[] = [
    { label: $t('preferences.appearance'), value: 'appearance' },
    { label: $t('preferences.layout'), value: 'layout' },
    { label: $t('preferences.shortcutKeys.title'), value: 'shortcutKey' },
    { label: $t('preferences.general'), value: 'general' },
  ];

  if (showCustomTab.value) {
    items.push({ label: customTabLabel.value, value: 'custom' });
  }

  return items;
});

const showBreadcrumbConfig = computed(() => {
  return (
    !isFullContent.value &&
    !isMixedNav.value &&
    !isHeaderNav.value &&
    preferences.header.enable
  );
});

function handleCustomPreferencesUpdate(updates: CustomPreferencesRecord) {
  updateCustomPreferences(updates);
}
</script>

<template>
  <VbenSegmented
    v-model="activeTab"
    :tabs="tabs"
    :class="{ 'sticky-tabs-header': stickyTabs }"
  >
    <template #general>
      <Block :title="$t('preferences.general')">
        <General
          v-model:app-dynamic-title="appDynamicTitle"
          v-model:app-enable-check-updates="appEnableCheckUpdates"
          v-model:app-enable-copy-preferences="appEnableCopyPreferences"
          v-model:app-font-family="appFontFamily"
          v-model:app-locale="appLocale"
          v-model:app-timezone="appTimezone"
          v-model:app-watermark="appWatermark"
          v-model:app-watermark-content="appWatermarkContent"
        />
      </Block>

      <Block :title="$t('preferences.animation.title')">
        <Animation
          v-model:transition-enable="transitionEnable"
          v-model:transition-loading="transitionLoading"
          v-model:transition-name="transitionName"
          v-model:transition-progress="transitionProgress"
        />
      </Block>
    </template>

    <template #appearance>
      <Block :title="$t('preferences.theme.title')">
        <Theme
          v-model="themeMode"
          v-model:theme-semi-dark-header="themeSemiDarkHeader"
          v-model:theme-semi-dark-sidebar="themeSemiDarkSidebar"
          v-model:theme-semi-dark-sidebar-sub="themeSemiDarkSidebarSub"
        />
      </Block>
      <Block :title="$t('preferences.theme.builtin.title')">
        <BuiltinTheme
          v-model="themeBuiltinType"
          v-model:theme-color-primary="themeColorPrimary"
          :is-dark="isDark"
        />
      </Block>
      <Block :title="$t('preferences.theme.radius')">
        <Radius v-model="themeRadius" />
      </Block>
      <!--
        여기에는 Block 제목을 주지 않는다. FontFamily 는 SelectItem 부품이라
        **행 안에 자기 라벨을 이미 갖고 있다**(일반 탭의 언어·시간대와 같은 모양).
        제목을 주면 '기본 글꼴' 이 두 번 나온다.
        이웃한 테두리 반경·글꼴 크기는 자체 라벨이 없어 Block 제목이 필요하다.
      -->
      <Block>
        <FontFamily v-model="appFontFamily" />
      </Block>
      <Block :title="$t('preferences.theme.fontSize')">
        <FontSize v-model="themeFontSize" />
      </Block>
      <Block :title="$t('preferences.other')">
        <ColorMode
          v-model:app-color-gray-mode="appColorGrayMode"
          v-model:app-color-weak-mode="appColorWeakMode"
        />
      </Block>
    </template>

    <template #layout>
      <Block :title="$t('preferences.layout')">
        <Layout v-model="appLayout" />
      </Block>
      <Block :title="$t('preferences.content')">
        <Content v-model="appContentCompact" />
      </Block>

      <Block :title="$t('preferences.sidebar.title')">
        <Sidebar
          v-model:sidebar-auto-activate-child="sidebarAutoActivateChild"
          v-model:sidebar-draggable="sidebarDraggable"
          v-model:sidebar-collapsed="sidebarCollapsed"
          v-model:sidebar-on-menu-select="sidebarOnMenuSelect"
          v-model:sidebar-collapsed-show-title="sidebarCollapsedShowTitle"
          v-model:sidebar-enable="sidebarEnable"
          v-model:sidebar-expand-on-hover="sidebarExpandOnHover"
          v-model:sidebar-scroll-to-active="sidebarScrollToActive"
          v-model:sidebar-width="sidebarWidth"
          v-model:sidebar-collapsed-button="sidebarCollapsedButton"
          v-model:sidebar-fixed-button="sidebarFixedButton"
          :current-layout="appLayout"
          :disabled="!isSideMode"
        />
      </Block>

      <Block :title="$t('preferences.header.title')">
        <Header
          v-model:header-enable="headerEnable"
          v-model:header-menu-align="headerMenuAlign"
          v-model:header-mode="headerMode"
          :disabled="isFullContent"
        />
      </Block>

      <Block :title="$t('preferences.navigationMenu.title')">
        <Navigation
          v-model:navigation-accordion="navigationAccordion"
          v-model:navigation-split="navigationSplit"
          v-model:navigation-style-type="navigationStyleType"
          :disabled="isFullContent"
          :disabled-navigation-split="!isMixedNav"
        />
      </Block>

      <Block :title="$t('preferences.breadcrumb.title')">
        <Breadcrumb
          v-model:breadcrumb-enable="breadcrumbEnable"
          v-model:breadcrumb-hide-only-one="breadcrumbHideOnlyOne"
          v-model:breadcrumb-show-home="breadcrumbShowHome"
          v-model:breadcrumb-show-icon="breadcrumbShowIcon"
          v-model:breadcrumb-style-type="breadcrumbStyleType"
          :disabled="
            !showBreadcrumbConfig ||
            !(isSideNav || isSideMixedNav || isHeaderSidebarNav)
          "
        />
      </Block>
      <Block :title="$t('preferences.tabbar.title')">
        <Tabbar
          v-model:tabbar-draggable="tabbarDraggable"
          v-model:tabbar-enable="tabbarEnable"
          v-model:tabbar-persist="tabbarPersist"
          v-model:tabbar-visit-history="tabbarVisitHistory"
          v-model:tabbar-show-icon="tabbarShowIcon"
          v-model:tabbar-show-maximize="tabbarShowMaximize"
          v-model:tabbar-show-more="tabbarShowMore"
          v-model:tabbar-style-type="tabbarStyleType"
          v-model:tabbar-wheelable="tabbarWheelable"
          v-model:tabbar-max-count="tabbarMaxCount"
          v-model:tabbar-middle-click-to-close="tabbarMiddleClickToClose"
        />
      </Block>
      <Block :title="$t('preferences.widget.title')">
        <Widget
          v-model:app-preferences-button-position="appPreferencesButtonPosition"
          v-model:widget-fullscreen-button-position="
            widgetFullscreenButtonPosition
          "
          v-model:widget-global-search-button-position="
            widgetGlobalSearchButtonPosition
          "
          v-model:widget-language-toggle-button-position="
            widgetLanguageToggleButtonPosition
          "
          v-model:widget-lock-screen-button-position="
            widgetLockScreenButtonPosition
          "
          v-model:widget-logout-button-position="widgetLogoutButtonPosition"
          v-model:widget-order="widgetOrder"
          v-model:widget-notification-button-position="
            widgetNotificationButtonPosition
          "
          v-model:widget-refresh-button-position="widgetRefreshButtonPosition"
          v-model:widget-theme-toggle-button-position="
            widgetThemeToggleButtonPosition
          "
          v-model:widget-timezone-button-position="
            widgetTimezoneButtonPosition
          "
        />
      </Block>
      <Block :title="$t('preferences.footer.title')">
        <Footer
          v-model:footer-enable="footerEnable"
          v-model:footer-fixed="footerFixed"
        />
      </Block>
    </template>

    <template #shortcutKey>
      <Block :title="$t('preferences.shortcutKeys.global')">
        <GlobalShortcutKeys
          v-model:shortcut-keys-enable="shortcutKeysEnable"
          v-model:shortcut-keys-global-search="shortcutKeysGlobalSearch"
          v-model:shortcut-keys-lock-screen="shortcutKeysGlobalLockScreen"
          v-model:shortcut-keys-logout="shortcutKeysGlobalLogout"
          v-model:shortcut-keys-preferences="shortcutKeysGlobalPreferences"
          v-model:shortcut-keys-escape="shortcutKeysGlobalEscape"
        />
      </Block>
    </template>

    <template #custom>
      <Block :title="customTabTitle">
        <Custom
          :fields="customPreferencesTab?.fields || []"
          :values="customPreferences"
          @update="handleCustomPreferencesUpdate"
        />
      </Block>
    </template>
  </VbenSegmented>
</template>

<style scoped>
:deep(.sticky-tabs-header [role='tablist']) {
  @apply -top-3 z-9999 sticky;
}
</style>
