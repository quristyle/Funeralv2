import type { ModalApiOptions, ModalState } from './modal';

import { Store } from '@vben-core/shared/store';
import { bindMethods, isFunction } from '@vben-core/shared/utils';

export class ModalApi<TData = unknown> {
  // 共享数据
  public sharedData: Record<'payload', TData | undefined> = {
    payload: undefined,
  };
  public store: Store<ModalState>;

  private api: Pick<
    ModalApiOptions,
    | 'onBeforeClose'
    | 'onCancel'
    | 'onClosed'
    | 'onConfirm'
    | 'onOpenChange'
    | 'onOpened'
  >;

  // private prevState!: ModalState;
  private state!: ModalState;

  constructor(options: ModalApiOptions = {}) {
    const {
      connectedComponent: _,
      onBeforeClose,
      onCancel,
      onClosed,
      onConfirm,
      onOpenChange,
      onOpened,
      ...storeState
    } = options;

    const defaultState: ModalState = {
      bordered: true,
      centered: false,
      class: '',
      closeOnClickModal: true,
      closeOnPressEscape: true,
      confirmDisabled: false,
      confirmLoading: false,
      contentClass: '',
      destroyOnClose: true,
      // [준수사항 3] 팝업은 헤더를 잡고 옮길 수 있어야 한다.
      // 부품이 이미 드래그를 지원하므로 기본값만 켜 두면 모든 vben 모달에 걸린다.
      // 전체화면일 때와 헤더가 없을 때는 modal.vue 의 shouldDraggable 이 알아서 끈다.
      draggable: true,
      footer: true,
      footerClass: '',
      fullscreen: false,
      fullscreenButton: true,
      header: true,
      headerClass: '',
      isOpen: false,
      loading: false,
      modal: true,
      openAutoFocus: false,
      showCancelButton: true,
      showConfirmButton: true,
      title: '',
      animationType: 'slide',
    };

    this.store = new Store<ModalState>({
      ...defaultState,
      ...storeState,
    });

    this.store.subscribe((state) => {
      // 상태가 업데이트될 때마다 onOpenChange 콜백 함수가 호출됩니다.
      const prevIsOpen = this.state?.isOpen;
      this.state = state;
      if (state?.isOpen !== prevIsOpen) {
        this.api.onOpenChange?.(!!state?.isOpen);
      }
    });

    this.state = this.store.state;

    this.api = {
      onBeforeClose,
      onCancel,
      onClosed,
      onConfirm,
      onOpenChange,
      onOpened,
    };
    bindMethods(this);
  }

  /**
   * 팝업 닫기
   * @description 팝업을 닫을 때 onBeforeClose 훅 함수가 호출됩니다. onBeforeClose가 false를 반환하면 팝업이 닫히지 않습니다.
   */
  async close() {
    // onBeforeClose 훅 함수를 통해 팝업 닫기 허용 여부를 판단합니다.
    // onBeforeClose가 false를 반환하면 팝업이 닫히지 않습니다.
    const allowClose = (await this.api.onBeforeClose?.()) ?? true;
    if (allowClose) {
      this.store.setState((prev) => ({
        ...prev,
        isOpen: false,
      }));
    }
  }

  getData(): TData | undefined {
    return this.sharedData.payload;
  }

  /**
   * 팝업 상태 잠금 (제출 과정 중 대기 상태에 사용)
   * @description 잠금 상태에서는 기본 취소 버튼이 비활성화되고, 팝업 내용이 스피너로 덮이며, 닫기 버튼이 숨겨집니다. 또한 수동으로 팝업을 닫는 것을 방지하고, 기본 제출 버튼을 로딩 상태로 표시합니다.
   * @param isLocked 잠금 여부
   */
  lock(isLocked = true) {
    return this.setState({ submitting: isLocked });
  }

  /**
   * 취소 작업
   */
  onCancel() {
    if (this.api.onCancel) {
      this.api.onCancel?.();
    } else {
      this.close();
    }
  }

  /**
   * 팝업 닫기 애니메이션 종료 후 콜백
   */
  onClosed() {
    if (!this.state.isOpen) {
      this.api.onClosed?.();
    }
  }

  /**
   * 확인 작업
   */
  onConfirm() {
    this.api.onConfirm?.();
  }

  /**
   * 팝업 열기 애니메이션 종료 후 콜백
   */
  onOpened() {
    if (this.state.isOpen) {
      this.api.onOpened?.();
    }
  }

  open() {
    this.store.setState((prev) => ({
      ...prev,
      isOpen: true,
      submitting: false,
    }));
  }

  setData(payload: TData) {
    this.sharedData.payload = payload;
    return this;
  }

  setState(
    stateOrFn:
      | ((prev: ModalState) => Partial<ModalState>)
      | Partial<ModalState>,
  ) {
    if (isFunction(stateOrFn)) {
      this.store.setState(stateOrFn);
    } else {
      this.store.setState((prev) => ({ ...prev, ...stateOrFn }));
    }
    return this;
  }

  /**
   * 팝업 잠금 상태 해제
   * @description lock 메서드로 설정된 잠금 상태를 해제합니다. lock(false)의 별칭입니다.
   */
  unlock() {
    return this.lock(false);
  }
}
