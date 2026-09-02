import type { Preferences } from '@vben/preferences';
import type { SelectOption } from '@vben/types';

import { SUPPORT_LANGUAGES } from '@vben/constants';
import { $t } from '@vben/locales';

/**
 * [환경설정 카탈로그] — 이 화면이 다루는 **모든** 설정 항목의 정본.
 *
 * ── 왜 목록으로 적는가 ──────────────────────────────────────
 *
 * `/setting/environment` 는 원래 헤더 톱니의 드로어와 **같은 부품**
 * (`PreferencesPanel`)을 렌더했다. 그 부품은 폭 350px 짜리 드로어를 위해 만든 것이라
 * 넓은 화면에서는 어색했다 — 컨트롤 폭이 165px 로 고정이고, 설명은 물음표에 숨고,
 * 항목 70여 개가 한 줄로 세로로 쌓인다.
 *
 * 그래서 이 화면은 자기 UI 를 갖는다. **드로어는 손대지 않았다** — 권한이 없는 역할도
 * 톱니로 자기 테마를 바꿀 길이 남아야 하고(23번 문서), 상위 동기화 비용도 커진다.
 *
 * 화면을 따로 만들면 **설정 항목이 갈라진다**는 위험이 생긴다. 그 위험을 코드로 막는다.
 *
 *   · 항목을 여기 한 곳에만 적는다 (화면은 이 목록을 그린다)
 *   · `coverage.test.ts` 가 `preferences-panel.vue` 의 `defineModel` 이름과 대조해
 *     **하나라도 빠지면 테스트가 깨진다.** 상위가 설정을 추가하면 거기서 걸린다.
 *
 * ── 항목 하나가 갖는 것 ─────────────────────────────────────
 *
 * `path` 는 스토어 경로(`app.locale`)이고 `model` 은 드로어 패널이 쓰는 이름
 * (`appLocale`)이다. 둘 다 적는 이유: 화면은 `path` 로 읽고 쓰지만, 누락 감시는
 * `model` 로만 대조할 수 있다(패널이 그 이름으로 선언한다).
 */

/** 화면이 그릴 수 있는 컨트롤 종류. */
export type EnvControl =
  | 'builtinTheme' // 기본 테마 색 + 사용자 정의
  | 'contentWidth' // 와이드 / 고정
  | 'layout' // 레이아웃 7종 (그림)
  | 'number'
  | 'segmented' // 값이 둘셋이라 펼쳐 두는 것이 빠른 것
  | 'select'
  | 'switch'
  | 'text'
  | 'themeMode' // 밝게 / 어둡게 / 시스템
  | 'transition' // 페이지 전환 효과 (움직이는 미리보기)
  | 'widgets'; // 헤더 위젯 순서 · 위치

/** 비활성·숨김 판정에 쓰는 지금 상태. 카탈로그는 스토어를 직접 보지 않는다. */
export interface EnvContext {
  isDark: boolean;
  isFullContent: boolean;
  isHeaderNav: boolean;
  isHeaderSidebarNav: boolean;
  isMixedNav: boolean;
  isSideMixedNav: boolean;
  isSideMode: boolean;
  isSideNav: boolean;
  layout: string;
  /** 현재 설정값 */
  p: Preferences;
  /** 시간대 목록 (비동기로 받아 온다) */
  timezones: SelectOption[];
}

export interface EnvField {
  /** 고를 수 있는 값. 함수면 지금 상태를 보고 만든다. */
  options?: ((ctx: EnvContext) => SelectOption[]) | SelectOption[];
  control: EnvControl;
  /** 라벨 아래 작은 글씨. **드로어는 이것을 물음표에 숨긴다** — 넓은 화면에서는 펼친다. */
  desc?: string;
  /** 끌 조건. 참이면 회색으로 두고 누르지 못하게 한다. */
  disabled?: (ctx: EnvContext) => boolean;
  /**
   * 이 항목이 함께 다루는 다른 스토어 경로.
   *
   * 컨트롤 하나가 값 여럿을 다룰 때 적는다(위젯 목록이 위치 열 개를 함께 다룬다).
   * 왼쪽 갈래 목록의 "기본값과 다름" 표시가 이 경로도 함께 센다.
   */
  extraPaths?: string[];
  /** 아예 감출 조건 (워터마크 문구처럼 상위 스위치가 꺼졌을 때). */
  hidden?: (ctx: EnvContext) => boolean;
  label: string;
  max?: number;
  min?: number;
  /**
   * 드로어 패널이 쓰는 모델 이름. 누락 감시가 이 값으로 대조한다.
   * 한 컨트롤이 여러 값을 다루면 `models` 에 나머지를 적는다.
   */
  model: string;
  models?: string[];
  /** 스토어 경로 `그룹.키`. */
  path: string;
  /** 검색에 걸리게 할 추가 낱말 (라벨·설명에 없는 말). */
  search?: string;
  /** 단축키 표시 (단축키 섹션). */
  shortcut?: string;
  step?: number;
  /** 참이면 한 줄을 통째로 쓴다 (그림·목록처럼 넓어야 하는 것). */
  wide?: boolean;
}

