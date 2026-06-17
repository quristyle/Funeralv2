import type { MenuRecordRaw } from '@vben-core/typings';

interface NormalMenuProps {
  /**
   * 메뉴 데이터
   */
  activePath?: string;
  /**
   * 접힘 여부
   */
  collapse?: boolean;
  /**
   * 메뉴 항목
   */
  menus?: MenuRecordRaw[];
  /**
   * @ko_KR 둥근 스타일 여부
   * @default true
   */
  rounded?: boolean;
  /**
   * 테마
   */
  theme?: 'dark' | 'light';
}

export type { NormalMenuProps };
