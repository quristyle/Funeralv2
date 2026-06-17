import type { Component } from 'vue';

interface IContextMenuItem {
  /**
   * @ko_KR 비활성화 여부
   */
  disabled?: boolean;
  /**
   * @ko_KR 클릭 이벤트 처리
   * @param data
   */
  handler?: (data: any) => void;
  /**
   * @ko_KR 숨김 여부
   */
  hidden?: boolean;
  /**
   * @ko_KR 아이콘
   */
  icon?: Component;
  /**
   * @ko_KR 아이콘 표시 여부
   */
  inset?: boolean;
  /**
   * @ko_KR 고유 식별자
   */
  key: string;
  /**
   * @ko_KR 구분선 여부
   */
  separator?: boolean;
  /**
   * @ko_KR 단축키
   */
  shortcut?: string;
  /**
   * @ko_KR 제목
   */
  text: string;
}
export type { IContextMenuItem };
