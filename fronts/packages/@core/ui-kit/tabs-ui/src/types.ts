import type { IContextMenuItem } from '@vben-core/shadcn-ui';
import type { TabDefinition, TabsStyleType } from '@vben-core/typings';

export type TabsEmits = {
  close: [string];
  sortTabs: [number, number];
  unpin: [TabDefinition];
};

export interface TabsProps {
  active?: string;
  /**
   * @ko_KR 콘텐츠 클래스
   * @default tabs-chrome
   */
  contentClass?: string;
  /**
   * @ko_KR 우클릭 메뉴
   */
  contextMenus?: (data: any) => IContextMenuItem[];
  /**
   * @ko_KR 드래그 가능 여부
   */
  draggable?: boolean;
  /**
   * @ko_KR 간격
   * @default 7
   * tabs-chrome 전용
   */
  gap?: number;
  /**
   * @ko_KR 탭 최대 너비
   * tabs-chrome 전용
   */
  maxWidth?: number;
  /**
   * @ko_KR 마우스 가운데 버튼 클릭 시 탭 닫기
   */
  middleClickToClose?: boolean;

  /**
   * @ko_KR 탭 최소 너비
   * tabs-chrome 전용
   */
  minWidth?: number;

  /**
   * @ko_KR 아이콘 표시 여부
   */
  showIcon?: boolean;
  /**
   * @ko_KR 탭 스타일
   */
  styleType?: TabsStyleType;

  /**
   * @ko_KR 탭 데이터
   */
  tabs?: TabDefinition[];

  /**
   * @ko_KR 휠 이벤트 응답 여부
   */
  wheelable?: boolean;
}

export interface TabConfig extends TabDefinition {
  affixTab: boolean;
  closable: boolean;
  icon: string;
  key: string;
  title: string;
}
