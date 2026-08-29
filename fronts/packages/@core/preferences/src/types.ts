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

type SupportedLanguagesType = 'en' | 'ko';
type CustomPreferencesValue = boolean | number | string;

interface CustomPreferencesOption<TValue extends string = string> {
  label: string;
  value: TValue;
}

interface BaseCustomPreferencesField<
  TKey extends string = string,
  TValue extends CustomPreferencesValue = CustomPreferencesValue,
> {
  componentProps?: Record<string, any>;
  defaultValue: TValue;
  disabled?: boolean;
  key: TKey;
  label: string;
  placeholder?: string;
  tip?: string;
}

interface CustomPreferencesInputField<
  TKey extends string = string,
> extends BaseCustomPreferencesField<TKey, string> {
  component: 'input';
}

interface CustomPreferencesNumberField<
  TKey extends string = string,
> extends BaseCustomPreferencesField<TKey, number> {
  component: 'number';
}

interface CustomPreferencesSelectField<
  TKey extends string = string,
> extends BaseCustomPreferencesField<TKey, string> {
  component: 'select';
  options: CustomPreferencesOption[];
}

interface CustomPreferencesSwitchField<
  TKey extends string = string,
> extends BaseCustomPreferencesField<TKey, boolean> {
  component: 'switch';
}

type CustomPreferencesRecord = Record<string, CustomPreferencesValue>;

type AnyCustomPreferencesField =
  | CustomPreferencesInputField
  | CustomPreferencesNumberField
  | CustomPreferencesSelectField
  | CustomPreferencesSwitchField;

type CustomPreferencesField<
  TCustomPreferences extends object = CustomPreferencesRecord,
> =
  string extends Extract<keyof TCustomPreferences, string>
    ? AnyCustomPreferencesField
    : {
        [K in Extract<
          keyof TCustomPreferences,
          string
        >]: TCustomPreferences[K] extends boolean
          ? CustomPreferencesSwitchField<K>
          : TCustomPreferences[K] extends number
            ? CustomPreferencesNumberField<K>
            : TCustomPreferences[K] extends string
              ? CustomPreferencesInputField<K> | CustomPreferencesSelectField<K>
              : never;
      }[Extract<keyof TCustomPreferences, string>];

interface PreferencesExtension<
  TCustomPreferences extends object = CustomPreferencesRecord,
> {
  fields: Array<CustomPreferencesField<TCustomPreferences>>;
  tabLabel: string;
  title?: string;
}

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
  /** 应用默认头像 */
  defaultAvatar: string;
  /** 기본 홈페이지 주소 */
  defaultHomePath: string;
  /** 开启动态标题 */
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
  /**
   * 기본 글꼴.
   *
   * 값은 글꼴 이름 하나가 아니라 '어떤 글꼴 묶음을 쓸지' 를 고르는 열쇠다.
   * 실제 글꼴 목록은 앱이 정한다(각 앱의 `src/styles/font.ts`) —
   * 프레임워크가 특정 글꼴을 알고 있을 이유가 없기 때문이다.
   */
  fontFamily: string;
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
   * @zh_CN 应用时区
   */
  timezone: string;
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
  /** logo高度， 只在 logoMode=full时生效 */
  fullLogoHeight?: number | string;
  /** logo 展示类型，icon 图标模式， full 铺满logo区域 */
  logoMode: 'full' | 'icon';
  /** logo text是否展示 */
  showText: boolean;
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

/** 메뉴를 고른 뒤 사이드바를 어떻게 할지. */
type SidebarMenuSelectBehavior = 'collapse' | 'hide' | 'none';

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
  /**
   * 왼쪽 메뉴를 고른 뒤 사이드바를 어떻게 할지.
   *
   * `none` 그대로 둔다(기본) / `collapse` 축소한다 /
   * `hide` 완전히 숨긴다(헤더 햄버거와 같은 상태)
   */
  onMenuSelect: SidebarMenuSelectBehavior;
  /** 사이드바 너비 */
  width: number;
}

interface ShortcutKeyPreferences {
  /** 단축키 활성화 여부 (전역) */
  enable: boolean;
  /** 是否启用全局关闭窗口快捷键 */
  globalEscape: boolean;
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
  /** 全屏按钮位置 */
  fullscreenButtonPosition: 'header' | 'none' | 'user-dropdown';
  /** 전역 검색 위젯 활성화 여부 */
  globalSearch: boolean;
  /** 全局搜索按钮位置 */
  globalSearchButtonPosition: 'header' | 'none' | 'user-dropdown';
  /** 언어 전환 위젯 활성화 여부 */
  languageToggle: boolean;
  /** 语言切换按钮位置 */
  languageToggleButtonPosition: 'header' | 'none' | 'user-dropdown';
  /** 잠금 화면 기능 활성화 여부 */
  lockScreen: boolean;
  /** 锁屏按钮位置 */
  lockScreenButtonPosition: 'header' | 'none' | 'user-dropdown';
  /** 退出登录按钮位置 */
  logoutButtonPosition: 'header' | 'none' | 'user-dropdown';
  /** 알림 위젯 표시 여부 */
  notification: boolean;
  /** 通知按钮位置 */
  notificationButtonPosition: 'header' | 'none' | 'user-dropdown';
  /** 小部件排序 */
  order: readonly string[];
  /** 새로고침 버튼 표시 여부 */
  refresh: boolean;
  /** 刷新按钮位置 */
  refreshButtonPosition: 'header' | 'none' | 'user-dropdown';
  /** 사이드바 토글 위젯 표시 여부 */
  sidebarToggle: boolean;
  /** 테마 전환 위젯 표시 여부 */
  themeToggle: boolean;
  /** 主题切换按钮位置 */
  themeToggleButtonPosition: 'header' | 'none' | 'user-dropdown';
  /** 시간대 위젯 표시 여부 */
  timezone: boolean;
  /** 时区按钮位置 */
  timezoneButtonPosition: 'header' | 'none' | 'user-dropdown';
}

interface Preferences {
  /** 전역 설정 */
  app: AppPreferences;
  /** 헤더 설정 */
  breadcrumb: BreadcrumbPreferences;
  /** 저작권 설정 */
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

interface InitialOptions<
  TCustomPreferences extends object = CustomPreferencesRecord,
> {
  extension?: PreferencesExtension<TCustomPreferences>;
  namespace: string;
  overrides?: DeepPartial<Preferences>;
}
export type {
  AnyCustomPreferencesField,
  AppPreferences,
  BaseCustomPreferencesField,
  BreadcrumbPreferences,
  CustomPreferencesField,
  CustomPreferencesInputField,
  CustomPreferencesNumberField,
  CustomPreferencesOption,
  CustomPreferencesRecord,
  CustomPreferencesSelectField,
  CustomPreferencesSwitchField,
  CustomPreferencesValue,
  FooterPreferences,
  HeaderPreferences,
  InitialOptions,
  LogoPreferences,
  NavigationPreferences,
  Preferences,
  PreferencesExtension,
  PreferencesKeys,
  ShortcutKeyPreferences,
  SidebarMenuSelectBehavior,
  SidebarPreferences,
  SupportedLanguagesType,
  TabbarPreferences,
  ThemePreferences,
  TransitionPreferences,
  WidgetPreferences,
};
