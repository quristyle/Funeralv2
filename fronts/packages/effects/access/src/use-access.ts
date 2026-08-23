import { computed } from 'vue';

import { preferences, updatePreferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';

/**
 * 스토어 값이 배열이 아닐 때를 막는다.
 *
 * 로그아웃은 `resetAllStores()` 로 모든 스토어를 되돌리는데, 되돌리는 도중에
 * `userRoles`·`accessCodes` 가 배열이 아닌 값으로 잠깐 보일 수 있다.
 * 그 상태로 `new Set(값)` 을 부르면 "object is not iterable" 로 화면이 죽는다.
 */
function toSet(value: unknown) {
  return new Set(Array.isArray(value) ? value : []);
}

function useAccess() {
  const accessStore = useAccessStore();
  const userStore = useUserStore();
  const accessMode = computed(() => {
    return preferences.app.accessMode;
  });

  /**
   * 역할을 기반으로 권한 여부 판단
   * @description: 사용자 역할을 기반으로 권한이 있는지 확인합니다.
   * @param roles
   */
  function hasAccessByRoles(roles: string[]) {
    const userRoleSet = toSet(userStore.userRoles);
    const intersection = roles.filter((item) => userRoleSet.has(item));
    return intersection.length > 0;
  }

  /**
   * 권한 코드를 기반으로 권한 여부 판단
   * @description: 사용자 권한 코드를 기반으로 권한이 있는지 확인합니다.
   * @param codes
   */
  function hasAccessByCodes(codes: string[]) {
    const userCodesSet = toSet(accessStore.accessCodes);

    const intersection = codes.filter((item) => userCodesSet.has(item));
    return intersection.length > 0;
  }

  async function toggleAccessMode() {
    updatePreferences({
      app: {
        accessMode:
          preferences.app.accessMode === 'frontend' ? 'backend' : 'frontend',
      },
    });
  }

  return {
    accessMode,
    hasAccessByCodes,
    hasAccessByRoles,
    toggleAccessMode,
  };
}

export { useAccess };
