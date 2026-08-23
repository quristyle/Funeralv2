type LayoutType =
  | 'full-content'
  | 'header-mixed-nav'
  | 'header-nav'
  | 'header-sidebar-nav'
  | 'mixed-nav'
  | 'sidebar-mixed-nav'
  | 'sidebar-nav';

type ThemeModeType = 'auto' | 'dark' | 'light';

/**
 * 偏好设置按钮位置
 * auto 自动（按布局上下文在 header/fixed 间切换）
 * fixed 固定在屏幕右边缘
 * header 顶栏
 * user-dropdown 用户的下拉弹出框中
 * none 不显示
 */
type PreferencesButtonPositionType =
  | 'auto'
  | 'fixed'
  | 'header'
  | 'none'
  | 'user-dropdown';

type BuiltinThemeType =
  | 'custom'
  | 'deep-blue'
  | 'deep-green'
  | 'default'
  | 'gray'
  | 'green'
  | 'neutral'
  | 'orange'
  | 'pink'
  | 'red'
  | 'rose'
  | 'sky-blue'
  | 'slate'
  | 'stone'
  | 'violet'
  | 'yellow'
  | 'zinc'
  | (Record<never, never> & string);

type ContentCompactType = 'compact' | 'wide';

type LayoutHeaderModeType = 'auto' | 'auto-scroll' | 'fixed' | 'static';
type LayoutHeaderMenuAlignType = 'center' | 'end' | 'start';

/**
 * 로그인 만료 모드
 * modal 팝업 모드
 * page 페이지 모드
 */
type LoginExpiredModeType = 'modal' | 'page';

/**
 * 브레드크럼 스타일
 * background 배경
 * normal 기본
 */
type BreadcrumbStyleType = 'background' | 'normal';

/**
 * 권한 모드
 * backend 백엔드 권한 모드
 * frontend 프런트엔드 권한 모드
 * mixed 혼합 권한 모드
 */
type AccessModeType = 'backend' | 'frontend' | 'mixed';

/**
 * 내비게이션 스타일
 * plain 단순
 * rounded 둥글게
 */
type NavigationStyleType = 'plain' | 'rounded';

/**
 * 탭 바 스타일
 * brisk 경쾌
 * card 카드
 * chrome 크롬
 * plain 단순
 */
type TabsStyleType = 'brisk' | 'card' | 'chrome' | 'plain';

/**
 * 페이지 전환 애니메이션
 */
type PageTransitionType = 'fade' | 'fade-down' | 'fade-slide' | 'fade-up';

/**
 * 페이지 전환 애니메이션
 * panel-center 중앙 레이아웃
 * panel-left 왼쪽 레이아웃
 * panel-right 오른쪽 레이아웃
 */
type AuthPageLayoutType = 'panel-center' | 'panel-left' | 'panel-right';

/**
 * 시간대 옵션
 */
interface TimezoneOption {
  label: string;
  offset: number;
  timezone: string;
}

export type {
  AccessModeType,
  AuthPageLayoutType,
  BreadcrumbStyleType,
  BuiltinThemeType,
  ContentCompactType,
  LayoutHeaderMenuAlignType,
  LayoutHeaderModeType,
  LayoutType,
  LoginExpiredModeType,
  NavigationStyleType,
  PageTransitionType,
  PreferencesButtonPositionType,
  TabsStyleType,
  ThemeModeType,
  TimezoneOption,
};
