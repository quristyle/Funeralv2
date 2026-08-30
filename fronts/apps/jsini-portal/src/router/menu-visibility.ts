import type { MenuRecordRaw } from '@vben/types';

/**
 * [사이드바 메뉴목록 거르기]
 *
 * 라우트에서 만든 메뉴 트리를 사이드바에 넣기 전에 두 가지로 거른다.
 *
 *  1. **열람 권한** — 열람할 수 없는 화면은 목록에 넣지 않는다.
 *  2. **화면 크기** — 메뉴마다 정한 `use_mobile` · `use_tablet`
 *     (`scom.system_menus`, [메뉴 관리]에서 정한다).
 *
 * **거르는 것은 목록뿐이다. 라우트는 그대로 만든다.**
 * 목록에서 빠진 화면도 주소로 직접 들어가면 라우트는 있고, 거기서 실제로 막는 것은
 * 열람 가드다(`router/guard.ts`). 목록에서 지우는 것은 통제가 아니라 정리다 —
 * 통제는 가드와 서버가 한다. 이 둘을 섞으면 "목록에 없으니 안전하다" 는 착각이 생긴다.
 *
 * [열람 권한 판정을 왜 밖에서 받나]
 *
 * 판정은 권한 스토어의 `canViewMenu()` 하나뿐이어야 한다 — 가드와 같은 판정을
 * 써야 "목록에 보이는데 누르면 403" 이 생기지 않는다. 이 파일이 스토어를 직접
 * 들여오는 대신 함수로 받으면, 판정이 한 곳에 남으면서 이 파일은 스토어 없이도
 * 시험할 수 있다.
 *
 * [화면 크기 규칙을 왜 미리 걷어 두나]
 *
 * 사이드바가 쓰는 `MenuRecordRaw` 는 `generateMenus`(packages/utils)가 만드는데,
 * 이때 우리가 붙인 meta 값은 떨어져 나간다. 그래서 API 응답에서 규칙을 미리 걷어
 * 두고 나중에 맞춰 본다. `MenuRecordRaw.path` 는 링크 메뉴면 `meta.link`,
 * `MenuRecordRaw.name` 은 `meta.title` 이 되므로 그 넷을 모두 열쇠로 담는다.
 */

/** 한 메뉴에 대해 API 응답에서 걷어 둔 것 */
interface MenuRule {
  /**
   * 자기 화면이 없는 묶음(CATALOG)인가.
   *
   * 묶음과 "자식을 가진 메뉴" 를 갈라 보려고 들고 있다. 자식이 있다고 다 묶음이
   * 아니다 — 예컨대 `/status`(현황관리)는 자식이 다섯인 **화면 있는 메뉴**다.
   * 이걸 묶음으로 다루면 자식이 모두 걸러졌을 때 자기 열람 권한이 있는데도
   * 함께 사라지고, 그 위 묶음까지 빈 묶음이 되어 사이드바가 통째로 비어 버린다.
   */
  isCatalog: boolean;
  useMobile: boolean;
  useTablet: boolean;
}

/** 화면 크기 구분. 데스크톱은 크기 규칙과 무관하게 다 보인다. */
type Viewport = 'desktop' | 'phone' | 'tablet';

/** 이 경로를 열람할 수 있는가. 권한 스토어의 `canViewMenu` 를 받는다. */
type CanViewFn = (path: string) => boolean;

/** 휴대폰: 40번 문서의 모바일 기준(vben `isMobile` = md 미만)과 같다. */
const PHONE_QUERY = '(max-width: 767px)';
/** 태블릿: tailwind 의 md 이상 lg 미만. */
const TABLET_QUERY = '(min-width: 768px) and (max-width: 1023px)';

/** API 응답에서 걷어 둔 규칙. 열쇠는 경로·링크·번역키·이름 어느 것이든 될 수 있다. */
const menuRules = new Map<string, MenuRule>();

/** 거르기 전의 메뉴. 크기나 권한이 바뀌면 여기서 다시 거른다. */
let fullMenus: MenuRecordRaw[] = [];

/** 걸러진 목록을 넣어 줄 곳 */
let applyMenus: ((menus: MenuRecordRaw[]) => void) | null = null;

/** 지금 쓰는 열람 권한 판정 */
let canView: CanViewFn = () => true;

let watching = false;

function matches(query: string) {
  return (
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia(query).matches
  );
}

/** 지금 화면 크기 */
function currentViewport(): Viewport {
  if (matches(PHONE_QUERY)) return 'phone';
  if (matches(TABLET_QUERY)) return 'tablet';
  return 'desktop';
}

/**
 * `/auth/menu/all` 응답에서 거르기에 필요한 것을 걷는다(유형 · 화면 크기).
 *
 * 라우트를 만들기 **전에** 불러야 한다. 이 응답에만 `meta.type` · `meta.useMobile`
 * 이 실려 있고, 라우트·메뉴로 옮겨지는 과정에서 떨어져 나가기 때문이다.
 */
export function collectMenuRules(menus: any[]) {
  menuRules.clear();

  const walk = (list: any[]) => {
    for (const menu of list ?? []) {
      const meta = menu?.meta ?? {};
      // 크기 값이 없으면 보이는 쪽이다 — 이 설정을 모르던 시절의 메뉴가 사라지면 안 된다.
      const rule: MenuRule = {
        isCatalog: String(meta.type ?? '').toUpperCase() === 'CATALOG',
        useMobile: meta.useMobile !== false,
        useTablet: meta.useTablet !== false,
      };

      for (const key of [menu?.path, meta.link, meta.title, menu?.name]) {
        if (typeof key === 'string' && key.length > 0) {
          menuRules.set(key, rule);
        }
      }

      if (menu?.children?.length) walk(menu.children);
    }
  };

  walk(menus);
}

