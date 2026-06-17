import type { SubMenuProvider } from '../types';

import { computed, getCurrentInstance } from 'vue';

import { findComponentUpward } from '../utils';

function useMenu() {
  const instance = getCurrentInstance();
  if (!instance) {
    throw new Error('instance is required');
  }

  /**
   * @ko_KR 모든 부모 메뉴 경로 가져오기
   */
  const parentPaths = computed(() => {
    let parent = instance.parent;
    const paths: string[] = [instance.props.path as string];
    while (parent?.type.name !== 'Menu') {
      if (parent?.props.path) {
        paths.unshift(parent.props.path as string);
      }
      parent = parent?.parent ?? null;
    }

    return paths;
  });

  const parentMenu = computed(() => {
    return findComponentUpward(instance, ['Menu', 'SubMenu']);
  });

  return {
    parentMenu,
    parentPaths,
  };
}

function useMenuStyle(menu?: SubMenuProvider) {
  const subMenuStyle = computed(() => {
    return {
      '--menu-level': menu ? (menu?.level ?? 0 + 1) : 0,
    };
  });
  return subMenuStyle;
}

export { useMenu, useMenuStyle };
