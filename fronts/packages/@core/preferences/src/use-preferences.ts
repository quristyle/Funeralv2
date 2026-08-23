import { computed } from 'vue';

import { diff, diffStrict } from '@vben-core/shared/utils';

import { preferencesManager } from './preferences';
import { isDarkTheme } from './update-css-variables';

function usePreferences() {
  const preferences = preferencesManager.getPreferences();
  const customPreferences = preferencesManager.getCustomPreferences();
  const initialPreferences = preferencesManager.getInitialPreferences();
  const initialCustomPreferences =
    preferencesManager.getInitialCustomPreferences();
  const preferencesExtension = computed(() =>
    preferencesManager.getPreferencesExtension(),
  );
  /**
   * @ko_KR 환경 설정 변경 사항을 계산합니다.
   * @zh_CN 使用 diffStrict：图标排序等数组字段需顺序敏感比较
   */
  const diffPreference = computed(() => {
    return diffStrict(initialPreferences, preferences);
  });

  const diffCustomPreference = computed(() => {
    return diff(initialCustomPreferences, customPreferences);
  });

  const appPreferences = computed(() => preferences.app);

  const shortcutKeysPreferences = computed(() => preferences.shortcutKeys);

  /**
   * @ko_KR 다크 모드인지 확인합니다.
   * @param preferences - 현재 환경 설정 객체, 이 객체의 테마 값을 사용하여 다크 모드 여부를 판단합니다.
   * @returns 테마가 다크 모드이면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
   */
  const isDark = computed(() => {
    return isDarkTheme(preferences.theme.mode);
  });

  const locale = computed(() => {
    return appPreferences.value.locale;
  });

  const isMobile = computed(() => {
    return appPreferences.value.isMobile;
  });

  const theme = computed(() => {
    return isDark.value ? 'dark' : 'light';
  });

  /**
   * @ko_KR 레이아웃 방식
   */
  const layout = computed(() =>
    isMobile.value ? 'sidebar-nav' : appPreferences.value.layout,
  );

  /**
   * @ko_KR 상단 내비게이션 바 표시 여부
   */
  const isShowHeaderNav = computed(() => {
    return preferences.header.enable;
  });

  /**
   * @ko_KR 콘텐츠 전체 화면 표시 여부 (측면, 하단, 상단, 탭 영역 제외)
   */
  const isFullContent = computed(
    () => appPreferences.value.layout === 'full-content',
  );

  /**
   * @ko_KR 측면 내비게이션 모드 여부
   */
  const isSideNav = computed(
    () => appPreferences.value.layout === 'sidebar-nav',
  );

  /**
   * @ko_KR 측면 혼합 모드 여부
   */
  const isSideMixedNav = computed(
    () => appPreferences.value.layout === 'sidebar-mixed-nav',
  );

  /**
   * @ko_KR 헤더 내비게이션 모드 여부
   */
  const isHeaderNav = computed(
    () => appPreferences.value.layout === 'header-nav',
  );

  /**
   * @ko_KR 헤더 혼합 내비게이션 모드 여부
   */
  const isHeaderMixedNav = computed(
    () => appPreferences.value.layout === 'header-mixed-nav',
  );

  /**
   * @ko_KR 상단 전체 너비 + 측면 내비게이션 모드 여부
   */
  const isHeaderSidebarNav = computed(
    () => appPreferences.value.layout === 'header-sidebar-nav',
  );

  /**
   * @ko_KR 혼합 내비게이션 모드 여부
   */
  const isMixedNav = computed(
    () => appPreferences.value.layout === 'mixed-nav',
  );

  /**
   * @ko_KR 측면 내비게이션 모드 포함 여부
   */
  const isSideMode = computed(() => {
    return (
      isMixedNav.value ||
      isSideMixedNav.value ||
      isSideNav.value ||
      isHeaderMixedNav.value ||
      isHeaderSidebarNav.value
    );
  });

  const sidebarCollapsed = computed(() => {
    return preferences.sidebar.collapsed;
  });

  /**
   * @ko_KR keep-alive 활성화 여부
   * 탭이 표시되고 keep-alive가 활성화된 경우에만 켜집니다.
   */
  const keepAlive = computed(
    () => preferences.tabbar.enable && preferences.tabbar.keepAlive,
  );

  /**
   * @ko_KR 로그인/회원가입 페이지 레이아웃이 왼쪽인지 여부
   */
  const authPanelLeft = computed(() => {
    return appPreferences.value.authPageLayout === 'panel-left';
  });

  /**
   * @ko_KR 로그인/회원가입 페이지 레이아웃이 오른쪽인지 여부
   */
  const authPanelRight = computed(() => {
    return appPreferences.value.authPageLayout === 'panel-right';
  });

  /**
   * @ko_KR 로그인/회원가입 페이지 레이아웃이 가운데인지 여부
   */
  const authPanelCenter = computed(() => {
    return appPreferences.value.authPageLayout === 'panel-center';
  });

  /**
   * @ko_KR 콘텐츠 최대화 여부
   * full-content 모드는 제외됩니다.
   */
  const contentIsMaximize = computed(() => {
    const headerIsHidden = preferences.header.hidden;
    const sidebarIsHidden = preferences.sidebar.hidden;
    return headerIsHidden && sidebarIsHidden && !isFullContent.value;
  });

  /**
   * @ko_KR 전역 검색 단축키 활성화 여부
   */
  const globalSearchShortcutKey = computed(() => {
    const { enable, globalSearch } = shortcutKeysPreferences.value;
    return enable && globalSearch;
  });

  /**
   * @ko_KR 전역 로그아웃 단축키 활성화 여부
   */
  const globalLogoutShortcutKey = computed(() => {
    const { enable, globalLogout } = shortcutKeysPreferences.value;
    return enable && globalLogout;
  });

  /**
   * @ko_KR 전역 로그아웃 단축키 활성화 여부
   */
  /** 환경설정 창 단축키 활성화 여부 */
  const globalPreferencesShortcutKey = computed(() => {
    const { enable, globalPreferences } = shortcutKeysPreferences.value;
    return enable && globalPreferences;
  });

  const globalEscapeShortcutKey = computed(() => {
    const { enable, globalEscape } = shortcutKeysPreferences.value;
    return enable && globalEscape;
  });

  const globalLockScreenShortcutKey = computed(() => {
    const { enable, globalLockScreen } = shortcutKeysPreferences.value;
    return enable && globalLockScreen;
  });

  /**
   * @ko_KR 환경 설정 버튼 위치
   */
  const preferencesButtonPosition = computed(() => {
    const { enablePreferences, preferencesButtonPosition } = preferences.app;
    // 환경 설정 버튼이 활성화되지 않은 경우
    if (!enablePreferences) {
      return {
        fixed: false,
        header: false,
        userDropdown: false,
      };
    }

    const { header, sidebar } = preferences;
    const headerHidden = header.hidden;
    const sidebarHidden = sidebar.hidden;

    const contentIsMaximize = headerHidden && sidebarHidden;

    const isHeaderPosition = preferencesButtonPosition === 'header';
    const isUserDropdownPosition =
      preferencesButtonPosition === 'user-dropdown';

    // 고정 위치가 설정된 경우
    if (preferencesButtonPosition !== 'auto') {
      return {
        fixed: preferencesButtonPosition === 'fixed',
        header: isHeaderPosition,
        userDropdown: isUserDropdownPosition,
      };
    }

    // 전체 화면 모드이거나 상단에 고정되지 않은 경우,
    const fixed =
      contentIsMaximize ||
      isFullContent.value ||
      isMobile.value ||
      !isShowHeaderNav.value;

    return {
      fixed,
      header: !fixed,
      userDropdown: !fixed && isUserDropdownPosition,
    };
  });

  return {
    authPanelCenter,
    authPanelLeft,
    authPanelRight,
    contentIsMaximize,
    customPreferences,
    diffPreference,
    diffCustomPreference,
    globalLockScreenShortcutKey,
    globalLogoutShortcutKey,
    globalEscapeShortcutKey,
    globalSearchShortcutKey,
    globalPreferencesShortcutKey,
    isDark,
    isFullContent,
    isHeaderMixedNav,
    isHeaderNav,
    isHeaderSidebarNav,
    isMixedNav,
    isMobile,
    isSideMixedNav,
    isSideMode,
    isSideNav,
    keepAlive,
    layout,
    locale,
    preferencesExtension,
    preferencesButtonPosition,
    sidebarCollapsed,
    theme,
    app: appPreferences.value,
  };
}

export { usePreferences };
