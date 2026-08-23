import type {
  VxeGridListeners,
  VxeGridPropTypes,
  VxeGridProps as VxeTableGridProps,
  VxeUIExport,
} from 'vxe-table';

import type { Ref } from 'vue';

import type { ClassType, DeepPartial } from '@vben/types';

import type {
  BaseFormComponentType,
  FormValues,
  VbenFormProps,
} from '@vben-core/form-ui';

import type { VxeGridApi } from './api';
import type { ViewedRowOptions } from './viewed-row';

import { useVbenForm } from '@vben-core/form-ui';

export interface VxePaginationInfo {
  currentPage: number;
  pageSize: number;
  total: number;
}

interface ToolbarConfigOptions extends VxeGridPropTypes.ToolbarConfig {
  /** 검색 폼 전환 버튼 표시 여부 */
  search?: boolean;
}

export type VxeTableGridColumns<T = any> = VxeTableGridOptions<T>['columns'];

export interface VxeTableGridOptions<T = any> extends VxeTableGridProps<T> {
  /** 툴바 설정 */
  toolbarConfig?: ToolbarConfigOptions;
}

export interface SeparatorOptions {
  show?: boolean;
  backgroundColor?: string;
}

export interface VxeGridProps<
  T extends Record<string, any> = any,
  D extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TFormValues extends FormValues = FormValues,
  TSubmitValues extends FormValues = TFormValues,
> {
  /**
   * 데이터
   */
  tableData?: any[];
  /**
   * 제목
   */
  tableTitle?: string;
  /**
   * 제목 도움말
   */
  tableTitleHelp?: string;
  /**
   * 컴포넌트 클래스
   */
  class?: ClassType;
  /**
   * vxe-grid 클래스
   */
  gridClass?: ClassType;
  /**
   * vxe-grid 설정
   */
  gridOptions?: DeepPartial<VxeTableGridOptions<T>>;
  /**
   * vxe-grid 이벤트
   */
  gridEvents?: DeepPartial<VxeGridListeners<T>>;
  /**
   * 폼 설정
   */
  formOptions?: VbenFormProps<D, P, TFormValues, TSubmitValues>;
  /**
   * 검색 폼 표시
   */
  showSearchForm?: boolean;
  /**
   * 검색 폼과 테이블 본문 사이의 구분선
   */
  separator?: boolean | SeparatorOptions;
  /**
   * 已读行功能
   */
  viewedRowOptions?: boolean | ViewedRowOptions<T>;
}

export type ExtendedVxeGridApi<
  D extends Record<string, any> = any,
  F extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TFormValues extends FormValues = FormValues,
  TSubmitValues extends FormValues = TFormValues,
> = VxeGridApi<D, F, P, TFormValues, TSubmitValues> & {
  useStore: <S = NoInfer<VxeGridProps<D, F, P, TFormValues, TSubmitValues>>>(
    selector?: (
      state: NoInfer<VxeGridProps<D, F, P, TFormValues, TSubmitValues>>,
    ) => S,
  ) => Readonly<Ref<S>>;
};

export interface SetupVxeTable {
  configVxeTable: (ui: VxeUIExport) => void;
  useVbenForm?: typeof useVbenForm;
}