export interface EnvSection {
  desc?: string;
  fields: EnvField[];
  icon: string;
  /** 한 줄에 항목 둘을 두어도 되는가. 그림·목록이 있는 섹션은 한 줄에 하나다. */
  twoColumn?: boolean;
  key: string;
  title: string;
}

/** 사이드바가 없는 레이아웃이거나 사이드바를 끈 상태. */
function sidebarOff(ctx: EnvContext): boolean {
  return !ctx.isSideMode || !ctx.p.sidebar.enable;
}

export function buildSections(): EnvSection[] {
  return [
    // ── 일반 ────────────────────────────────────────────────
    {
      key: 'general',
      title: $t('preferences.general'),
      desc: '언어 · 시간대처럼 화면 전체에 걸리는 것.',
      icon: 'lucide:settings',
      twoColumn: true,
      fields: [
        {
          path: 'app.locale',
          model: 'appLocale',
          label: $t('preferences.language'),
          desc: '화면 글자의 언어. 메뉴 이름은 서버에서 오므로 함께 바뀐다.',
          control: 'select',
          options: SUPPORT_LANGUAGES,
        },
        {
          path: 'app.timezone',
          model: 'appTimezone',
          label: $t('preferences.timezone'),
          desc: '날짜·시각을 어느 시간대로 보일지. 저장된 값은 바뀌지 않는다.',
          control: 'select',
          options: (ctx) => ctx.timezones,
        },
        {
          path: 'app.dynamicTitle',
          model: 'appDynamicTitle',
          label: $t('preferences.dynamicTitle'),
          desc: '브라우저 탭 제목에 지금 보고 있는 화면 이름을 넣는다.',
          control: 'switch',
        },
        {
          path: 'app.watermark',
          model: 'appWatermark',
          label: $t('preferences.watermark'),
          desc: '화면 위에 옅은 글자를 반복해 깐다. 화면 사진이 새어 나갈 때 출처를 남긴다.',
          control: 'switch',
        },
        {
          path: 'app.watermarkContent',
          model: 'appWatermarkContent',
          label: '워터마크 문구',
          desc: '비워 두면 아무것도 찍히지 않는다.',
          control: 'text',
          hidden: (ctx) => !ctx.p.app.watermark,
        },
        {
          path: 'app.enableCheckUpdates',
          model: 'appEnableCheckUpdates',
          label: $t('preferences.checkUpdates'),
          desc: '새 버전이 배포되면 알려 준다. 이 포털은 기본으로 끈다.',
          control: 'switch',
        },
        {
          path: 'app.enableCopyPreferences',
          model: 'appEnableCopyPreferences',
          label: $t('preferences.enableCopyPreferences'),
          desc: '이 화면 위쪽에 [설정 복사] 단추를 둔다. 개발자가 기본값을 옮길 때 쓴다.',
          control: 'switch',
        },
        {
          path: 'app.enableStickyPreferencesNavigationBar',
          model: 'appEnableStickyPreferencesNavigationBar',
          label: $t('preferences.enableStickyPreferencesNavigationBar'),
          desc: '헤더 톱니로 여는 좁은 설정창에서 탭 머리를 위에 붙여 둔다. 이 화면과는 무관하다.',
          control: 'switch',
          search: '드로어 톱니',
        },
      ],
    },

    // ── 테마 ────────────────────────────────────────────────
    {
      key: 'theme',
      title: $t('preferences.theme.title'),
      desc: '색 · 글꼴 · 모서리.',
      icon: 'lucide:palette',
      fields: [
        {
          path: 'theme.mode',
          model: 'themeMode',
          label: '테마',
          desc: '시스템을 고르면 운영체제의 밝게·어둡게를 따라간다.',
          control: 'themeMode',
          wide: true,
        },
        {
          path: 'theme.builtinType',
          model: 'themeBuiltinType',
          models: ['themeColorPrimary'],
          extraPaths: ['theme.colorPrimary'],
          label: $t('preferences.theme.builtin.title'),
          desc: '단추 · 선택된 메뉴처럼 강조에 쓰이는 색. 맨 끝에서 직접 고를 수도 있다.',
          control: 'builtinTheme',
          wide: true,
        },
        {
          path: 'theme.radius',
          model: 'themeRadius',
          label: $t('preferences.theme.radius'),
          desc: '단추 · 카드 · 입력칸 모서리의 둥근 정도.',
          control: 'segmented',
          options: [
            { label: '0', value: '0' },
            { label: '0.25', value: '0.25' },
            { label: '0.5', value: '0.5' },
            { label: '0.75', value: '0.75' },
            { label: '1', value: '1' },
          ],
        },
        {
          path: 'theme.fontSize',
          model: 'themeFontSize',
          label: $t('preferences.theme.fontSize'),
          desc: '화면 전체 글자 크기(px). 표가 많은 화면은 작게 두면 한 번에 더 보인다.',
          control: 'number',
          min: 6,
          max: 32,
          step: 1,
        },
        {
          path: 'app.fontFamily',
          model: 'appFontFamily',
          label: $t('preferences.fontFamily'),
          desc: '저장소에 넣어 둔 글꼴만 쓴다(준수사항 5). 바깥에서 받아 오지 않는다.',
          control: 'select',
          options: [
            { label: 'S-CoreDream', value: 'S-CoreDream' },
            { label: '나눔스퀘어라운드', value: 'NanumSquareRound' },
            { label: 'Play', value: 'Play' },
            { label: $t('preferences.followSystem'), value: 'system' },
          ],
        },
        {
          path: 'theme.semiDarkSidebar',
          model: 'themeSemiDarkSidebar',
          label: $t('preferences.theme.darkSidebar'),
          desc: '밝은 테마에서 사이드바만 어둡게 둔다. 어두운 테마에서는 쓸 일이 없다.',
          control: 'switch',
          disabled: (ctx) =>
            ctx.isDark || ctx.isHeaderNav || ctx.isFullContent,
        },
        {
          path: 'theme.semiDarkSidebarSub',
          model: 'themeSemiDarkSidebarSub',
          label: $t('preferences.theme.darkSidebarSub'),
          desc: '2열 메뉴의 두 번째 열도 어둡게. 위 항목이 켜져 있어야 한다.',
          control: 'switch',
          disabled: (ctx) =>
            ctx.isDark ||
            (ctx.layout !== 'header-mixed-nav' &&
              ctx.layout !== 'sidebar-mixed-nav') ||
            !ctx.p.theme.semiDarkSidebar,
        },
        {
          path: 'theme.semiDarkHeader',
          model: 'themeSemiDarkHeader',
          label: $t('preferences.theme.darkHeader'),
          desc: '밝은 테마에서 헤더만 어둡게 둔다.',
          control: 'switch',
          disabled: (ctx) => ctx.isDark,
        },
        {
          path: 'app.colorWeakMode',
          model: 'appColorWeakMode',
          label: $t('preferences.theme.weakMode'),
          desc: '색으로만 구분되던 것을 색약에서도 가릴 수 있게 화면 색을 보정한다.',
          control: 'switch',
        },
        {
          path: 'app.colorGrayMode',
          model: 'appColorGrayMode',
          label: $t('preferences.theme.grayMode')
            ,
          desc: '화면 전체를 회색으로 만든다. 추모일 등에 쓴다.',
          control: 'switch',
        },
      ],
    },

    // ── 레이아웃 ────────────────────────────────────────────
    {
      key: 'layout',
      title: $t('preferences.layout'),
      desc: '메뉴를 어디에 두고 본문을 얼마나 넓게 쓸지.',
      icon: 'lucide:layout-dashboard',
      fields: [
        {
          path: 'app.layout',
          model: 'appLayout',
          label: '메뉴 배치',
          desc: '고르면 곧바로 바뀐다. 아래 사이드바·헤더 설정 중 무엇이 켜지는지도 여기에 따라 달라진다.',
          control: 'layout',
          wide: true,
        },
        {
          path: 'app.contentCompact',
          model: 'appContentCompact',
          label: $t('preferences.content'),
          desc: '와이드는 창을 꽉 채우고, 고정은 가운데 1200px 로 묶는다.',
          control: 'contentWidth',
          wide: true,
        },
      ],
    },

    // ── 사이드바 ────────────────────────────────────────────
    {
      key: 'sidebar',
      title: $t('preferences.sidebar.title'),
      desc: '왼쪽 메뉴 영역. 메뉴 배치가 세로형 계열일 때만 쓰인다.',
      icon: 'lucide:panel-left',
      twoColumn: true,
      fields: [
        {
          path: 'sidebar.enable',
          model: 'sidebarEnable',
          label: $t('preferences.sidebar.visible'),
          desc: '끄면 왼쪽 메뉴가 사라진다. 헤더 왼쪽 햄버거로 다시 켤 수 있다.',
          control: 'switch',
          disabled: (ctx) => !ctx.isSideMode,
        },
        {
          path: 'sidebar.width',
          model: 'sidebarWidth',
          label: $t('preferences.sidebar.width'),
          desc: '펼친 상태의 폭(px). 메뉴 이름이 길면 넓게 둔다.',
          control: 'number',
          min: 160,
          max: 320,
          step: 10,
          disabled: sidebarOff,
        },
        {
          path: 'sidebar.collapsed',
          model: 'sidebarCollapsed',
          label: $t('preferences.sidebar.collapsed'),
          desc: '아이콘만 남기고 접는다. 본문을 넓게 쓸 때.',
          control: 'switch',
          disabled: sidebarOff,
        },
        {
          path: 'sidebar.onMenuSelect',
          model: 'sidebarOnMenuSelect',
          label: $t('preferences.sidebar.onMenuSelect'),
          desc: '메뉴를 고른 뒤 사이드바를 어떻게 둘지. 숨기기는 헤더 햄버거로 되돌린다.',
          control: 'select',
          options: [
            { label: $t('preferences.sidebar.onMenuSelectNone'), value: 'none' },
            {
              label: $t('preferences.sidebar.onMenuSelectCollapse'),
              value: 'collapse',
            },
            { label: $t('preferences.sidebar.onMenuSelectHide'), value: 'hide' },
          ],
          disabled: sidebarOff,
        },
        {
          path: 'sidebar.expandOnHover',
          model: 'sidebarExpandOnHover',
          label: $t('preferences.sidebar.expandOnHover'),
          desc: '접힌 사이드바에 마우스를 올리면 펼친다. 접혀 있을 때만 뜻이 있다.',
          control: 'switch',
          disabled: (ctx) => sidebarOff(ctx) || !ctx.p.sidebar.collapsed,
        },
        {
          path: 'sidebar.collapsedShowTitle',
          model: 'sidebarCollapsedShowTitle',
          label: $t('preferences.sidebar.collapsedShowTitle'),
          desc: '접힌 상태에서도 아이콘 아래 이름을 적는다.',
          control: 'switch',
          disabled: (ctx) => sidebarOff(ctx) || !ctx.p.sidebar.collapsed,
        },
        {
          path: 'sidebar.scrollToActive',
          model: 'sidebarScrollToActive',
          label: $t('preferences.sidebar.scrollToActive'),
          desc: '메뉴를 고르면 그 항목이 세로 가운데로 오도록 굴린다. 메뉴가 많을 때 보인다.',
          control: 'switch',
          disabled: (ctx) => sidebarOff(ctx) || ctx.p.sidebar.collapsed,
        },
        {
          path: 'sidebar.autoActivateChild',
          model: 'sidebarAutoActivateChild',
          label: $t('preferences.sidebar.autoActivateChild'),
          desc: '묶음을 누르면 그 안 첫 화면까지 바로 연다. 2열·혼합 배치에서만 쓰인다.',
          control: 'switch',
          disabled: (ctx) =>
            sidebarOff(ctx) ||
            !['header-mixed-nav', 'mixed-nav', 'sidebar-mixed-nav'].includes(
              ctx.layout,
            ),
        },
        {
          path: 'sidebar.draggable',
          model: 'sidebarDraggable',
          label: $t('preferences.sidebar.draggable'),
          desc: '사이드바 오른쪽 가장자리를 끌어 폭을 바꿀 수 있게 한다.',
          control: 'switch',
          disabled: sidebarOff,
        },
        {
          path: 'sidebar.collapsedButton',
          model: 'sidebarCollapsedButton',
          label: `${$t('preferences.sidebar.buttons')} — ${$t('preferences.sidebar.buttonCollapsed')}`,
          desc: '사이드바 아래에 접기 단추를 둔다.',
          control: 'switch',
          disabled: sidebarOff,
          search: '축소 버튼',
        },
        {
          path: 'sidebar.fixedButton',
          model: 'sidebarFixedButton',
          label: `${$t('preferences.sidebar.buttons')} — ${$t('preferences.sidebar.buttonFixed')}`,
          desc: '사이드바 아래에 고정(핀) 단추를 둔다.',
          control: 'switch',
          disabled: sidebarOff,
          search: '고정 버튼',
        },
      ],
    },

    // ── 헤더 ────────────────────────────────────────────────
    {
      key: 'header',
      title: $t('preferences.header.title'),
      desc: '화면 맨 위 줄.',
      icon: 'lucide:panel-top',
      twoColumn: true,
      fields: [
        {
          path: 'header.enable',
          model: 'headerEnable',
          label: $t('preferences.header.visible'),
          desc: '끄면 맨 윗줄이 사라진다. 위젯·브레드크럼도 함께 사라진다.',
          control: 'switch',
          disabled: (ctx) => ctx.isFullContent,
        },
        {
          path: 'header.mode',
          model: 'headerMode',
          label: $t('preferences.mode'),
          desc: '고정은 늘 붙어 있고, 자동 숨김은 굴릴 때 비켜 준다.',
          control: 'select',
          options: [
            { label: $t('preferences.header.modeStatic'), value: 'static' },
            { label: $t('preferences.header.modeFixed'), value: 'fixed' },
            { label: $t('preferences.header.modeAuto'), value: 'auto' },
            {
              label: $t('preferences.header.modeAutoScroll'),
              value: 'auto-scroll',
            },
          ],
          disabled: (ctx) => !ctx.p.header.enable,
        },
        {
          path: 'header.menuAlign',
          model: 'headerMenuAlign',
          label: $t('preferences.header.menuAlign'),
          desc: '헤더에 메뉴가 있는 배치(가로형 · 혼합)에서 그 메뉴를 어디에 붙일지.',
          control: 'segmented',
          options: [
            { label: $t('preferences.header.menuAlignStart'), value: 'start' },
            { label: $t('preferences.header.menuAlignCenter'), value: 'center' },
            { label: $t('preferences.header.menuAlignEnd'), value: 'end' },
          ],
          disabled: (ctx) => !ctx.p.header.enable,
        },
      ],
    },

    // ── 탐색 메뉴 ───────────────────────────────────────────
    {
      key: 'navigation',
      title: $t('preferences.navigationMenu.title'),
      desc: '메뉴 자체의 모양과 펼침 규칙.',
      icon: 'lucide:list-tree',
      twoColumn: true,
      fields: [
        {
          path: 'navigation.styleType',
          model: 'navigationStyleType',
          label: $t('preferences.navigationMenu.style'),
          desc: '라운드는 선택된 항목을 둥근 판으로, 심플은 줄로 표시한다.',
          control: 'segmented',
          options: [
            { label: $t('preferences.rounded'), value: 'rounded' },
            { label: $t('preferences.plain'), value: 'plain' },
          ],
          disabled: (ctx) => ctx.isFullContent,
        },
        {
          path: 'navigation.accordion',
          model: 'navigationAccordion',
          label: $t('preferences.navigationMenu.accordion'),
          desc: '한 묶음을 펼치면 다른 묶음은 접힌다. 끄면 여러 묶음을 함께 펼쳐 둔다.',
          control: 'switch',
          disabled: (ctx) => ctx.isFullContent,
        },
        {
          path: 'navigation.split',
          model: 'navigationSplit',
          label: $t('preferences.navigationMenu.split'),
          desc: $t('preferences.navigationMenu.splitTip'),
          control: 'switch',
          disabled: (ctx) => ctx.isFullContent || !ctx.isMixedNav,
        },
      ],
    },

    // ── 브레드크럼 ──────────────────────────────────────────
    {
      key: 'breadcrumb',
      title: $t('preferences.breadcrumb.title'),
      desc: '헤더에서 "어디에 있는지" 를 보여 주는 줄. 세로형 계열 배치에서 쓰인다.',
      icon: 'lucide:chevrons-right',
      twoColumn: true,
      fields: [
        {
          path: 'breadcrumb.enable',
          model: 'breadcrumbEnable',
          label: $t('preferences.breadcrumb.enable'),
          desc: '헤더 왼쪽에 현재 위치를 적는다.',
          control: 'switch',
          disabled: (ctx) =>
            ctx.isFullContent ||
            !ctx.p.header.enable ||
            !(ctx.isSideNav || ctx.isSideMixedNav || ctx.isHeaderSidebarNav),
        },
        {
          path: 'breadcrumb.hideOnlyOne',
          model: 'breadcrumbHideOnlyOne',
          label: $t('preferences.breadcrumb.hideOnlyOne'),
          desc: '단계가 하나뿐이면(대시보드 등) 굳이 적지 않는다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.breadcrumb.enable,
        },
        {
          path: 'breadcrumb.showIcon',
          model: 'breadcrumbShowIcon',
          label: $t('preferences.breadcrumb.icon'),
          desc: '각 단계 앞에 메뉴 아이콘을 붙인다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.breadcrumb.enable,
        },
        {
          path: 'breadcrumb.showHome',
          model: 'breadcrumbShowHome',
          label: $t('preferences.breadcrumb.home')
            ,
          desc: '맨 앞에 집 아이콘을 두어 첫 화면으로 갈 수 있게 한다. 아이콘 표시가 켜져 있어야 한다.',
          control: 'switch',
          disabled: (ctx) =>
            !ctx.p.breadcrumb.enable || !ctx.p.breadcrumb.showIcon,
        },
        {
          path: 'breadcrumb.styleType',
          model: 'breadcrumbStyleType',
          label: $t('preferences.breadcrumb.style'),
          desc: '배경을 고르면 옅은 판 위에 얹는다.',
          control: 'segmented',
          options: [
            { label: $t('preferences.normal'), value: 'normal' },
            {
              label: $t('preferences.breadcrumb.background'),
              value: 'background',
            },
          ],
          disabled: (ctx) => !ctx.p.breadcrumb.enable,
        },
      ],
    },

    // ── 탭 바 ───────────────────────────────────────────────
    {
      key: 'tabbar',
      title: $t('preferences.tabbar.title'),
      desc: '열어 둔 화면을 줄지어 두는 곳.',
      icon: 'lucide:app-window',
      twoColumn: true,
      fields: [
        {
          path: 'tabbar.enable',
          model: 'tabbarEnable',
          label: $t('preferences.tabbar.enable'),
          desc: '끄면 화면을 하나씩만 쓴다. 여러 화면을 오가는 일이 많으면 켜 둔다.',
          control: 'switch',
        },
        {
          path: 'tabbar.styleType',
          model: 'tabbarStyleType',
          label: $t('preferences.tabbar.styleType.title'),
          desc: '탭 모양.',
          control: 'select',
          options: [
            { label: $t('preferences.tabbar.styleType.chrome'), value: 'chrome' },
            { label: $t('preferences.tabbar.styleType.plain'), value: 'plain' },
            { label: $t('preferences.tabbar.styleType.card'), value: 'card' },
            { label: $t('preferences.tabbar.styleType.brisk'), value: 'brisk' },
          ],
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.persist',
          model: 'tabbarPersist',
          label: $t('preferences.tabbar.persist'),
          desc: '브라우저를 닫았다 열어도 열려 있던 탭을 되살린다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.maxCount',
          model: 'tabbarMaxCount',
          label: $t('preferences.tabbar.maxCount'),
          desc: $t('preferences.tabbar.maxCountTip'),
          control: 'number',
          min: 0,
          max: 30,
          step: 5,
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.visitHistory',
          model: 'tabbarVisitHistory',
          label: $t('preferences.tabbar.visitHistory'),
          desc: $t('preferences.tabbar.visitHistoryTip'),
          control: 'switch',
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.draggable',
          model: 'tabbarDraggable',
          label: $t('preferences.tabbar.draggable'),
          desc: '탭을 끌어 순서를 바꿀 수 있게 한다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.wheelable',
          model: 'tabbarWheelable',
          label: $t('preferences.tabbar.wheelable'),
          desc: $t('preferences.tabbar.wheelableTip'),
          control: 'switch',
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.middleClickToClose',
          model: 'tabbarMiddleClickToClose',
          label: $t('preferences.tabbar.middleClickClose'),
          desc: '가운데 단추(휠)로 눌러 탭을 닫는다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.showIcon',
          model: 'tabbarShowIcon',
          label: $t('preferences.tabbar.icon'),
          desc: '탭 이름 앞에 메뉴 아이콘을 붙인다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.showMore',
          model: 'tabbarShowMore',
          label: $t('preferences.tabbar.showMore'),
          desc: '탭 오른쪽에 더 보기(⋯) 단추를 둔다. 오른쪽 클릭으로도 같은 메뉴가 나온다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
        {
          path: 'tabbar.showMaximize',
          model: 'tabbarShowMaximize',
          label: $t('preferences.tabbar.showMaximize'),
          desc: '본문만 남기고 넓게 보는 단추를 둔다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.tabbar.enable,
        },
      ],
    },

    // ── 헤더 위젯 ───────────────────────────────────────────
    {
      key: 'widget',
      title: $t('preferences.widget.title'),
      desc: '헤더 오른쪽 아이콘들. 순서를 바꾸고, 사용자 메뉴로 내리거나 감출 수 있다.',
      icon: 'lucide:mouse-pointer-click',
      fields: [
        {
          path: 'widget.order',
          model: 'widgetOrder',
          models: [
            'widgetGlobalSearchButtonPosition',
            'widgetFullscreenButtonPosition',
            'widgetLanguageToggleButtonPosition',
            'widgetNotificationButtonPosition',
            'widgetThemeToggleButtonPosition',
            'widgetLockScreenButtonPosition',
            'widgetLogoutButtonPosition',
            'widgetRefreshButtonPosition',
            'widgetTimezoneButtonPosition',
            'appPreferencesButtonPosition',
          ],
          extraPaths: [
            'widget.globalSearchButtonPosition',
            'widget.fullscreenButtonPosition',
            'widget.languageToggleButtonPosition',
            'widget.notificationButtonPosition',
            'widget.themeToggleButtonPosition',
            'widget.lockScreenButtonPosition',
            'widget.logoutButtonPosition',
            'widget.refreshButtonPosition',
            'widget.timezoneButtonPosition',
            'app.preferencesButtonPosition',
          ],
          label: '위젯 순서와 위치',
          desc: '위아래 화살표로 순서를 바꾼다. 위치를 사용자 메뉴로 내리면 헤더가 한결 가벼워진다.',
          control: 'widgets',
          wide: true,
          search:
            '검색 전체화면 언어 알림 테마 잠금 로그아웃 새로고침 시간대 환경설정',
        },
      ],
    },

    // ── 푸터 ────────────────────────────────────────────────
    {
      key: 'footer',
      title: $t('preferences.footer.title'),
      desc: '화면 맨 아래 줄.',
      icon: 'lucide:panel-bottom',
      twoColumn: true,
      fields: [
        {
          path: 'footer.enable',
          model: 'footerEnable',
          label: $t('preferences.footer.visible'),
          desc: '맨 아래 줄을 둔다. 이 포털은 기본으로 끈다.',
          control: 'switch',
        },
        {
          path: 'footer.fixed',
          model: 'footerFixed',
          label: $t('preferences.footer.fixed'),
          desc: '굴려도 아래에 붙어 있게 한다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.footer.enable,
        },
      ],
    },

    // ── 애니메이션 ──────────────────────────────────────────
    {
      key: 'animation',
      title: $t('preferences.animation.title'),
      desc: '화면이 바뀔 때의 움직임. 느린 장비에서는 꺼 두는 편이 빠르게 느껴진다.',
      icon: 'lucide:sparkles',
      fields: [
        {
          path: 'transition.progress',
          model: 'transitionProgress',
          label: $t('preferences.animation.progress'),
          desc: '화면을 옮길 때 위쪽에 진행 줄을 그린다.',
          control: 'switch',
        },
        {
          path: 'transition.loading',
          model: 'transitionLoading',
          label: $t('preferences.animation.loading'),
          desc: '화면을 받아 오는 동안 가리개를 띄운다.',
          control: 'switch',
        },
        {
          path: 'transition.enable',
          model: 'transitionEnable',
          label: $t('preferences.animation.transition'),
          desc: '화면이 바뀔 때 부드럽게 넘긴다.',
          control: 'switch',
        },
        {
          path: 'transition.name',
          model: 'transitionName',
          label: '전환 효과',
          desc: '아래에서 고른다. 위 항목이 켜져 있어야 쓰인다.',
          control: 'transition',
          wide: true,
          disabled: (ctx) => !ctx.p.transition.enable,
        },
      ],
    },

    // ── 단축키 ──────────────────────────────────────────────
    {
      key: 'shortcut',
      title: $t('preferences.shortcutKeys.title'),
      desc: '전역 단축키. 위 항목을 끄면 아래 전부가 멈춘다.',
      icon: 'lucide:keyboard',
      twoColumn: true,
      fields: [
        {
          path: 'shortcutKeys.enable',
          model: 'shortcutKeysEnable',
          label: `${$t('preferences.shortcutKeys.title')} 사용`,
          desc: '끄면 아래 단축키가 모두 멈춘다.',
          control: 'switch',
        },
        {
          path: 'shortcutKeys.globalSearch',
          model: 'shortcutKeysGlobalSearch',
          label: $t('preferences.shortcutKeys.search'),
          desc: '메뉴를 이름으로 찾아 바로 연다.',
          control: 'switch',
          shortcut: 'mod K',
          disabled: (ctx) => !ctx.p.shortcutKeys.enable,
        },
        {
          path: 'shortcutKeys.globalLogout',
          model: 'shortcutKeysGlobalLogout',
          label: $t('preferences.shortcutKeys.logout'),
          desc: '누르면 로그아웃한다.',
          control: 'switch',
          shortcut: 'alt Q',
          disabled: (ctx) => !ctx.p.shortcutKeys.enable,
        },
        {
          path: 'shortcutKeys.globalPreferences',
          model: 'shortcutKeysGlobalPreferences',
          label: $t('preferences.shortcutKeys.preferences'),
          // **아직 아무 일도 하지 않는다.** 값은 저장되지만 `Alt ,` 를 받는 곳이
          // 이 저장소에 없다 — 프레임워크가 `globalPreferencesShortcutKey` 를
          // 계산해 두었을 뿐 쓰는 데가 없다(형제 단축키는 header.vue 가 잡는다).
          // 그래서 단축키 표시(`shortcut`)도 달지 않는다 — 달면 되는 줄 안다.
          desc: '헤더 톱니의 좁은 설정창을 여는 단축키. 아직 동작하지 않는다 — 값만 저장된다.',
          control: 'switch',
          disabled: (ctx) => !ctx.p.shortcutKeys.enable,
        },
        {
          path: 'shortcutKeys.globalLockScreen',
          model: 'shortcutKeysGlobalLockScreen',
          label: $t('ui.widgets.lockScreen.title'),
          desc: '자리를 비울 때 화면을 잠근다.',
          control: 'switch',
          shortcut: 'alt L',
          disabled: (ctx) => !ctx.p.shortcutKeys.enable,
        },
        {
          path: 'shortcutKeys.globalEscape',
          model: 'shortcutKeysGlobalEscape',
          label: $t('preferences.shortcutKeys.escape'),
          desc: '열려 있는 팝업·서랍을 닫는다.',
          control: 'switch',
          shortcut: 'Esc',
          disabled: (ctx) => !ctx.p.shortcutKeys.enable,
        },
      ],
    },
  ];
}
