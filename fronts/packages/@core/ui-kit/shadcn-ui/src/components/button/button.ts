import type { AsTag } from 'reka-ui';

import type { Component } from 'vue';

import type { ButtonVariants, ButtonVariantSize } from '../../ui';

export interface VbenButtonProps {
  /**
   * The element or component this component should render as. Can be overwrite by `asChild`
   * @defaultValue "div"
   */
  as?: AsTag | Component;
  /**
   * Change the default rendered element for the one passed as a child, merging their props and behavior.
   *
   * Read our [Composition](https://www.reka-ui.com/docs/guides/composition) guide for more details.
   */
  asChild?: boolean;
  class?: any;
  disabled?: boolean;
  loading?: boolean;
  size?: ButtonVariantSize;
  variant?: ButtonVariants;
}

export type CustomRenderType = (() => Component | string) | string;

export type ValueType = boolean | number | string;

export interface VbenButtonGroupProps extends Pick<
  VbenButtonProps,
  'disabled'
> {
  /** 단일 선택 모드에서 선택 해제 허용 */
  allowClear?: boolean;
  /** 값 변경 전 콜백 */
  beforeChange?: (
    value: ValueType,
    isChecked: boolean,
  ) => boolean | PromiseLike<boolean | undefined> | undefined;
  /** 버튼 스타일 */
  btnClass?: any;
  /** 버튼 간격 */
  gap?: number;
  /** 다중 선택 모드에서 최대 선택 수 제한. 0은 제한 없음을 의미 */
  maxCount?: number;
  /** 다중 선택 허용 여부 */
  multiple?: boolean;
  /** 옵션 */
  options?: { [key: string]: any; label: CustomRenderType; value: ValueType }[];
  /** 아이콘 표시 */
  showIcon?: boolean;
  /** 크기 */
  size?: 'large' | 'middle' | 'small';
}
