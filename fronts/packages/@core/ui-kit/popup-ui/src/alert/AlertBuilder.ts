import type { Component, VNode } from 'vue';

import type { Recordable } from '@vben-core/typings';

import type { AlertProps, BeforeCloseScope, PromptProps } from './alert';

import { h, nextTick, ref, render } from 'vue';

import { useSimpleLocale } from '@vben-core/composables';
import { Input, VbenRenderContent } from '@vben-core/shadcn-ui';
import { isFunction, isString } from '@vben-core/shared/utils';

import Alert from './alert.vue';

const alerts = ref<Array<{ container: HTMLElement; instance: Component }>>([]);

const { $t } = useSimpleLocale();

export function vbenAlert(options: AlertProps): Promise<void>;
export function vbenAlert(
  message: string,
  options?: Partial<AlertProps>,
): Promise<void>;
export function vbenAlert(
  message: string,
  title?: string,
  options?: Partial<AlertProps>,
): Promise<void>;

export function vbenAlert(
  arg0: AlertProps | string,
  arg1?: Partial<AlertProps> | string,
  arg2?: Partial<AlertProps>,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const options: AlertProps = isString(arg0)
      ? {
          content: arg0,
        }
      : { ...arg0 };
    if (arg1) {
      if (isString(arg1)) {
        options.title = arg1;
      } else if (!isString(arg1)) {
        // 두 번째 인수가 객체인 경우 옵션에 병합
        Object.assign(options, arg1);
      }
    }

    if (arg2 && !isString(arg2)) {
      Object.assign(options, arg2);
    }
    // 컨테이너 요소 생성
    const container = document.createElement('div');
    document.body.append(container);

    // 콜백에서 인스턴스에 접근하기 위한 참조 생성
    const alertRef = { container, instance: null as any };

    const props: AlertProps & Recordable<any> = {
      onClosed: (isConfirm: boolean) => {
        // 컴포넌트 인스턴스 및 생성된 모든 DOM 제거 (페이지를 열기 전 상태로 복구)
        // alerts 배열에서 해당 인스턴스 제거
        alerts.value = alerts.value.filter((item) => item !== alertRef);

        // DOM에서 컨테이너 제거
        render(null, container);
        if (container.parentNode) {
          container.remove();
        }

        // Promise를 resolve하여 사용자 작업 결과 전달
        if (isConfirm) {
          resolve();
        } else {
          reject(new Error('dialog cancelled'));
        }
      },
      ...options,
      open: true,
      title: options.title ?? $t.value('prompt'),
    };

    // Alert 컴포넌트의 VNode 생성
    const vnode = h(Alert, props);

    // 컨테이너에 컴포넌트 렌더링
    render(vnode, container);

    // 컴포넌트 인스턴스 참조 저장
    alertRef.instance = vnode.component?.proxy as Component;

    // 인스턴스와 컨테이너를 alerts 배열에 추가
    alerts.value.push(alertRef);
  });
}

export function vbenConfirm(options: AlertProps): Promise<void>;
export function vbenConfirm(
  message: string,
  options?: Partial<AlertProps>,
): Promise<void>;
export function vbenConfirm(
  message: string,
  title?: string,
  options?: Partial<AlertProps>,
): Promise<void>;

export function vbenConfirm(
  arg0: AlertProps | string,
  arg1?: Partial<AlertProps> | string,
  arg2?: Partial<AlertProps>,
): Promise<void> {
  const defaultProps: Partial<AlertProps> = {
    showCancel: true,
  };
  if (!arg1) {
    return isString(arg0)
      ? vbenAlert(arg0, defaultProps)
      : vbenAlert({ ...defaultProps, ...arg0 });
  } else if (!arg2) {
    return isString(arg1)
      ? vbenAlert(arg0 as string, arg1, defaultProps)
      : vbenAlert(arg0 as string, { ...defaultProps, ...arg1 });
  }
  return vbenAlert(arg0 as string, arg1 as string, {
    ...defaultProps,
    ...arg2,
  });
}

export async function vbenPrompt<T = any>(
  options: PromptProps<T>,
): Promise<T | undefined> {
  const {
    component: _component,
    componentProps: _componentProps,
    componentSlots,
    content,
    defaultValue,
    modelPropName: _modelPropName,
    ...delegated
  } = options;

  const modelValue = ref<T | undefined>(defaultValue);
  const inputComponentRef = ref<null | VNode>(null);
  const staticContents: Component[] = [
    h(VbenRenderContent, { content, renderBr: true }),
  ];

  const modelPropName = _modelPropName || 'modelValue';
  const componentProps = { ..._componentProps };

  // 렌더링할 때마다 다시 계산되는 내용 함수
  const contentRenderer = () => {
    const currentProps = {
      ...componentProps,
      [modelPropName]: modelValue.value,
      [`onUpdate:${modelPropName}`]: (val: T) => {
        modelValue.value = val;
      },
    };

    // 현재 값 설정

    // 업데이트 처리 함수 설정

    // 입력 컴포넌트 생성
    inputComponentRef.value = h(
      _component || Input,
      currentProps,
      componentSlots,
    );

    // 정적 내용과 입력 컴포넌트를 포함하는 배열 반환
    return h(
      'div',
      { class: 'flex flex-col gap-2' },
      { default: () => [...staticContents, inputComponentRef.value] },
    );
  };

  const props: AlertProps & Recordable<any> = {
    ...delegated,
    async beforeClose(scope: BeforeCloseScope) {
      if (delegated.beforeClose) {
        return await delegated.beforeClose({
          ...scope,
          value: modelValue.value,
        });
      }
    },
    // 함수 형태를 사용하여 렌더링할 때마다 내용 재계산
    content: contentRenderer,
    contentMasking: true,
    async onOpened() {
      await nextTick();
      const componentRef: null | VNode = inputComponentRef.value;
      if (componentRef) {
        if (
          componentRef.component?.exposed &&
          isFunction(componentRef.component.exposed.focus)
        ) {
          componentRef.component.exposed.focus();
        } else {
          if (componentRef.el) {
            if (
              isFunction(componentRef.el.focus) &&
              ['BUTTON', 'INPUT', 'SELECT', 'TEXTAREA'].includes(
                componentRef.el.tagName,
              )
            ) {
              componentRef.el.focus();
            } else if (isFunction(componentRef.el.querySelector)) {
              const focusableElement = componentRef.el.querySelector(
                'input, select, textarea, button',
              );
              if (focusableElement && isFunction(focusableElement.focus)) {
                focusableElement.focus();
              }
            } else if (
              componentRef.el.nextElementSibling &&
              isFunction(componentRef.el.nextElementSibling.focus)
            ) {
              componentRef.el.nextElementSibling.focus();
            }
          }
        }
      }
    },
  };

  await vbenConfirm(props);
  return modelValue.value;
}

export function clearAllAlerts() {
  alerts.value.forEach((alert) => {
    // DOM에서 컨테이너 제거
    render(null, alert.container);
    if (alert.container.parentNode) {
      alert.container.remove();
    }
  });
  alerts.value = [];
}