/** 이 메뉴에 대해 걷어 둔 규칙. 경로로 못 찾으면 이름으로 찾는다. */
function ruleOf(menu: MenuRecordRaw) {
  return menuRules.get(menu.path) ?? menuRules.get(menu.name);
}

/** 이 메뉴가 지금 크기의 목록에 보여야 하는가. 규칙을 못 찾으면 보인다. */
function fitsViewport(menu: MenuRecordRaw, viewport: Viewport) {
  if (viewport === 'desktop') return true;

  const rule = ruleOf(menu);
  if (!rule) return true;

  return viewport === 'phone' ? rule.useMobile : rule.useTablet;
}

/**
 * 이 메뉴가 **자기 화면**으로서 목록에 남을 자격이 있는가.
 *
 * 묶음(CATALOG)은 자기 화면이 없으므로 언제나 아니다 — 남은 자식으로만 판단한다.
 * 그 밖의 메뉴는 열람 권한을 따진다. 외부 링크는 앱 안의 화면이 아니라 권한표에도
 * 없으므로 그대로 남긴다.
 */
function isOwnScreen(menu: MenuRecordRaw) {
  if (ruleOf(menu)?.isCatalog) return false;
  if (!menu.path || menu.path.startsWith('http')) return true;
  return canView(menu.path);
}

/**
 * 메뉴 트리를 걸러 낸다.
 *
 * 부모가 빠지면 그 아래도 함께 빠진다. 자식이 **모두** 빠져 버린 묶음(디렉터리)도
 * 함께 뺀다 — 눌러도 아무것도 없는 빈 묶음이 남는 것을 막기 위해서다.
 *
 * 열람 권한은 **화면이 있는 메뉴에만** 따진다. 디렉터리는 화면이 없어 열람 권한이
 * 꺼져 있으므로(현재 43건), 디렉터리에 권한을 따지면 트리가 통째로 사라진다.
 * 디렉터리는 남은 자식이 있느냐로만 판단한다.
 */
function filterMenus(
  menus: MenuRecordRaw[],
  viewport: Viewport,
): MenuRecordRaw[] {
  const walk = (list: MenuRecordRaw[]): MenuRecordRaw[] => {
    const kept: MenuRecordRaw[] = [];

    for (const menu of list) {
      if (!fitsViewport(menu, viewport)) continue;

      const children = menu.children ?? [];
      const keptChildren = children.length > 0 ? walk(children) : [];

      // 남은 자식이 있거나, 자기 자신이 열 수 있는 화면이면 남긴다.
      // 둘 다 아니면 뺀다 — 자식이 모두 걸러진 묶음이 그렇다.
      if (keptChildren.length === 0 && !isOwnScreen(menu)) continue;

      // 자식이 모두 걸러졌으면 `children` 을 빈 배열로 남긴다.
      // 사이드바는 자식이 있을 때만 펼치는 묶음으로 그리므로(menu-ui sub-menu.vue),
      // 빈 배열이어야 눌러서 바로 열리는 항목이 된다.
      kept.push(children.length > 0 ? { ...menu, children: keptChildren } : menu);
    }

    return kept;
  };

  return walk(menus);
}

/**
 * 라우트에서 만든 메뉴를 걸러 사이드바에 넣는다.
 *
 * `accessStore.setAccessMenus(...)` 를 직접 부르는 대신 이 함수를 쓴다.
 * 거르기 전 목록을 들고 있다가 화면 크기가 바뀌면 다시 걸러 주기 때문이다
 * (기기 회전·창 크기 조절·개발자 도구의 기기 모드).
 *
 * **권한을 먼저 받아 둔 뒤에 부른다.** 못 받은 상태에서는 `canViewMenu` 가
 * 전부 통과시키므로 걸러지지 않은 목록이 그대로 들어간다.
 *
 * @param menus 라우트에서 만든 메뉴(거르기 전)
 * @param setMenus 걸러진 메뉴를 넣을 곳. 보통 `accessStore.setAccessMenus`
 * @param canViewMenu 열람 권한 판정. 권한 스토어의 `canViewMenu`
 */
export function setVisibleMenus(
  menus: MenuRecordRaw[],
  setMenus: (menus: MenuRecordRaw[]) => void,
  canViewMenu: CanViewFn,
) {
  fullMenus = menus;
  applyMenus = setMenus;
  canView = canViewMenu;
  reapply();
  startViewportWatch();
}

/** 들고 있던 목록을 지금 기준으로 다시 걸러 넣는다. */
function reapply() {
  applyMenus?.(filterMenus(fullMenus, currentViewport()));
}

/** 화면 크기가 바뀌면 다시 거른다. 한 번만 건다. */
function startViewportWatch() {
  if (watching || typeof window === 'undefined') return;
  if (typeof window.matchMedia !== 'function') return;
  watching = true;

  for (const query of [PHONE_QUERY, TABLET_QUERY]) {
    const list = window.matchMedia(query);
    // 사파리 14 이하는 addEventListener 가 없다 — 있을 때만 건다.
    if (typeof list.addEventListener === 'function') {
      list.addEventListener('change', reapply);
    }
  }
}
