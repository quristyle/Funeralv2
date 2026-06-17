import type { Component } from 'vue';

interface VbenDropdownMenuItem {
  disabled?: boolean;
  /**
   * 클릭 이벤트 처리
   * @param data
   */
  handler?: (data: any) => void;
  /**
   * 아이콘
   */
  icon?: Component;
  /**
   * 제목
   */
  label: string;
  /**
   * 구분선 여부
   */
  separator?: boolean;
  /**
   * 고유 식별자
   */
  value: string;
}

interface DropdownMenuProps {
  menus: VbenDropdownMenuItem[];
}

export type { DropdownMenuProps, VbenDropdownMenuItem };
