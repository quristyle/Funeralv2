import type { Component, VNode, VNodeArrayChildren } from 'vue';

import type { Recordable } from '@vben-core/typings';

import { createContext } from '@vben-core/shadcn-ui';

export type IconType = 'error' | 'info' | 'question' | 'success' | 'warning';

export type BeforeCloseScope = {
  isConfirm: boolean;
};

export type AlertProps = {
  /** 닫기 전 콜백, false를 반환하면 닫기가 중단됩니다. */
  beforeClose?: (
    scope: BeforeCloseScope,
  ) => boolean | Promise<boolean | undefined> | undefined;
  /** 테두리 */
  bordered?: boolean;
  /**
   * 버튼 정렬 방식
   * @default 'end'
   */
  buttonAlign?: 'center' | 'end' | 'start';
  /** 취소 버튼 텍스트 */
  cancelText?: string;
  /** 중앙 표시 여부 */
  centered?: boolean;
  /** 확인 버튼 텍스트 */
  confirmText?: string;
  /** 팝업 컨테이너 추가 스타일 */
  containerClass?: string;
  /** 팝업 메시지 내용 */
  content: Component | string;
  /** 팝업 내용 추가 스타일 */
  contentClass?: string;
  /** beforeClose 콜백 실행 중 내용 영역에 로딩 마스크 표시 */
  contentMasking?: boolean;
  /** 팝업 하단 내용 (버튼과 같은 컨테이너) */
  footer?: Component | string;
  /** 팝업 아이콘 (제목 앞) */
  icon?: Component | IconType;
  /**
   * 팝업 마스크 블러 효과
   */
  overlayBlur?: number;
  /** 취소 버튼 표시 여부 */
  showCancel?: boolean;
  /** 팝업 제목 */
  title?: string;
};

/** Prompt 속성 */
export type PromptProps<T = any> = {
  /** 닫기 전 콜백, false를 반환하면 닫기가 중단됩니다. */
  beforeClose?: (scope: {
    isConfirm: boolean;
    value: T | undefined;
  }) => boolean | Promise<boolean | undefined> | undefined;
  /** 사용자 입력을 받기 위한 컴포넌트 */
  component?: Component;
  /** 입력 컴포넌트 속성 */
  componentProps?: Recordable<any>;
  /** 입력 컴포넌트 슬롯 */
  componentSlots?:
    | (() => any)
    | Recordable<unknown>
    | VNode
    | VNodeArrayChildren;
  /** 기본값 */
  defaultValue?: T;
  /** 입력 컴포넌트의 값 속성명 */
  modelPropName?: string;
} & Omit<AlertProps, 'beforeClose'>;

/**
 * Alert 컨텍스트
 */
export type AlertContext = {
  /** 취소 작업 실행 */
  doCancel: () => void;
  /** 확인 작업 실행 */
  doConfirm: () => void;
};

export const [injectAlertContext, provideAlertContext] =
  createContext<AlertContext>('VbenAlertContext');

/**
 * Alert 컨텍스트 가져오기
 * @returns AlertContext
 */
export function useAlertContext() {
  const context = injectAlertContext();
  if (!context) {
    throw new Error('useAlertContext must be used within an AlertProvider');
  }
  return context;
}
