<script lang="ts" setup>
import type { SetupContext } from 'vue';

import type { RouteLocationNormalizedLoaded } from 'vue-router';

import type { MenuRecordRaw } from '@vben/types';

import { computed, inject, onMounted, useSlots, watch, ref } from 'vue';
import { useRoute } from 'vue-router';

import { useRefresh } from '@vben/hooks';
import { $t, $tIfKey, i18n } from '@vben/locales';
import {
  preferences,
  updatePreferences,
  usePreferences,
} from '@vben/preferences';
import { useAccessStore, useTabbarStore, useTimezoneStore } from '@vben/stores';
import { cloneDeep, mapTree } from '@vben/utils';

import { VbenAdminLayout } from '@vben-core/layout-ui';
// `Input` 은 아래 메뉴 검색칸이 쓴다.
//
// 예전에는 이 import 가 빠져 있었다. 그러면 `<Input>` 이 컴포넌트로 해석되지 못하고
// **native `<input>` 로 떨어진다.** 글자는 쳐지는데 `v-model` 이 붙을 곳이 없어
// `modelValue` 가 평범한 HTML 속성으로 박히고, `update:modelValue` 는 아무 데도 닿지 않는다.
// 그래서 **메뉴 검색이 화면상으로는 입력되는데 필터가 전혀 걸리지 않았다.**
// 콘솔에는 `Failed to resolve component: Input` 이 계속 찍히고 있었다.
import { Input, VbenBackTop, VbenLogo } from '@vben-core/shadcn-ui';
import { ELEMENT_ID_LAYOUT_SCROLL } from '@vben-core/shared/constants';

import { Breadcrumb, CheckUpdates, Preferences } from '../widgets';
// AI 채팅 사이드바의 여닫힘. 본문 옆에 자리를 잡으므로 자리와 폭은 레이아웃이 잡고,
// **내용은 앱이 `#ai-chat` 슬롯으로 넣어 준다** — 그 화면은 antd 를 쓰는데
// 프레임워크 패키지는 antd 를 쓰지 않는다.
import { isAiChatPinned } from '../widgets/ai-chat/state';
import { LayoutContent, LayoutContentSpinner } from './content';
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
import { useLayoutScroll } from './use-layout-scroll';

defineOptions({ name: 'BasicLayout' });

withDefaults(defineProps<Props>(), {
  avatar: '',
  text: '',
});

const emit = defineEmits<{
  clearPreferencesAndLogout: [];
  clickLogo: [];
  logout: [];
}>();

interface Props {
  avatar?: string;
  text?: string;
}

