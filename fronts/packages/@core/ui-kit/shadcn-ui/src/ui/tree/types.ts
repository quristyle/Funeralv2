import type { Arrayable } from '@vueuse/core';
import type { FlattenedItem } from 'reka-ui';

import type { Recordable } from '@vben-core/typings';

export interface TreeProps {
  /** 단일 선택 시 기존 옵션 취소 허용 */
  allowClear?: boolean;
  /** 비연관 선택 시 상위 노드 자동 선택 */
  autoCheckParent?: boolean;
  /** 테두리 표시 */
  bordered?: boolean;
  /** 부모-자식 연관 선택 취소 */
  checkStrictly?: boolean;
  /** 자식 필드명 */
  childrenField?: string;
  /** 기본 펼침 키 */
  defaultExpandedKeys?: Array<number | string>;
  /** 기본 펼침 단계 (defaultExpandedKeys보다 우선순위 높음) */
  defaultExpandedLevel?: number;
  /** 기본값 */
  defaultValue?: Arrayable<number | string>;
  /** 비활성화 */
  disabled?: boolean;
  /** 비활성화 필드명 */
  disabledField?: string;
  /** 사용자 정의 노드 클래스명 */
  getNodeClass?: (item: FlattenedItem<Recordable<any>>) => string;
  iconField?: string;
  /** label 필드명 */
  labelField?: string;
  /** 다중 선택 여부 */
  multiple?: boolean;
  /** iconField로 지정된 아이콘 표시 */
  showIcon?: boolean;
  /** 펼치기/접기 애니메이션 활성화 */
  transition?: boolean;
  /** 트리 데이터 */
  treeData: Recordable<any>[];
  /** 값 필드명 */
  valueField?: string;
}

export function treePropsDefaults() {
  return {
    allowClear: false,
    autoCheckParent: true,
    bordered: false,
    checkStrictly: false,
    defaultExpandedKeys: () => [],
    defaultExpandedLevel: 0,
    disabled: false,
    disabledField: 'disabled',
    iconField: 'icon',
    labelField: 'label',
    multiple: false,
    showIcon: true,
    transition: true,
    valueField: 'value',
    childrenField: 'children',
  };
}
