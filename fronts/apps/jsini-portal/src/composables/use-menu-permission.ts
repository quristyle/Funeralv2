import type { MenuPermission } from '#/api/core/menu';

import { computed, onMounted } from 'vue';
import { useRoute } from 'vue-router';

import { useMenuPermissionStore } from '#/store/menu-permission';

/** 권한 정보가 아직 없을 때 쓰는 값. 잠그지 않는다. */
const ALLOW_ALL: MenuPermission = {
  canCreate: true,
  canCust1: true,
  canCust2: true,
  canCust3: true,
  canCust4: true,
  canCust5: true,
  canCust6: true,
  canCust7: true,
  canCust8: true,
  canDelete: true,
  canExcel: true,
  canPrint: true,
  canSearch: true,
  canUpdate: true,
  canView: true,
  menuId: '',
  path: '',
};

/**
 * [현재 화면의 권한]
 *
 * JSini 포털은 여러 MSA 를 한 화면 안에 담는다. 권한은 포털 한 곳
 * (`scom.roles` / `scom.role_menus`)에서만 관리하고, 장례식장·헬프데스크 등
 * 모든 화면이 그 결과를 따른다. 각 시스템이 자기 권한 규칙을 따로 두지 않는다.
 *
 * 쓰는 법:
 * ```ts
 * const perm = useMenuPermission();
 * ```
 * ```html
 * <Button v-if="perm.canCreate" @click="onCreate">등록</Button>
 * <Button :disabled="!perm.canUpdate" @click="onSave">저장</Button>
 * ```
 *
 * 경로로 자기 권한을 찾는다. 상세 화면처럼 메뉴에 등록되지 않은 하위 경로는
 * 가장 구체적으로 겹치는 상위 메뉴의 권한을 물려받는다.
 *
 * @param path 권한을 찾을 경로. 비우면 현재 라우트를 쓴다.
 *             다른 화면의 권한을 봐야 할 때만 넘긴다.
 */
export function useMenuPermission(path?: string) {
  const route = useRoute();
  const store = useMenuPermissionStore();

  // 새로고침으로 화면에 바로 들어온 경우를 대비해 여기서도 한 번 확인한다.
  // 이미 받아둔 상태면 아무 일도 하지 않는다.
  onMounted(() => {
    store.load();
  });

  const permission = computed<MenuPermission>(() => {
    // 권한 정보를 아직 못 받았거나 역할이 하나도 없는 계정은 막지 않는다.
    // 열람 가드·v-perm·can() 과 같은 규칙이다.
    if (!store.isLoaded || !store.hasAnyData) return ALLOW_ALL;
    return store.resolve(path ?? route.path);
  });

  return {
    /** 등록 */
    canCreate: computed(() => permission.value.canCreate),
    /** 사용자 정의 1~8. 이름은 메뉴 관리 화면에서 붙인다. */
    canCust1: computed(() => permission.value.canCust1),
    canCust2: computed(() => permission.value.canCust2),
    canCust3: computed(() => permission.value.canCust3),
    canCust4: computed(() => permission.value.canCust4),
    canCust5: computed(() => permission.value.canCust5),
    canCust6: computed(() => permission.value.canCust6),
    canCust7: computed(() => permission.value.canCust7),
    canCust8: computed(() => permission.value.canCust8),
    /** 삭제 */
    canDelete: computed(() => permission.value.canDelete),
    /** 엑셀 내려받기 */
    canExcel: computed(() => permission.value.canExcel),
    /** 인쇄·출력 */
    canPrint: computed(() => permission.value.canPrint),
    /** 조회(검색 실행) */
    canSearch: computed(() => permission.value.canSearch),
    /** 수정 */
    canUpdate: computed(() => permission.value.canUpdate),
    /** 열람 */
    canView: computed(() => permission.value.canView),
    /** 권한 목록을 아직 받지 못했는지. 받기 전에는 버튼을 잠가 두는 데 쓴다. */
    isLoading: computed(() => !store.isLoaded),
    /** 원본 값이 필요할 때 */
    permission,
  };
}
