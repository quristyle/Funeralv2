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
   * [체험 모드] 권한 예시 화면(`/system/perm-sample`)이 쓴다.
   *
   * 실제 권한은 그대로 두고 **한 경로의 값만** 임시로 덮어써서
   * "이 권한을 켜면 화면이 어떻게 보이는지" 를 눌러 가며 확인하게 한다.
   * 역할 관리에서 체크박스를 켜고 새로고침하는 왕복을 없애기 위한 것이다.
   *
   * 덮어쓰는 곳은 `resolve()` 하나다. 즉 **버튼 표시 여부만** 바뀐다.
   * `findExact()` 는 일부러 손대지 않는다 — 라우터 가드가 그것으로 열람을
   * 막는데, 체험 중에 열람을 끄면 보고 있던 화면에서 스스로 튕겨 나간다.
   *
   * 서버 판단에는 아무 영향이 없다. 화면이 보여 주는 모습만 바뀐다.
   */
  const simulation = ref<null | { path: string; permission: MenuPermission }>(
    null,
  );

  /** 체험 모드가 켜져 있는지. 화면 상단 경고 띠를 띄우는 데 쓴다. */
  const isSimulating = computed(() => simulation.value !== null);

  /** 체험 모드를 켠다(이미 켜져 있으면 값만 갈아 끼운다). */
  function startSimulation(path: string, permission: MenuPermission) {
    simulation.value = { path: normalize(path), permission: { ...permission } };
  }

  /** 체험 모드를 끈다. 실제 권한으로 즉시 돌아간다. */
  function stopSimulation() {
    simulation.value = null;
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

    // 체험 모드는 지정한 그 경로만 덮어쓴다. 다른 화면은 실제 권한 그대로다.
    const sim = simulation.value;
    if (sim && sim.path === target) return sim.permission;

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
    simulation.value = null;
  }

  const isLoaded = computed(() => loaded.value);

  /**
   * 이 사용자에게 권한 정보가 하나라도 있는지.
   *
   * 역할이 하나도 배정되지 않은 계정은 목록이 비어서 온다.
   *
   * **이 값으로 권한을 판단하지 않는다.** 예전에는 "정보가 아예 없으면 막지 않는다" 로
   * 썼는데, 그러면 권한을 하나도 주지 않은 계정이 오히려 전부 열리는 셈이라
   * 방향이 거꾸로였다. 지금은 비어 있으면 그대로 '권한 없음' 이다
   * (`resolve()` 가 `EMPTY_PERMISSION` 을 준다).
   *
   * 남겨 둔 이유는 하나다 — 권한 예시 화면(`/system/perm-sample`)이
   * "이 계정은 역할이 없어서 아무 것도 못 한다" 를 사람에게 알려 주는 데 쓴다.
   */
  const hasAnyData = computed(() => Object.keys(byPath.value).length > 0);

  return {
    $reset,
    byPath,
    findExact,
    hasAnyData,
    isLoaded,
    isSimulating,
    load,
    loading,
    resolve,
    startSimulation,
    stopSimulation,
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
