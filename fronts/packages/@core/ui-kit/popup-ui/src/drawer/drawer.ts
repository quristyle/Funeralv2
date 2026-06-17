import type { Component, Ref } from 'vue';

import type { ClassType, MaybePromise } from '@vben-core/typings';

import type { DrawerApi } from './drawer-api';

export type DrawerPlacement = 'bottom' | 'left' | 'right' | 'top';

export type CloseIconPlacement = 'left' | 'right';

export interface DrawerProps {
  /**
   * 내용 영역에 마운트할지 여부
   * @default false
   */
  appendToMain?: boolean;
  /**
   * 취소 버튼 텍스트
   */
  cancelText?: string;
  class?: ClassType;
  /**
   * 닫기 버튼 표시 여부
   * @default true
   */
  closable?: boolean;
  /**
   * 닫기 버튼 위치
   */
  closeIconPlacement?: CloseIconPlacement;
  /**
   * 팝업 마스크 클릭 시 팝업 닫기 여부
   * @default true
   */
  closeOnClickModal?: boolean;
  /**
   * ESC 키를 눌렀을 때 팝업 닫기 여부
   * @default true
   */
  closeOnPressEscape?: boolean;
  /**
   * 확인 버튼 로딩
   * @default false
   */
  confirmLoading?: boolean;
  /**
   * 확인 버튼 텍스트
   */
  confirmText?: string;
  contentClass?: string;
  /**
   * 팝업 설명
   */
  description?: string;
  /**
   * 닫을 때 드로어 파괴(제거)
   */
  destroyOnClose?: boolean;
  /**
   * 푸터 표시 여부
   * @default true
   */
  footer?: boolean;
  /**
   * 팝업 푸터 스타일
   */
  footerClass?: ClassType;
  /**
   * 헤더 표시 여부
   * @default true
   */
  header?: boolean;
  /**
   * 팝업 헤더 스타일
   */
  headerClass?: ClassType;
  /**
   * 팝업 로딩 여부
   * @default false
   */
  loading?: boolean;
  /**
   * 마스크 표시 여부
   * @default true
   */
  modal?: boolean;

  /**
   * 자동 포커스 여부
   */
  openAutoFocus?: boolean;
  /**
   * 팝업 마스크 블러 효과
   */
  overlayBlur?: number;
  /**
   * 드로어 위치
   * @default right
   */
  placement?: DrawerPlacement;

  /**
   * 취소 버튼 표시 여부
   * @default true
   */
  showCancelButton?: boolean;
  /**
   * 확인 버튼 표시 여부
   * @default true
   */
  showConfirmButton?: boolean;
  /**
   * 제출 중 (드로어 상태 잠금)
   */
  submitting?: boolean;
  /**
   * 팝업 제목
   */
  title?: string;
  /**
   * 팝업 제목 툴팁
   */
  titleTooltip?: string;
  /**
   * 드로어 Z-인덱스
   */
  zIndex?: number;
}

export interface DrawerState extends DrawerProps {
  /** 팝업 열림 상태 */
  isOpen?: boolean;
  /**
   * 공유 데이터
   */
  sharedData?: Record<string, any>;
}

export type ExtendedDrawerApi = DrawerApi & {
  useStore: <T = NoInfer<DrawerState>>(
    selector?: (state: NoInfer<DrawerState>) => T,
  ) => Readonly<Ref<T>>;
};

export interface DrawerApiOptions extends DrawerState {
  /**
   * 독립적인 드로어 컴포넌트
   */
  connectedComponent?: Component;
  /**
   * 닫기 전 콜백, false를 반환하면 닫기가 방지됩니다.
   * @returns
   */
  onBeforeClose?: () => MaybePromise<boolean | undefined>;
  /**
   * 취소 버튼 클릭 시 콜백
   */
  onCancel?: () => void;
  /**
   * 팝업 닫기 애니메이션 종료 시 콜백
   * @returns
   */
  onClosed?: () => void;
  /**
   * 확인 버튼 클릭 시 콜백
   */
  onConfirm?: () => void;
  /**
   * 팝업 상태 변경 콜백
   * @param isOpen
   * @returns
   */
  onOpenChange?: (isOpen: boolean) => void;
  /**
   * 팝업 열기 애니메이션 종료 시 콜백
   * @returns
   */
  onOpened?: () => void;
}
