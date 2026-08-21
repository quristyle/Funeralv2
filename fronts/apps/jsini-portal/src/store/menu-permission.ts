import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

import { getMenuPermissionsApi, type MenuPermission } from '#/api/core/menu';

/** 아무 권한도 없는 상태. 조회조차 안 되는 메뉴에 쓴다. */
export const EMPTY_PERMISSION: MenuPermission = {
  canCreate: false,
  canCust1: false,
  canCust2: false,
  canCust3: false,
  canCust4: false,
  canCust5: false,
  canCust6: false,
  canCust7: false,
  canCust8: false,
  canDelete: false,
  canExcel: false,
  canPrint: false,
  canSearch: false,
  canUpdate: false,
  canView: false,
  menuId: '',
  path: '',
};

/**
 * [메뉴 권한 스토어]
 *
 * JSini 포털은 여러 MSA(장례식장·헬프데스크 …)를 한 화면 안에 담는다.
 * 각 MSA 가 저마다 권한을 들고 있으면 관리가 갈라지므로,
 * 권한은 포털 한 곳(`scom.roles` / `scom.role_menus`)에서만 관리하고
 * 모든 화면이 그 결과를 따른다.
 *
 * 이 스토어는 로그인 후 `/auth/menu/permissions` 를 한 번 받아 두고,
 * 화면들은 `useMenuPermission()` 으로 자기 경로에 해당하는 권한을 꺼내 쓴다.
 *
 * 서버가 이미 두 가지를 반영해서 내려준다.
 *  - 사용자가 속한 여러 역할의 권한을 OR 로 합친 값
 *  - 메뉴가 "사용하지 않는다"고 지정한 항목은 꺼진 값
 * 그래서 화면은 이 값만 그대로 믿으면 된다.
 */
export const useMenuPermissionStore = defineStore('menu-permission', () => {
  /** 경로 → 권한 */
  const byPath = ref<Record<string, MenuPermission>>({});
  const loaded = ref(false);
  const loading = ref(false);

  /**
   * 권한 목록을 받아 둔다.
   * 로그인 직후 한 번 부르면 되고, 역할이 바뀌었을 때만 다시 부른다.
   */
  async function load(force = false) {
    if (loading.value) return;
    if (loaded.value && !force) return;

    loading.value = true;
    try {
      const list = (await getMenuPermissionsApi()) ?? [];
      const map: Record<string, MenuPermission> = {};
      list.forEach((item) => {
        if (item.path) map[normalize(item.path)] = item;
      });
      byPath.value = map;
      loaded.value = true;
    } finally {
      loading.value = false;
    }
  }

  /**
   * 경로와 정확히 일치하는 메뉴의 권한. 없으면 undefined.
   *
   * 열람 가드는 이것만 쓴다. 접두어로 상위 메뉴를 물려받게 하면
   * 화면이 없는 디렉터리(열람 권한이 꺼져 있다)나 메뉴에 등록되지 않은
   * 상세 경로가 통째로 막히기 때문이다.
   */
  function findExact(path: string): MenuPermission | undefined {
    return byPath.value[normalize(path)];
  }

  /**
   * 경로로 권한을 찾는다. 버튼 표시 여부를 정할 때 쓴다.
   *
   * 상세·등록 화면처럼 `/helpdesk/request/detail/123` 같은 하위 경로는
   * 메뉴에 등록되어 있지 않다. 이럴 때는 가장 길게 일치하는 상위 메뉴의 권한을 쓴다.
   * 다만 화면이 없는 디렉터리는 모든 권한이 꺼져 있으므로 후보에서 뺀다 —
   * 그러지 않으면 상세 화면의 버튼이 전부 사라진다.
   */
  function resolve(path: string): MenuPermission {
    const target = normalize(path);
    const exact = byPath.value[target];
    if (exact) return exact;

    let best: MenuPermission | undefined;
    let bestLength = 0;
    for (const [menuPath, perm] of Object.entries(byPath.value)) {
      // 화면이 없는 디렉터리는 모든 권한이 꺼져 있다. 물려받을 대상이 아니다.
      if (!hasAnyPermission(perm)) continue;
      if (
        menuPath.length > bestLength &&
        (target === menuPath || target.startsWith(`${menuPath}/`))
      ) {
        best = perm;
        bestLength = menuPath.length;
      }
    }
    return best ?? EMPTY_PERMISSION;
  }

  function $reset() {
    byPath.value = {};
    loaded.value = false;
    loading.value = false;
  }

  const isLoaded = computed(() => loaded.value);

  /**
   * 이 사용자에게 권한 정보가 하나라도 있는지.
   *
   * 역할이 하나도 배정되지 않은 계정은 목록이 비어서 온다.
   * 이때 "전부 거부"로 다루면 그 계정은 화면에 들어갈 수는 있는데
   * (열람 가드는 통과시킨다) 버튼은 하나도 안 보이는 이상한 상태가 된다.
   * 통합 이행 중에는 판단을 한쪽으로 모아 둔다 — 정보가 아예 없으면 막지 않는다.
   * 역할을 배정하는 순간부터 실제 권한이 그대로 적용된다.
   */
  const hasAnyData = computed(() => Object.keys(byPath.value).length > 0);

  return {
    $reset,
    byPath,
    findExact,
    hasAnyData,
    isLoaded,
    load,
    loading,
    resolve,
  };
});

/** 권한이 하나라도 켜져 있는지. 디렉터리(전부 꺼짐)를 걸러내는 데 쓴다. */
function hasAnyPermission(p: MenuPermission) {
  return (
    p.canView ||
    p.canSearch ||
    p.canCreate ||
    p.canUpdate ||
    p.canDelete ||
    p.canPrint ||
    p.canExcel ||
    p.canCust1 ||
    p.canCust2 ||
    p.canCust3 ||
    p.canCust4 ||
    p.canCust5 ||
    p.canCust6 ||
    p.canCust7 ||
    p.canCust8
  );
}

/** 끝의 슬래시와 대소문자 차이를 없앤다. */
function normalize(path: string) {
  const trimmed = path.trim().toLowerCase();
  return trimmed.length > 1 && trimmed.endsWith('/')
    ? trimmed.slice(0, -1)
    : trimmed;
}
