import type {
  AccessModeType,
  AuthPageLayoutType,
  BreadcrumbStyleType,
  BuiltinThemeType,
  ContentCompactType,
  DeepPartial,
  LayoutHeaderMenuAlignType,
  LayoutHeaderModeType,
  LayoutType,
  LoginExpiredModeType,
  NavigationStyleType,
  PageTransitionType,
  PreferencesButtonPositionType,
  TabsStyleType,
  ThemeModeType,
} from '@vben-core/typings';

type SupportedLanguagesType = 'en-US' | 'ko-KR';

interface AppPreferences {
  /** 권한 모드 */
  accessMode: AccessModeType;
  /** 로그인/회원가입 페이지 레이아웃 */
  authPageLayout: AuthPageLayoutType;
  /** 업데이트 확인 폴링 시간 */
  checkUpdatesInterval: number;
  /** 회색 모드 활성화 여부 */
  colorGrayMode: boolean;
  /** 색약 모드 활성화 여부 */
  colorWeakMode: boolean;
  /** 컴팩트 모드 활성화 여부 */
  compact: boolean;
  /** 콘텐츠 컴팩트 모드 활성화 여부 */
  contentCompact: ContentCompactType;
  /** 콘텐츠 컴팩트 너비 */
  contentCompactWidth: number;
  /** 콘텐츠 패딩 */
  contentPadding: number;
  /** 콘텐츠 하단 패딩 */
  contentPaddingBottom: number;
  /** 콘텐츠 좌측 패딩 */
  contentPaddingLeft: number;
  /** 콘텐츠 우측 패딩 */
  contentPaddingRight: number;
  /** 콘텐츠 상단 패딩 */
  contentPaddingTop: number;
  // /** 애플리케이션 기본 아바타 */
  defaultAvatar: string;
  /** 기본 홈페이지 주소 */
  defaultHomePath: string;
  // /** 동적 타이틀 활성화 */
  dynamicTitle: boolean;
  /** 업데이트 확인 활성화 여부 */
  enableCheckUpdates: boolean;
  /** 환경 설정 복사 버튼 표시 여부 */
  enableCopyPreferences: boolean;
  /** 환경 설정 표시 여부 */
  enablePreferences: boolean;
  /**
   * @ko_KR refreshToken 활성화 여부
   */
  enableRefreshToken: boolean;
  /**
   * @ko_KR 환경 설정 내비게이션 바 상단 고정 활성화 여부
   */
  enableStickyPreferencesNavigationBar: boolean;
  /** 모바일 여부 */
  isMobile: boolean;
  /** 레이아웃 방식 */
  layout: LayoutType;
  /** 지원 언어 */
  locale: SupportedLanguagesType;
  /** 로그인 만료 모드 */
  loginExpiredMode: LoginExpiredModeType;
  /** 애플리케이션 이름 */
  name: string;
  /** 환경 설정 버튼 위치 */
  preferencesButtonPosition: PreferencesButtonPositionType;
  /**
   * @ko_KR 워터마크 활성화 여부
   */
  watermark: boolean;
  /**
   * @ko_KR 워터마크 문구
   */
  watermarkContent: string;
  /** z-index */
  zIndex: number;
}

interface BreadcrumbPreferences {
  /** 브레드크럼 활성화 여부 */
  enable: boolean;
  /** 브레드크럼 항목이 하나일 때 숨김 여부 */
  hideOnlyOne: boolean;
  /** 브레드크럼 홈 아이콘 표시 여부 */
  showHome: boolean;
  /** 브레드크럼 아이콘 표시 여부 */
  showIcon: boolean;
  /** 브레드크럼 스타일 */
  styleType: BreadcrumbStyleType;
}

interface CopyrightPreferences {
  /** 저작권 회사명 */
  companyName: string;
  /** 저작권 회사 링크 */
  companySiteLink: string;
  /** 저작권 날짜 */
  date: string;
  /** 저작권 표시 여부 */
  enable: boolean;
  /** ICP 등록 번호 */
  icp: string;
  /** ICP 등록 번호 링크 */
  icpLink: string;
  /** 설정 패널 표시 여부 */
  settingShow?: boolean;
}

