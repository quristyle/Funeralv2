import type { MenuFavoriteApi } from '#/api/portal/system/menu-favorite';

import { computed, ref } from 'vue';

import { defineStore } from 'pinia';

import {
  addMenuFavorite,
  getMenuFavorites,
  removeMenuFavorite,
} from '#/api/portal/system/menu-favorite';

/** 경로에서 조회 조건·해시를 떼어낸다. 탭의 `fullPath` 에는 `?id=3` 이 붙어 온다. */
function cleanPath(path: string) {
  const bare = String(path ?? '')
    .split('?')[0]!
    .split('#')[0]!
    .replace(/\/+$/, '');
  return bare.length > 0 ? bare : '/';
}

/**
 * 사용자별 즐겨찾기 메뉴.
 *
 * 두 곳이 이 스토어를 본다.
 *
 * - **탭 오른쪽 메뉴** — 지금 탭이 즐겨찾기인지에 따라 '추가' 또는 '제거' 를 보여준다
 * - **사이드바 즐겨찾기 묶음** — 담아 둔 메뉴를 맨 위에 모아 보여준다
 *
 * 서버가 추가·해제 응답으로 **갱신된 목록 전체**를 주므로, 바꾼 뒤 다시 받으러
 * 한 번 더 부르지 않는다.
 */
export const useMenuFavoriteStore = defineStore('menu-favorite', () => {
  const favorites = ref<MenuFavoriteApi.MenuFavorite[]>([]);
  /** 한 번이라도 불러왔는지. '아직 안 불러옴' 과 '즐겨찾기가 없음' 을 구분한다. */
  const loaded = ref(false);
  /** 추가·해제가 진행 중인지. 연달아 누르는 것을 막는다. */
  const saving = ref(false);

  /** 즐겨찾기한 경로 집합. 판정을 O(1) 로 한다 — 탭마다 매번 훑지 않게. */
  const favoritePaths = computed(
    () => new Set(favorites.value.map((f) => cleanPath(f.path))),
  );

  /** 이 경로가 즐겨찾기인가 */
  function isFavorite(path?: string) {
    if (!path) return false;
    return favoritePaths.value.has(cleanPath(path));
  }

  /**
   * 즐겨찾기를 받아 온다.
   *
   * 실패해도 조용히 넘어간다 — 즐겨찾기는 곁들이는 기능이라, 이것 때문에
   * 화면 전체가 오류로 떨어지면 안 된다. 못 받아오면 빈 목록으로 남는다.
   */
  async function load(forceRefresh = false) {
    if (loaded.value && !forceRefresh) return favorites.value;

    try {
      favorites.value = (await getMenuFavorites()) ?? [];
    } catch {
      favorites.value = [];
    } finally {
      loaded.value = true;
    }
    return favorites.value;
  }

  /** 즐겨찾기에 담는다. 이미 담겨 있으면 서버가 그대로 둔다. */
  async function add(path: string) {
    if (saving.value) return;
    saving.value = true;
    try {
      favorites.value = (await addMenuFavorite(cleanPath(path))) ?? [];
      loaded.value = true;
    } finally {
      saving.value = false;
    }
  }

  /** 즐겨찾기에서 뺀다. */
  async function remove(path: string) {
    if (saving.value) return;
    saving.value = true;
    try {
      favorites.value = (await removeMenuFavorite(cleanPath(path))) ?? [];
      loaded.value = true;
    } finally {
      saving.value = false;
    }
  }

  function $reset() {
    favorites.value = [];
    loaded.value = false;
    saving.value = false;
  }

  return {
    $reset,
    add,
    favoritePaths,
    favorites,
    isFavorite,
    load,
    loaded,
    remove,
    saving,
  };
});
