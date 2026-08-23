import type {
  ContentCompactType,
  LayoutHeaderModeType,
  LayoutType,
  ThemeModeType,
} from '@vben-core/typings';

interface VbenLayoutProps {
  /**
   * 콘텐츠 영역 고정 너비
   * @default 'wide'
   */
  contentCompact?: ContentCompactType;
  /**
   * 고정 너비 레이아웃 너비
   * @default 1200
   */
  contentCompactWidth?: number;
  /**
   * 패딩
   * @default 16
   */
  contentPadding?: number;
  /**
   * 하단 패딩
   * @default 16
   */
  contentPaddingBottom?: number;
  /**
   * 좌측 패딩
   * @default 16
   */
  contentPaddingLeft?: number;
  /**
   * 우측 패딩
   * @default 16
   */
  contentPaddingRight?: number;
  /**
   * 상단 패딩
   * @default 16
   */
  contentPaddingTop?: number;
  /**
   * 푸터 표시 여부
   * @default false
   */
  footerEnable?: boolean;
  /**
   * 푸터 고정 여부
   * @default true
   */
  footerFixed?: boolean;
  /**
   * 푸터 높이
   * @default 32
   */
  footerHeight?: number;

  /**
   * 헤더 높이
   * @default 48
   */
  headerHeight?: number;
  /**
   * 상단 바 숨김 여부
   * @default false
   */
  headerHidden?: boolean;
  /**
   * 헤더 표시 모드
   * @default 'fixed'
   */
  headerMode?: LayoutHeaderModeType;
  /**
   * 헤더 상단 바 테마
   */
  headerTheme?: ThemeModeType;
  /**
   * 헤더 사이드바 전환 버튼 표시 여부
   * @default
   */
  headerToggleSidebarButton?: boolean;
  /**
   * 헤더 표시 여부
   * @default true
   */
  headerVisible?: boolean;
  /**
   * 모바일 표시 여부
   * @default false
   */
  isMobile?: boolean;
  /**
   * 레이아웃 방식
   * sidebar-nav 사이드 메뉴 레이아웃
   * header-nav 상단 메뉴 레이아웃
   * mixed-nav 사이드 및 상단 메뉴 레이아웃
   * sidebar-mixed-nav 사이드 혼합 메뉴 레이아웃
   * full-content 전체 화면 콘텐츠 레이아웃
   * @default sidebar-nav
   */
  layout?: LayoutType;
  /**
   * 사이드 메뉴 접힘 상태
   * @default false
   */
  sidebarCollapse?: boolean;
  /**
   * 사이드 메뉴 접기 버튼
   * @default true
   */
  sidebarCollapsedButton?: boolean;
  /**
   * 사이드 메뉴 접힘 시 타이틀 표시 여부
   * @default true
   */
  sidebarCollapseShowTitle?: boolean;
  /**
   * 사이드바 표시 여부
   * @default true
   */
  sidebarEnable?: boolean;
  /**
   * 사이드 메뉴 접힘 시 추가 너비
   * @default 48
   */
  sidebarExtraCollapsedWidth?: number;
  /**
   * 扩展区域extra-title的高度
   */
  sidebarExtraTitleHeight?: number;
  /**
   * 侧边菜单折叠按钮是否固定
   * @default true
   */
  sidebarFixedButton?: boolean;
  /**
   * 사이드바 숨김 여부
   * @default false
   */
  sidebarHidden?: boolean;
  /**
   * 侧边栏 Logo 区域是否显示
   */
  sidebarLogoVisible: boolean;
  /**
   * 混合侧边栏宽度
   * @default 80
   */
  sidebarMixedWidth?: number;
  /**
   * 사이드바 테마
   * @default dark
   */
  sidebarTheme?: ThemeModeType;
  /**
   * 사이드바 서브 바 테마
   * @default dark
   */
  sidebarThemeSub?: ThemeModeType;
  /**
   * 사이드바 너비
   * @default 210
   */
  sidebarWidth?: number;
  /**
   * 사이드 메뉴 접힘 너비
   * @default 48
   */
  sideCollapseWidth?: number;
  /**
   * 탭 표시 여부
   * @default true
   */
  tabbarEnable?: boolean;
  /**
   * 탭 높이
   * @default 30
   */
  tabbarHeight?: number;
  /**
   * zIndex
   * @default 100
   */
  zIndex?: number;
}
export type { VbenLayoutProps };