interface FooterPreferences {
  /** 푸터 표시 여부 */
  enable: boolean;
  /** 푸터 고정 여부 */
  fixed: boolean;
  /** 푸터 높이 */
  height: number;
}

interface HeaderPreferences {
  /** 헤더 활성화 여부 */
  enable: boolean;
  /** 헤더 높이 */
  height: number;
  /** 헤더 숨김 여부 (CSS) */
  hidden: boolean;
  /** 헤더 메뉴 정렬 */
  menuAlign: LayoutHeaderMenuAlignType;
  /** 헤더 표시 모드 */
  mode: LayoutHeaderModeType;
}

interface LogoPreferences {
  /** 로고 표시 여부 */
  enable: boolean;
  /** 로고 이미지 맞춤 방식 */
  fit: 'contain' | 'cover' | 'fill' | 'none' | 'scale-down';
  /** 로고 주소 */
  source: string;
  /** 다크 테마 로고 주소 (선택 사항, 설정하지 않으면 source 사용) */
  sourceDark?: string;
}

interface NavigationPreferences {
  /** 내비게이션 메뉴 아코디언 모드 */
  accordion: boolean;
  /** 내비게이션 메뉴 분할 여부 (layout=mixed-nav인 경우에만 유효) */
  split: boolean;
  /** 내비게이션 메뉴 스타일 */
  styleType: NavigationStyleType;
}

interface SidebarPreferences {
  /** 디렉토리 클릭 시 하위 메뉴 자동 활성화 */
  autoActivateChild: boolean;
  /** 사이드바 접힘 여부 */
  collapsed: boolean;
  /** 사이드바 접힘 버튼 표시 여부 */
  collapsedButton: boolean;
  /** 사이드바 접힘 시 타이틀 표시 여부 */
  collapsedShowTitle: boolean;
  /** 사이드바 접힘 너비 */
  collapseWidth: number;
  /** 사이드바 메뉴 드래그 가능 여부 */
  draggable: boolean;
  /** 사이드바 표시 여부 */
  enable: boolean;
  /** 사이드바 자동 확장 상태 (Hover) */
  expandOnHover: boolean;
  /** 사이드바 확장 영역 접힘 여부 */
  extraCollapse: boolean;
  /** 사이드바 확장 영역 접힘 너비 */
  extraCollapsedWidth: number;
  /** 사이드바 고정 버튼 표시 여부 */
  fixedButton: boolean;
  /** 사이드바 숨김 여부 (CSS) */
  hidden: boolean;
  /** 혼합 사이드바 너비 */
  mixedWidth: number;
  /** 사이드바 너비 */
  width: number;
}

interface ShortcutKeyPreferences {
  /** 단축키 활성화 여부 (전역) */
  enable: boolean;
  /** 전역 잠금 화면 단축키 활성화 여부 */
  globalLockScreen: boolean;
  /** 전역 로그아웃 단축키 활성화 여부 */
  globalLogout: boolean;
  /** 전역 환경 설정 단축키 활성화 여부 */
  globalPreferences: boolean;
  /** 전역 검색 단축키 활성화 여부 */
  globalSearch: boolean;
}

interface TabbarPreferences {
  /** 멀티 탭 드래그 활성화 여부 */
  draggable: boolean;
  /** 멀티 탭 활성화 여부 */
  enable: boolean;
  /** 탭 높이 */
  height: number;
  /** 탭 캐시(KeepAlive) 기능 활성화 */
  keepAlive: boolean;
  /** 최대 탭 수 제한 */
  maxCount: number;
  /** 마우스 휠 클릭으로 탭 닫기 여부 */
  middleClickToClose: boolean;
  /** 탭 유지(Persist) 여부 */
  persist: boolean;
  /** 탭 아이콘 표시 여부 */
  showIcon: boolean;
  /** 최대화 버튼 표시 여부 */
  showMaximize: boolean;
  /** 더 보기 버튼 표시 여부 */
  showMore: boolean;
  /** 새로고침 버튼 표시 여부 */
  showRefresh: boolean;
  /** 탭 스타일 */
  styleType: TabsStyleType;
  /** 방문 기록 활성화 여부 */
  visitHistory: boolean;
  /** 마우스 휠 응답 활성화 여부 */
  wheelable: boolean;
}

