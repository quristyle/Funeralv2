import type { Component, Ref } from 'vue';

import type {
  MenuRecordBadgeRaw,
  Recordable,
  ThemeModeType,
} from '@vben-core/typings';

interface MenuProps {
  /**
   * @ko_KR 아코디언 모드 활성화 여부
   * @default true
   */
  accordion?: boolean;
  /**
   * @ko_KR 메뉴 접힘 여부
   * @default false
   */
  collapse?: boolean;

  /**
   * @ko_KR 메뉴 접힘 시 메뉴 이름 표시 여부
   * @default false
   */
  collapseShowTitle?: boolean;

  /**
   * @ko_KR 기본 활성화된 메뉴
   */
  defaultActive?: string;

  /**
   * @ko_KR 기본 확장된 메뉴
   */
  defaultOpeneds?: string[];

  /**
   * @ko_KR 메뉴 모드
   * @default vertical
   */
  mode?: 'horizontal' | 'vertical';

  /**
   * @ko_KR 라운드 스타일 여부
   * @default true
   */
  rounded?: boolean;

  /**
   * @ko_KR 활성화된 메뉴 항목으로 자동 스크롤 여부
   * @default false
   */
  scrollToActive?: boolean;

  /**
   * @ko_KR 메뉴 테마
   * @default dark
   */
  theme?: ThemeModeType;
}

interface SubMenuProps extends MenuRecordBadgeRaw {
  /**
   * @ko_KR 활성화 아이콘
   */
  activeIcon?: string;
  /**
   * @ko_KR 비활성화 여부
   */
  disabled?: boolean;
  /**
   * @ko_KR 아이콘
   */
  icon?: Component | string;
  /**
   * @ko_KR submenu 이름
   */
  path: string;
}

interface MenuItemProps extends MenuRecordBadgeRaw {
  /**
   * @ko_KR 아이콘
   */
  activeIcon?: string;
  /**
   * @ko_KR 비활성화 여부
   */
  disabled?: boolean;
  /**
   * @ko_KR 아이콘
   */
  icon?: Component | string;
  /**
   * @ko_KR menuitem 이름
   */
  path: string;
  /**
   * @ko_KR 메뉴에 전달되는 파라미터
   */
  query?: Recordable<any>;
}

interface MenuItemRegistered {
  active: boolean;
  parentPaths: string[];
  path: string;
  query?: Recordable<any>;
}

interface MenuItemClicked {
  parentPaths: string[];
  path: string;
}

interface MenuProvider {
  activePath?: string;
  addMenuItem: (item: MenuItemRegistered) => void;

  addSubMenu: (item: MenuItemRegistered) => void;
  closeMenu: (path: string, parentLinks: string[]) => void;
  handleMenuItemClick: (item: MenuItemClicked) => void;
  handleSubMenuClick: (subMenu: MenuItemRegistered) => void;
  isMenuPopup: boolean;
  items: Record<string, MenuItemRegistered>;

  openedMenus: string[];
  openMenu: (path: string, parentLinks: string[]) => void;
  props: MenuProps;
  removeMenuItem: (item: MenuItemRegistered) => void;

  removeSubMenu: (item: MenuItemRegistered) => void;

  subMenus: Record<string, MenuItemRegistered>;
  theme: string;
}

interface SubMenuProvider {
  addSubMenu: (item: MenuItemRegistered) => void;
  handleMouseleave?: (deepDispatch: boolean) => void;
  level: number;
  mouseInChild: Ref<boolean>;
  removeSubMenu: (item: MenuItemRegistered) => void;
}

export type {
  MenuItemClicked,
  MenuItemProps,
  MenuItemRegistered,
  MenuProps,
  MenuProvider,
  SubMenuProps,
  SubMenuProvider,
};
