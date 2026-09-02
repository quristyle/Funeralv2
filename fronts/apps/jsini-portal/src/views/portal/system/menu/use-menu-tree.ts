import type { SystemMenuApi } from '#/api/portal/system/menu';

import { getMenuList } from '#/api/portal/system/menu';

/**
 * [메뉴 관리 화면이 나눠 쓰는 메뉴 트리]
 *
 * 이 화면에는 같은 목록을 필요로 하는 곳이 둘이다.
 *
 *   1. 그리드 — 화면에 그리는 목록
 *   2. 수정 창의 **상위메뉴 선택기**(`ApiTreeSelect`)
 *
 * 그런데 선택기는 자기 `api` 를 직접 불러서, 창을 열 때마다 그리드가 방금 받은 것과
 * **똑같은 180행을 다시 받았다.** 창은 `destroyOnClose` 라 열 때마다 새로 만들어지므로
 * 수정 단추를 누르는 횟수만큼 반복됐다(전체 새로고침 때는 선택기가 미리 떠서 두 번).
 *
 * 그래서 받아 둔 것을 여기 한 곳에 두고 둘이 나눠 쓴다.
 *
 *   - `reloadMenuTree()` — **그리드가** 쓴다. 언제나 서버에 묻고 받은 것을 여기 둔다.
 *     (첫 진입 · 새로고침 단추 · 저장한 뒤)
 *   - `loadMenuTree()` — **선택기가** 쓴다. 받아 둔 것이 있으면 그것을 쓴다.
 *
 * 덕분에 선택기가 보는 목록은 **그리드가 보고 있는 것과 항상 같다.**
 * 저장에 성공하면 화면이 `refresh()` 로 다시 받으므로 여기 있는 것도 함께 새로워진다.
 *
 * 두 곳이 같은 순간에 부르면(전체 새로고침) 진행 중인 요청에 함께 붙어 **한 번만** 나간다.
 *
 * > 트리의 노드는 그리드 행과 `meta` 를 **참조로 공유한다.** 배지를 눌러 `meta` 를 고치면
 * > 여기 있는 것도 같이 바뀌는데, 둘이 같은 것을 보여야 하므로 그게 맞다.
 * > 선택기는 `afterFetch` 에서 노드를 복사해 쓰므로 이쪽을 되고치지 않는다.
 */

/** 받아 둔 트리. 화면을 떠나면 비운다. */
let cachedTree: null | SystemMenuApi.SystemMenu[] = null;
/** 받아 둔 트리가 어느 언어인지. 언어가 바뀌면 다시 받아야 한다. */
let cachedLocale: null | string = null;
/** 지금 나가 있는 요청. 같은 순간에 둘이 부르면 여기에 함께 붙는다. */
let inflight: null | Promise<SystemMenuApi.SystemMenu[]> = null;

/** 응답이 배열/`result`/`data.result` 중 무엇으로 와도 목록을 꺼낸다. */
function getMenuItems(response: any): SystemMenuApi.SystemMenu[] {
  if (Array.isArray(response)) return response;
  if (Array.isArray(response?.result)) return response.result;
  if (Array.isArray(response?.data?.result)) return response.data.result;
  return [];
}

/**
 * 서버에서 새로 받는다. **그리드가 쓴다.**
 *
 * 새로고침 단추가 정말 서버에 묻도록, 받아 둔 것이 있어도 무조건 다시 받는다.
 */
export function reloadMenuTree(
  locale?: string,
): Promise<SystemMenuApi.SystemMenu[]> {
  const key = locale ?? '';
  cachedLocale = key;
  const request = getMenuList(locale)
    .then((response) => {
      const tree = getMenuItems(response);
      // 늦게 온 응답이 새 언어의 것을 덮지 않도록 언어가 그대로일 때만 담는다.
      if (cachedLocale === key) cachedTree = tree;
      return tree;
    })
    .finally(() => {
      if (inflight === request) inflight = null;
    });
  inflight = request;
  return request;
}

/**
 * 받아 둔 것이 있으면 그것을 쓴다. **수정 창의 상위메뉴 선택기가 쓴다.**
 *
 * 없거나(첫 진입) 언어가 다르면 한 번 받는다 — 그때는 예전과 똑같이 동작한다.
 */
export function loadMenuTree(
  locale?: string,
): Promise<SystemMenuApi.SystemMenu[]> {
  const key = locale ?? '';
  if (cachedLocale === key) {
    // 그리드가 지금 받고 있으면 그 요청에 함께 붙는다(요청이 겹치지 않는다).
    if (inflight) return inflight;
    if (cachedTree) return Promise.resolve(cachedTree);
  }
  return reloadMenuTree(locale);
}

/**
 * 받아 둔 것을 비운다. 화면을 떠날 때 부른다.
 *
 * 다음에 다시 들어왔을 때 **옛 트리를 잠깐이라도 쓰지 않게** 하려는 것이다.
 * 비워 두면 그리드가 첫 조회를 하고, 선택기는 그 요청에 함께 붙는다.
 */
export function clearMenuTree() {
  cachedTree = null;
  cachedLocale = null;
}
