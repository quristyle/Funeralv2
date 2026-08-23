import type { DrawerApiOptions, DrawerState } from './drawer';

import { Store } from '@vben-core/shared/store';
import { bindMethods, isFunction } from '@vben-core/shared/utils';

export class DrawerApi<TData = unknown> {
  // 共享数据
  public sharedData: Record<'payload', TData | undefined> = {
    payload: undefined,
  };
  public store: Store<DrawerState>;

  private api: Pick<
    DrawerApiOptions,
    | 'onBeforeClose'
    | 'onCancel'
    | 'onClosed'
    | 'onConfirm'
    | 'onOpenChange'
    | 'onOpened'
  >;

  // private prevState!: DrawerState;
  private state!: DrawerState;

  constructor(options: DrawerApiOptions = {}) {
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

    const defaultState: DrawerState = {
      class: '',
      closable: true,
      closeIconPlacement: 'right',
      closeOnClickModal: true,
      closeOnPressEscape: true,
      confirmLoading: false,
      contentClass: '',
      footer: true,
      header: true,
      isOpen: false,
      loading: false,
      modal: true,
      openAutoFocus: false,
      placement: 'right',
      showCancelButton: true,
      showConfirmButton: true,
      submitting: false,
      title: '',
    };

    this.store = new Store<DrawerState>({
      ...defaultState,
      ...storeState,
    });

    this.store.subscribe((state) => {
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
   * 드로어 닫기
   * @description 드로어를 닫을 때 onBeforeClose 훅 함수가 호출됩니다. onBeforeClose가 false를 반환하면 팝업이 닫히지 않습니다.
   */
  async close() {
    // onBeforeClose 훅 함수를 통해 팝업 닫기 허용 여부를 판단합니다.
    // onBeforeClose가 false를 반환하면 팝업이 닫히지 않습니다.
    const allowClose = (await this.api.onBeforeClose?.()) ?? true;
    if (allowClose) {
      this.store.setState((prev) => ({
        ...prev,
        isOpen: false,
        submitting: false,
      }));
    }
  }

  getData(): TData | undefined {
    return this.sharedData.payload;
  }

  /**
   * 드로어 상태 잠금 (제출 과정 중 대기 상태에 사용)
   * @description 잠금 상태에서는 기본 취소 버튼이 비활성화되고, 드로어 내용이 스피너로 덮이며, 닫기 버튼이 숨겨집니다. 또한 수동으로 팝업을 닫는 것을 방지하고, 기본 제출 버튼을 로딩 상태로 표시합니다.
   * @param isLocked 잠금 여부
   */
  lock(isLocked: boolean = true) {
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
    this.store.setState((prev) => ({ ...prev, isOpen: true }));
  }

  setData(payload: TData) {
    this.sharedData.payload = payload;
    return this;
  }

  setState(
    stateOrFn:
      | ((prev: DrawerState) => Partial<DrawerState>)
      | Partial<DrawerState>,
  ) {
    if (isFunction(stateOrFn)) {
      this.store.setState(stateOrFn);
    } else {
      this.store.setState((prev) => ({ ...prev, ...stateOrFn }));
    }
    return this;
  }

  /**
   * 드로어 잠금 상태 해제
   * @description lock 메서드로 설정된 잠금 상태를 해제합니다. lock(false)의 별칭입니다.
   */
  unlock() {
    return this.lock(false);
  }
}
