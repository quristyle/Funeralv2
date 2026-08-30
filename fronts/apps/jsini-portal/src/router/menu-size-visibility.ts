import type { MenuRecordRaw } from '@vben/types';

/**
 * [화면 크기별 메뉴목록 노출]
 *
 * 포털은 PWA 라 휴대폰·태블릿에서도 같은 메뉴를 받는다(39·40번 문서).
 * 데스크톱에서만 쓸모 있는 화면(넓은 그리드·조직도·배치 편집 등)까지 휴대폰
 * 메뉴목록에 그대로 나오면 목록만 길어진다. 그래서 메뉴마다 두 값을 둔다 —
 * `scom.system_menus.use_mobile` · `use_tablet` ([메뉴 관리]에서 정한다).
 *
 * **거르는 것은 메뉴목록뿐이다.** 라우트는 그대로 만들어지므로 주소로 직접
 * 들어가거나 즐겨찾기·고정 탭으로 열면 화면은 열린다. 아예 못 들어가게 하려면
 * `status = 0`(비활성)이나 역할 권한을 쓴다 — 뜻이 다른 장치다.
 *
 * [왜 라우트 생성 뒤에 거르나]
 *
 * 백엔드가 크기를 판정해 내려주면 훨씬 간단하지만, 그러면 라우트도 함께
 * 사라져 휴대폰에서 그 주소가 404 가 된다. 목적은 "목록을 짧게" 이지
 * "못 들어가게" 가 아니므로, 라우트는 다 만들고 목록만 여기서 거른다.
 *
 * [왜 경로·이름 둘 다로 찾나]
 *
 * 사이드바가 쓰는 `MenuRecordRaw` 는 `generateMenus`(packages/utils)가 만드는데,
 * 이때 우리가 붙인 meta 값은 떨어져 나간다. 그래서 API 응답에서 규칙을 미리
 * 걷어 두고 나중에 맞춰 본다. `MenuRecordRaw.path` 는 링크 메뉴면 `meta.link`,
 * `MenuRecordRaw.name` 은 `meta.title` 이 되므로 그 넷을 모두 열쇠로 담는다.
 */

/** 한 메뉴의 크기별 노출 규칙 */
interface MenuSizeRule {
  useMobile: boolean;
  useTablet: boolean;
}

/** 화면 크기 구분. 데스크톱은 이 기능과 무관하게 항상 다 보인다. */
type Viewport = 'desktop' | 'phone' | 'tablet';

/** 휴대폰: 40번 문서의 모바일 기준(vben `isMobile` = md 미만)과 같다. */
const PHONE_QUERY = '(max-width: 767px)';
/** 태블릿: tailwind 의 md 이상 lg 미만. */
const TABLET_QUERY = '(min-width: 768px) and (max-width: 1023px)';

/** API 응답에서 걷어 둔 규칙. 열쇠는 경로·링크·번역키·이름 어느 것이든 될 수 있다. */
const rules = new Map<string, MenuSizeRule>();

/** 거르기 전의 메뉴. 크기가 바뀌면 여기서 다시 거른다. */
let fullMenus: MenuRecordRaw[] = [];

/** 지금 화면 크기를 적용해 주는 곳. 크기가 바뀌면 이 함수를 다시 부른다. */
let applyMenus: ((menus: MenuRecordRaw[]) => void) | null = null;

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
 * `/auth/menu/all` 응답에서 크기별 노출 규칙을 걷는다.
 *
 * 라우트를 만들기 **전에** 불러야 한다. 이 응답에만 `meta.useMobile` 이 실려 있고,
 * 라우트·메뉴로 옮겨지는 과정에서 떨어져 나가기 때문이다.
 */
export function collectMenuSizeRules(menus: any[]) {
  rules.clear();

  const walk = (list: any[]) => {
    for (const menu of list ?? []) {
      const meta = menu?.meta ?? {};
      // 값이 없으면 보이는 쪽이다 — 이 기능을 모르던 시절의 메뉴가 사라지면 안 된다.
      const rule: MenuSizeRule = {
        useMobile: meta.useMobile !== false,
        useTablet: meta.useTablet !== false,
      };

      for (const key of [menu?.path, meta.link, meta.title, menu?.name]) {
        if (typeof key === 'string' && key.length > 0) {
          rules.set(key, rule);
        }
      }

      if (menu?.children?.length) walk(menu.children);
    }
  };

  walk(menus);
}

/** 이 메뉴가 지금 크기의 목록에 보여야 하는가. 규칙을 못 찾으면 보인다. */
function isVisibleAt(menu: MenuRecordRaw, viewport: Viewport) {
  if (viewport === 'desktop') return true;

  const rule = rules.get(menu.path) ?? rules.get(menu.name);
  if (!rule) return true;

  return viewport === 'phone' ? rule.useMobile : rule.useTablet;
}

/**
 * 지금 화면 크기에 맞게 메뉴 트리를 거른다.
 *
 * 부모가 빠지면 그 아래도 함께 빠진다. 자식이 **모두** 빠져 버린 묶음(디렉토리)도
 * 함께 뺀다 — 눌러도 아무것도 없는 빈 묶음이 남는 것을 막기 위해서다.
 */
function filterBySize(
  menus: MenuRecordRaw[],
  viewport: Viewport,
): MenuRecordRaw[] {
  if (viewport === 'desktop') return menus;

  const walk = (list: MenuRecordRaw[]): MenuRecordRaw[] => {
    const kept: MenuRecordRaw[] = [];

    for (const menu of list) {
      if (!isVisibleAt(menu, viewport)) continue;

      const children = menu.children ?? [];
      if (children.length === 0) {
        kept.push(menu);
        continue;
      }

      const keptChildren = walk(children);
      if (keptChildren.length === 0) continue;

      kept.push({ ...menu, children: keptChildren });
    }

    return kept;
  };

  return walk(menus);
}

/**
 * 라우트에서 만든 메뉴를 화면 크기에 맞게 걸러 저장한다.
 *
 * `accessStore.setAccessMenus(...)` 를 직접 부르는 대신 이 함수를 쓴다.
 * 거르기 전 목록을 들고 있다가 크기가 바뀌면 다시 걸러 주기 때문이다
 * (기기 회전·창 크기 조절·개발자 도구의 기기 모드).
 *
 * @param menus 라우트에서 만든 메뉴(거르기 전)
 * @param setMenus 걸러진 메뉴를 저장할 곳. 보통 `accessStore.setAccessMenus`
 */
export function setMenusForViewport(
  menus: MenuRecordRaw[],
  setMenus: (menus: MenuRecordRaw[]) => void,
) {
  fullMenus = menus;
  applyMenus = setMenus;
  setMenus(filterBySize(menus, currentViewport()));
  startViewportWatch();
}

/** 크기가 바뀌면 저장된 목록을 다시 거른다. 한 번만 건다. */
function startViewportWatch() {
  if (watching || typeof window === 'undefined') return;
  if (typeof window.matchMedia !== 'function') return;
  watching = true;

  const reapply = () => {
    if (!applyMenus) return;
    applyMenus(filterBySize(fullMenus, currentViewport()));
  };

  for (const query of [PHONE_QUERY, TABLET_QUERY]) {
    const list = window.matchMedia(query);
    // 사파리 14 이하는 addEventListener 가 없다 — 있을 때만 건다.
    if (typeof list.addEventListener === 'function') {
      list.addEventListener('change', reapply);
    }
  }
}
