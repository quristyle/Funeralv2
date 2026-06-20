import { defineComponent, h } from 'vue';
import { useVbenModal, VbenModal } from '@vben/common-ui';
import type { ModalApiOptions, ModalProps } from '@vben/common-ui';

/**
 * Funeralv2 프로젝트 공통 BaseModal 훅
 * @param options 모달 설정 옵션
 */
export function useBaseModal(options: ModalApiOptions = {}) {
  // 프로젝트 전역의 기본 속성 일괄 지정
  const defaultOptions: ModalApiOptions = {
    draggable: true,         // 드래그 가능 여부 기본 활성화
    centered: true,          // 중앙 정렬 기본 활성화
    confirmText: '확인',     // 한국어 기본값 적용
    cancelText: '취소',      // 한국어 기본값 적용
    closable: true,          // 닫기 버튼 표시 기본 활성화
    ...options,
  };

  // Vben 내부의 상태 보관용 modalApi 인스턴스 생성
  const [_, modalApi] = useVbenModal(defaultOptions);

  // VbenModal을 직접 렌더링하도록 래핑하여 디자인/버튼 소실 문제를 완전히 해결합니다.
  const BaseModal = defineComponent(
    (props: ModalProps, { attrs, slots }) => {
      return () =>
        h(
          VbenModal,
          {
            ...props,
            ...attrs,
            // 프로젝트 공통 디자인 지정을 위한 CSS 클래스 주입
            class: [
              'funeral-base-modal', 
              attrs.class as string || '', 
              props.class as string || ''
            ].join(' ').trim(),
            modalApi: modalApi,
          },
          slots,
        );
    },
    {
      name: 'BaseModal',
      inheritAttrs: false,
    }
  );

  return [BaseModal, modalApi] as const;
}

export type { ModalApiOptions, ModalProps };