interface ThemePreferences {
  /** 기본 테마 이름 */
  builtinType: BuiltinThemeType;
  /** 에러 색상 */
  colorDestructive: string;
  /** 메인 색상 */
  colorPrimary: string;
  /** 성공 색상 */
  colorSuccess: string;
  /** 경고 색상 */
  colorWarning: string;
  /** 글꼴 크기 (단위: px) */
  fontSize: number;
  /** 현재 테마 */
  mode: ThemeModeType;
  /** 테두리 반경 (Radius) */
  radius: string;
  /** 세미 다크 헤더 활성화 여부 (theme='light'인 경우에만 유효) */
  semiDarkHeader: boolean;
  /** 세미 다크 메뉴 활성화 여부 (theme='light'인 경우에만 유효) */
  semiDarkSidebar: boolean;
  /** 세미 다크 하위 메뉴 활성화 여부 (theme='light'인 경우에만 유효) */
  semiDarkSidebarSub: boolean;
}

interface TransitionPreferences {
  /** 페이지 전환 애니메이션 활성화 여부 */
  enable: boolean;
  // /** 페이지 로딩 표시 활성화 여부 */
  loading: boolean;
  /** 페이지 전환 애니메이션 이름 */
  name: PageTransitionType | string;
  /** 페이지 로딩 진행률 애니메이션 활성화 여부 */
  progress: boolean;
}

interface WidgetPreferences {
  /** 전체화면 위젯 활성화 여부 */
  fullscreen: boolean;
  /** 전역 검색 위젯 활성화 여부 */
  globalSearch: boolean;
  /** 언어 전환 위젯 활성화 여부 */
  languageToggle: boolean;
  /** 잠금 화면 기능 활성화 여부 */
  lockScreen: boolean;
  /** 알림 위젯 표시 여부 */
  notification: boolean;
  /** 새로고침 버튼 표시 여부 */
  refresh: boolean;
  /** 사이드바 토글 위젯 표시 여부 */
  sidebarToggle: boolean;
  /** 테마 전환 위젯 표시 여부 */
  themeToggle: boolean;
  /** 시간대 위젯 표시 여부 */
  timezone: boolean;
}

interface Preferences {
  /** 전역 설정 */
  app: AppPreferences;
  /** 헤더 설정 */
  breadcrumb: BreadcrumbPreferences;
  /** 저작권 설정 */
  copyright: CopyrightPreferences;
  /** 푸터 설정 */
  footer: FooterPreferences;
  /** 브레드크럼 설정 */
  header: HeaderPreferences;
  /** 로고 설정 */
  logo: LogoPreferences;
  /** 내비게이션 설정 */
  navigation: NavigationPreferences;
  /** 단축키 설정 */
  shortcutKeys: ShortcutKeyPreferences;
  /** 사이드바 설정 */
  sidebar: SidebarPreferences;
  /** 탭 바 설정 */
  tabbar: TabbarPreferences;
  /** 테마 설정 */
  theme: ThemePreferences;
  /** 전환 애니메이션 설정 */
  transition: TransitionPreferences;
  /** 위젯 설정 */
  widget: WidgetPreferences;
}

type PreferencesKeys = keyof Preferences;

interface InitialOptions {
  namespace: string;
  overrides?: DeepPartial<Preferences>;
}
export type {
  AppPreferences,
  BreadcrumbPreferences,
  FooterPreferences,
  HeaderPreferences,
  InitialOptions,
  LogoPreferences,
  NavigationPreferences,
  Preferences,
  PreferencesKeys,
  ShortcutKeyPreferences,
  SidebarPreferences,
  SupportedLanguagesType,
  TabbarPreferences,
  ThemePreferences,
  TransitionPreferences,
  WidgetPreferences,
};