const {
  isDark,
  isHeaderNav,
  isMixedNav,
  isMobile,
  isSideMode,
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
const layoutScrollTarget = `#${ELEMENT_ID_LAYOUT_SCROLL}`;

useLayoutScroll();

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

const logoTheme = computed(() => {
  const showLogoInHeader =
    !isSideMode.value ||
    isHeaderSidebarNav.value ||
    isMixedNav.value ||
    isMobile.value;
  return showLogoInHeader ? headerTheme.value : sidebarTheme.value;
});

/**
 * layout-sidebar扩展区域插槽extra-title的高度
 */
const sidebarExtraTitleHeight = computed<number | undefined>(() => {
  const showSideExtraTitle =
    preferences.logo.enable && preferences.logo.showText;
  return showSideExtraTitle ? undefined : 0;
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
 * 왼쪽 메뉴를 고른 뒤 사이드바를 어떻게 할지 (`sidebar.onMenuSelect`, 기본 `none`).
 *
 * 좁은 화면에서 본문을 넓게 쓰려는 설정이다. **새 상태를 만들지 않는다** —
 * 축소도 숨기기도 이미 있는 값(`collapsed` · `hidden`)을 그대로 쓰므로,
 * 되돌리는 방법도 원래 쓰던 것 그대로다(접기 버튼 · 헤더 왼쪽 햄버거).
 *
 * [세로 메뉴에서만 접는다]
 * `mode` 가 `vertical` 인 것이 왼쪽 사이드바다. 상단 가로 메뉴(`horizontal`)에서
 * 고른 것까지 접으면, 사이드바를 쓰지도 않은 사람이 사이드바가 사라지는 것을 본다.
 *
 * [디렉터리를 펼치는 클릭은 접지 않는다]
 * `@select` 는 **실제로 이동하는 항목**에서만 온다(디렉터리를 여닫는 것은
 * `@open` 이다). 그래서 여기서 따로 걸러 낼 것이 없다.
 */
function handleSidebarMenuSelect(key: string, mode?: string) {
  // 어느 쪽을 눌렀는지 기억한다(즐겨찾기 항목과 트리 항목의 구분).
  pickedSidebarKey.value = key;

  // 얹은 묶음은 신원과 이동할 곳이 다르다. 접두사 붙은 신원으로는 이동할 수 없다.
  handleMenuSelect(extraMenuLinks.value.get(key) ?? key, mode);

  if (!isSideMode.value) return;

  switch (preferences.sidebar.onMenuSelect) {
    // 축소 — 아이콘만 남는다. 접기 버튼이나 '마우스 올리면 펼치기' 로 되돌린다.
    case 'collapse': {
      if (!preferences.sidebar.collapsed) {
        updatePreferences({ sidebar: { collapsed: true } });
      }
      break;
    }
    // 완전히 숨기기 — 헤더 왼쪽 햄버거가 만드는 상태와 **같은 값**을 쓴다.
    // 그래서 다시 보이게 하는 방법도 그 버튼으로 같다(`toggleSidebar`).
    case 'hide': {
      if (!preferences.sidebar.hidden) {
        updatePreferences({ sidebar: { hidden: true } });
      }
      break;
    }
    // 'none' — 그대로 둔다(기본).
    default: {
      break;
    }
  }
}

/**
 * 메뉴 래핑 및 메뉴 이름 번역
 * @param menus 원본 메뉴 데이터
 * @param deep 깊은 래핑 여부. 2열 레이아웃의 경우 확장 메뉴에서 더 깊은 데이터가 다시 래핑되므로 첫 번째 레이어만 래핑하면 됩니다.
 */
function wrapperMenus(menus: MenuRecordRaw[], deep: boolean = true) {
  return deep
    ? mapTree(menus, (item) => {
        return { ...cloneDeep(item), name: $tIfKey(item.name) };
      })
    : menus.map((item) => {
        return { ...cloneDeep(item), name: $tIfKey(item.name) };
      });
}

/* ---------------------------------------------------------------------------
 * 사이드바 메뉴 검색
 * 입력이 느릴(끊길) 수 있음을 고려해 키워드를 debounce(300ms) 처리하여
 * 필터 재계산이 매 키 입력마다 발생하지 않도록 한다.
 * ------------------------------------------------------------------------- */
/* ---------------------------------------------------------------------------
 * 메뉴 다시 읽기
 *
 * 메뉴는 백엔드가 내려준다(scom.system_menus → /auth/menu/all). 메뉴 관리에서
 * 메뉴를 고쳐도 이미 떠 있는 화면의 좌측 메뉴는 그대로라, 다시 읽을 방법이 필요하다.
 *
 * 갱신은 **앱이 주입해 준 핸들러**가 맡는다. 이 레이아웃은 프레임워크 패키지라
 * 앱의 라우트 표나 스토어를 직접 알지 못하기 때문이다.
 * 앱 쪽 구현은 `src/router/access.ts` 의 `refreshAccessMenus()` 이고,
 * `src/layouts/basic.vue` 에서 이 키로 주입한다.
 *
 * 핸들러가 없으면(그 배선을 하지 않은 앱) 예전 동작인 전체 새로고침으로 물러난다.
 * 결과는 같지만 앱이 처음부터 다시 뜨므로 열린 탭·스크롤·입력 중이던 값이 날아간다.
 * ------------------------------------------------------------------------- */
const menuReloading = ref(false);

/** 앱이 주입한 메뉴 갱신 함수. 없으면 전체 새로고침으로 물러난다. */
const menuReloadHandler = inject<(() => Promise<void>) | null>(
  'MENU_RELOAD_HANDLER',
  null,
);

async function reloadMenus() {
  if (menuReloading.value) return;
  menuReloading.value = true;

  if (!menuReloadHandler) {
    // 접근 상태(isAccessChecked)는 저장되지 않으므로 새로 열면 초기화된다.
    // 그래서 라우터 가드가 메뉴와 라우트를 처음부터 다시 구성한다.
    // 아이콘이 한 번 도는 것을 보이게 한 뒤 새로 연다.
    setTimeout(() => window.location.reload(), 150);
    return;
  }

  try {
    await menuReloadHandler();
  } finally {
    // 아이콘이 한 번은 돌아 눌린 것이 보이게 최소 시간을 준다.
    setTimeout(() => {
      menuReloading.value = false;
    }, 300);
  }
}

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

/**
 * 사이드바 맨 위에 얹을 추가 묶음. 지금은 즐겨찾기가 쓴다.
 *
 * 이 레이아웃은 프레임워크 패키지라 즐겨찾기 API 를 알지 못한다.
 * 메뉴 다시 읽기(`MENU_RELOAD_HANDLER`)와 같은 방식으로 앱이 주입한다.
 * 주입하지 않은 앱에서는 아무것도 얹히지 않는다.
 */
const sidebarExtraMenus = inject<{ value: MenuRecordRaw[] } | null>(
  'SIDEBAR_EXTRA_MENUS',
  null,
);

/**
 * 얹은 묶음의 경로에 붙이는 접두사.
 *
 * 즐겨찾기에 담은 메뉴는 아래 트리에도 **같은 경로로** 한 번 더 나타난다.
 * 경로가 같으면 메뉴 컴포넌트가 둘을 항목 하나로 본다(`items` 가 경로로 키를
 * 잡고, 활성 판정도 `path === activePath` 하나뿐이다). 그래서 어느 쪽을 눌러도
 * 양쪽이 함께 활성으로 그려지고, 활성 항목으로 스크롤할 때도 먼저 찾은
 * 즐겨찾기 쪽으로 뛴다.
 *
 * 얹을 때 접두사를 붙여 **신원을 나눈다.** 이동할 곳은 `link` 에 남기므로
 * 눌렀을 때 가는 화면은 그대로다.
 */
const EXTRA_MENU_PREFIX = '__extra__:';

// 얹은 묶음(즐겨찾기). 이름 번역은 트리와 같은 규칙($tIfKey)을 거친다 —
// 제목이 다국어 키일 수 있고, 메뉴 검색 대상에도 들어가야 한다.
const wrappedExtraMenus = computed(() =>
  mapTree(sidebarExtraMenus?.value ?? [], (item) => ({
    ...cloneDeep(item),
    link: item.path,
    name: $tIfKey(item.name),
    path: `${EXTRA_MENU_PREFIX}${item.path}`,
  })),
);

/** 접두사 붙은 경로 → 실제로 이동할 경로. */
const extraMenuLinks = computed(() => {
  const links = new Map<string, string>();
  const walk = (nodes: MenuRecordRaw[]) => {
    for (const node of nodes) {
      if (node.link) {
        links.set(node.path, node.link);
      }
      if (node.children?.length) {
        walk(node.children);
      }
    }
  };
  walk(wrappedExtraMenus.value);
  return links;
});

// 이름 번역이 반영된 사이드바 메뉴 (검색 대상)
const wrappedSidebarMenus = computed(() => [
  ...wrappedExtraMenus.value,
  ...wrapperMenus(sidebarMenus.value),
]);

/**
 * 사이드바에서 마지막으로 고른 항목의 신원.
 *
 * 즐겨찾기와 트리에 같은 메뉴가 있을 때 **어느 쪽을 눌렀는지**를 기억한다.
 * 라우트만으로는 구분할 수 없다 — 둘이 같은 화면으로 가기 때문이다.
 */
const pickedSidebarKey = ref('');

/**
 * 사이드바에서 활성으로 그릴 항목.
 *
 * 기본은 라우트가 가리키는 **트리 쪽**이다. 방금 즐겨찾기를 눌러서 왔고 그
 * 항목이 지금 화면과 같은 곳을 가리킬 때만 즐겨찾기 쪽을 활성으로 둔다.
 * 그래서 주소창·탭·메뉴 검색으로 들어오면 트리 쪽이 활성이 된다.
 */
const sidebarActiveKey = computed(() => {
  const picked = pickedSidebarKey.value;
  if (picked && extraMenuLinks.value.get(picked) === sidebarActive.value) {
    return picked;
  }
  return sidebarActive.value;
});

// 고른 기억은 **그 화면에 있는 동안만** 유효하다. 화면이 바뀌면 지운다.
// 지우지 않으면 즐겨찾기로 열었던 화면에 나중에 탭으로 다시 들어올 때
// 누르지도 않은 즐겨찾기가 활성으로 되살아난다.
watch(sidebarActive, () => {
  if (
    pickedSidebarKey.value &&
    extraMenuLinks.value.get(pickedSidebarKey.value) !== sidebarActive.value
  ) {
    pickedSidebarKey.value = '';
  }
});

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

function handleLogout() {
  emit('logout');
}

/** 레이아웃 컴포넌트 참조 — 모바일에서 로고로 사이드바를 열 때 쓴다. */
const adminLayoutRef = ref<null | { openMobileSidebar: () => void }>(null);

function clickLogo() {
  // 모바일에서는 로고가 곧 메뉴 손잡이다. 화면이 좁아 햄버거가 눈에 안 띄고,
  // 최상단 왼쪽에 보이는 것이 로고뿐이라 사용자가 그걸 누른다 (지시, 2026-09-04).
  // 서랍 상태는 vben-layout 내부에 있어 노출 메서드(openMobileSidebar)로 연다.
  if (preferences.app.isMobile) {
    adminLayoutRef.value?.openMobileSidebar();
    return;
  }
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
//
// 모바일 보정 둘 (우리 수정 — 40번 문서. 상위에 같은 수정이 있는지 확인 전):
//  · 100vh → 100dvh. 모바일 브라우저는 주소창이 접혔다 펴지며 100vh 와 실제
//    가시 높이가 어긋나는데, 바깥이 overflow-hidden 이라 어긋난 만큼 하단이
//    잘려 접근 불가가 된다. dvh 는 그 변동을 따라간다 (미지원 브라우저는 vh 폴백).
//  · 모바일에서는 탭바를 그리지 않으므로(아래 tabbarVisible) 높이에서도 빼지 않는다.
const supportsDvh =
  typeof CSS !== 'undefined' && CSS.supports?.('height', '100dvh');

// 탭바는 데스크톱 전용이다. 모바일에서 38px 띠 + 새로고침·최대화 버튼은
// 자리만 차지하고, 화면 전환은 사이드바(드로어)가 맡는다.
const tabbarVisible = computed(
  () => preferences.tabbar.enable && !preferences.app.isMobile,
);

const contentHeightStyle = computed(() => {
  const headerHeight = preferences.header.enable ? preferences.header.height : 0;
  const tabbarHeight = tabbarVisible.value ? preferences.tabbar.height : 0;
  return {
    height: `calc(${supportsDvh ? '100dvh' : '100vh'} - ${headerHeight + tabbarHeight}px)`,
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
    ref="adminLayoutRef"
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
    :header-toggle-sidebar-button="
      // 모바일에서는 로고가 사이드바를 열므로(clickLogo) 햄버거가 중복이다 (지시, 2026-09-04)
      preferences.widget.sidebarToggle && !preferences.app.isMobile
    "
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
    :sidebar-extra-title-height="sidebarExtraTitleHeight"
    :sidebar-hidden="preferences.sidebar.hidden"
    :sidebar-mixed-width="preferences.sidebar.mixedWidth"
    :sidebar-theme="sidebarTheme"
    :sidebar-theme-sub="sidebarThemeSub"
    :sidebar-width="preferences.sidebar.width"
    :side-collapse-width="preferences.sidebar.collapseWidth"
    :sidebar-logo-visible="preferences.logo.enable"
    :tabbar-enable="tabbarVisible"
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
    <!--
      로고 — JSINI 브랜드.

      `VbenLogo` 를 쓰지 않는다. 그것은 '아이콘 + 앱 이름 글자' 로 그리는데,
      그러면 워드마크가 화면 글꼴로 써진 'JSINI ADMIN' 이 된다.
      **워드마크는 그린 글자라 글꼴로 흉내내면 안 된다** (docs/brand/README.md).

      좌표는 `docs/brand/generate.py` 가 만든 것을 옮겼다. **여기서 고치지 않는다** —
      generate.py 를 고치고 다시 뽑아서 옮긴다.
      파일(`/brand/*.svg`)을 가져오지 않고 인라인한 이유는, 이 파일이 공용 패키지라
      특정 앱의 `public/` 에 있는 경로를 박으면 그 파일이 없는 앱에서 깨지기 때문이다.
      첫 화면에 무조건 필요한 것이라 요청을 하나 줄이는 뜻도 있다.

      펼친 사이드바는 가로 조합(심볼 + JSINI), 접으면 블레이드 J 한 자만 보인다.
      'Admin' 은 로고의 일부가 아니라 어느 시스템인지 알려 주는 꼬리표라 세로선으로 끊는다.
    -->
    <template #logo>
      <a
        v-if="preferences.logo.enable"
        :class="logoClass"
        class="flex h-full items-center gap-2 overflow-hidden px-3 transition-all duration-500"
        href="javascript:void 0"
        aria-label="JSINI"
        @click="clickLogo"
      >
        <!-- 접었을 때 — 블레이드 J 한 자 -->
        <svg
          v-if="logoCollapsed"
          class="size-8 shrink-0"
          viewBox="0 0 64 64"
          role="img"
          aria-label="JSINI"
        >
          <path
            d="M38.6665,20.0003 L46.6663,12.0005 L46.6663,51.9995 L17.3337,51.9995 L17.3337,43.9997 L38.6665,43.9997 Z"
            :fill="logoTheme === 'dark' ? '#FFFFFF' : '#0A0A0A'"
          />
        </svg>

        <!-- 펼쳤을 때 — 가로 조합 + Admin 꼬리표 -->
        <template v-else>
          <svg
            class="h-[22px] w-auto shrink-0"
            viewBox="0 0 228 60"
            role="img"
            aria-label="JSINI"
          >
            <g transform="translate(-18,-30)">
              <path
                d="M58,30 L102,30 L102,42 L70,42 L70,54 L102,54 L102,90 L58,90 L58,78 L90,78 L90,66 L58,66 Z"
                :fill="logoTheme === 'dark' ? '#D2D2D7' : '#6E6E73'"
              />
              <path
                d="M50,42 L62,30 L62,90 L18,90 L18,78 L50,78 Z"
                :fill="logoTheme === 'dark' ? '#FFFFFF' : '#0A0A0A'"
              />
            </g>
            <g :fill="logoTheme === 'dark' ? '#FFFFFF' : '#0A0A0A'">
              <path d="M124,21 L130,15 L130,45 L108,45 L108,39 L124,39 Z" />
              <path
                d="M140,15 L162,15 L162,21 L146,21 L146,27 L162,27 L162,45 L140,45 L140,39 L156,39 L156,33 L140,33 Z"
              />
              <path d="M172,15 L178,15 L178,45 L172,45 Z" />
              <path
                d="M188,45 L188,15 L194,15 L206,35 L206,15 L212,15 L212,45 L206,45 L194,25 L194,45 Z"
              />
              <path d="M222,15 L228,15 L228,45 L222,45 Z" />
            </g>
          </svg>
          <span class="bg-border h-3 w-px shrink-0" aria-hidden="true"></span>
          <span class="text-muted-foreground truncate text-[10px] tracking-[0.18em] uppercase">
            Admin
          </span>
        </template>
      </a>
    </template>
    <!-- 헤더 영역 -->
    <template #header>
      <LayoutHeader
        :avatar="avatar"
        :theme="theme"
        :text="text"
        @clear-preferences-and-logout="clearPreferencesAndLogout"
        @logout="handleLogout"
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
        <template v-for="item in headerSlots" #[item]>
          <slot :name="item"></slot>
        </template>
      </LayoutHeader>
    </template>
    <!-- 사이드 메뉴 영역 -->
    <template #menu>
      <!-- 접힌 사이드바에서는 검색창이 숨으므로 리로드 버튼만 따로 보여 준다 -->
      <div
        v-if="sidebarCollapsed"
        class="bg-sidebar sticky top-0 z-20 -mt-2 flex justify-center pb-2 pt-2"
      >
        <button
          :aria-label="$t('common.refresh')"
          class="text-muted-foreground hover:text-foreground hover:bg-accent flex size-8 items-center justify-center rounded transition-colors"
          :class="{ 'pointer-events-none opacity-60': menuReloading }"
          type="button"
          :title="$t('common.refresh')"
          @click="reloadMenus"
        >
          <svg
            class="size-4"
            :class="{ 'animate-spin': menuReloading }"
            fill="none"
            stroke="currentColor"
            stroke-linecap="round"
            stroke-width="2"
            viewBox="0 0 24 24"
          >
            <path d="M21 12a9 9 0 1 1-2.64-6.36" />
            <path d="M21 3v6h-6" />
          </svg>
        </button>
      </div>

      <!-- 메뉴 검색 입력부: 접힌 사이드바에서는 숨김, 스크롤 시 상단 고정 -->
      <div
        v-if="!sidebarCollapsed"
        class="bg-sidebar sticky top-0 z-20 -mt-2 flex items-center gap-1 px-2 pb-2 pt-2"
      >
        <!--
          min-w-0 이 없으면 사이드바를 좁혀도 이 칸이 줄어들지 않는다.

          flex 항목의 기본값은 `min-width: auto` 라서 **내용의 고유 최소 너비**보다
          작아지지 않는다. 그 내용이 `<input>` 인데, input 은 size 속성 기본값(20자)
          만큼의 고유 너비를 갖는다 — 실측 192px 이다. 그래서 이 칸이 192px 에 고정되고,
          옆의 새로고침 버튼(shrink-0)이 밖으로 밀려 잘렸다.
          사이드바 기본 너비(224px)에서도 이미 8px 잘려 있었다.
        -->
        <div class="relative min-w-0 flex-1">
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
          <!--
            오른쪽 여백은 지우기 버튼이 있을 때만 둔다. 항상 pr-8 을 두면
            좁은 사이드바에서 좌우 여백 64px 이 글자 자리를 다 먹는다.
          -->
          <Input
            v-model="menuSearchKeyword"
            :placeholder="$t('common.search')"
            class="h-8 w-full min-w-0 pl-8"
            :class="menuSearchKeyword ? 'pr-8' : 'pr-2'"
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

        <!--
          메뉴 다시 읽기.
          메뉴 관리에서 메뉴를 고친 뒤 눌러 좌측 메뉴를 최신 상태로 되돌린다.
        -->
        <button
          :aria-label="$t('common.refresh')"
          class="text-muted-foreground hover:text-foreground hover:bg-accent flex size-8 shrink-0 items-center justify-center rounded transition-colors"
          :class="{ 'pointer-events-none opacity-60': menuReloading }"
          type="button"
          :title="$t('common.refresh')"
          @click="reloadMenus"
        >
          <svg
            class="size-4"
            :class="{ 'animate-spin': menuReloading }"
            fill="none"
            stroke="currentColor"
            stroke-linecap="round"
            stroke-width="2"
            viewBox="0 0 24 24"
          >
            <path d="M21 12a9 9 0 1 1-2.64-6.36" />
            <path d="M21 3v6h-6" />
          </svg>
        </button>
      </div>
      <LayoutMenu
        :key="sidebarMenuKey"
        :accordion="preferences.navigation.accordion"
        :collapse="preferences.sidebar.collapsed"
        :collapse-show-title="preferences.sidebar.collapsedShowTitle"
        :default-active="sidebarActiveKey"
        :default-openeds="sidebarSearchOpenPaths"
        :menus="filteredSidebarMenus"
        :rounded="isMenuRounded"
        :scroll-to-active="preferences.sidebar.scrollToActive"
        :theme="sidebarTheme"
        mode="vertical"
        @open="handleMenuOpen"
        @select="handleSidebarMenuSelect"
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
        :show-text="preferences.logo.showText"
        :text="preferences.app.name"
        :theme="sidebarThemeSub"
      >
        <template v-if="$slots['logo-text']" #text>
          <slot name="logo-text"></slot>
        </template>
      </VbenLogo>
    </template>

    <template #tabbar>
      <!-- 모바일에서는 탭바를 숨긴다 — contentHeightStyle 의 tabbarVisible 과 짝 -->
      <LayoutTabbar
        v-if="tabbarVisible"
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
          <!-- 내용은 앱이 넣는다. 슬롯이 비어 있으면 빈 칸만 열린다. -->
          <slot name="ai-chat"></slot>
        </div>
      </div>
    </template>

    <template v-if="preferences.transition.loading" #content-overlay>
      <LayoutContentSpinner />
    </template>

    <!--
      푸터. 안에 들어가던 저작권 표시는 걷어냈다 —
      vben 의 회사명·사이트·중국 ICP 등록번호가 기본값으로 박혀 있던 것이라
      이 제품에서 보일 이유가 없다. 넣을 내용이 생기면 여기에 넣는다.
    -->
    <template v-if="preferences.footer.enable" #footer>
      <LayoutFooter />
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

      <!--
        떠 있는 설정 버튼은 모바일에서 숨긴다 (지시, 2026-09-04).
        화면 오른쪽 세로 가운데에 떠서 내용을 가리고, 모바일 헤더 정리
        (아바타만 남김)와 어긋난다. 설정이 필요하면 데스크톱에서 한다.
      -->
      <template v-if="preferencesButtonPosition.fixed && !preferences.app.isMobile">
        <Preferences
          class="fixed top-1/2 right-0 z-100 -translate-y-1/2 transform"
          @clear-preferences-and-logout="clearPreferencesAndLogout"
        />
      </template>
      <VbenBackTop :target="layoutScrollTarget" />
    </template>
  </VbenAdminLayout>
</template>
