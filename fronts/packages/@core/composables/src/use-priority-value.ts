import type { ComputedRef, Ref } from 'vue';

import { computed, getCurrentInstance, unref, useAttrs, useSlots } from 'vue';

import {
  getFirstNonNullOrUndefined,
  kebabToCamelCase,
} from '@vben-core/shared/utils';

/**
 * 슬롯, attrs, props, state 순서대로 값을 가져옵니다.
 * @param key
 * @param props
 * @param state
 */
export function usePriorityValue<
  T extends Record<string, any>,
  S extends Record<string, any>,
  K extends keyof T = keyof T,
>(key: K, props: T, state: Readonly<Ref<NoInfer<S>>> | undefined) {
  const instance = getCurrentInstance();
  const slots = useSlots();
  const attrs = useAttrs() as T;

  const value = computed((): T[K] => {
    // props는 전달 여부와 상관없이 기본값을 가지므로 순서에 영향을 줄 수 있습니다.
    // 원본 props에 값이 있는지 판단하여 전달 여부를 확인합니다.
    const rawProps = (instance?.vnode?.props || {}) as T;

    const standardRawProps = {} as T;

    for (const [key, value] of Object.entries(rawProps)) {
      standardRawProps[kebabToCamelCase(key) as K] = value;
    }
    const propsKey =
      standardRawProps?.[key] === undefined ? undefined : props[key];

    // 슬롯을 비활성화할 수 있습니다.
    return getFirstNonNullOrUndefined(
      slots[key as string],
      attrs[key],
      propsKey,
      state?.value?.[key as keyof S],
    ) as T[K];
  });

  return value;
}

/**
 * state의 값을 일괄적으로 가져옵니다. (각 값은 ref)
 * @param props
 * @param state
 */
export function usePriorityValues<
  T extends Record<string, any>,
  S extends Ref<Record<string, any>> = Readonly<Ref<NoInfer<T>, NoInfer<T>>>,
>(props: T, state: S | undefined) {
  const result: { [K in keyof T]: ComputedRef<T[K]> } = {} as never;

  (Object.keys(props) as (keyof T)[]).forEach((key) => {
    result[key] = usePriorityValue(key as keyof typeof props, props, state);
  });

  return result;
}

/**
 * state의 값을 일괄적으로 가져옵니다. (하나의 computed에 모아서 투과 전달용으로 사용)
 * @param props
 * @param state
 */
export function useForwardPriorityValues<
  T extends Record<string, any>,
  S extends Ref<Record<string, any>> = Readonly<Ref<NoInfer<T>, NoInfer<T>>>,
>(props: T, state: S | undefined) {
  const computedResult: { [K in keyof T]: ComputedRef<T[K]> } = {} as never;

  (Object.keys(props) as (keyof T)[]).forEach((key) => {
    computedResult[key] = usePriorityValue(
      key as keyof typeof props,
      props,
      state,
    );
  });

  return computed(() => {
    const unwrapResult: Record<string, any> = {};
    Object.keys(props).forEach((key) => {
      unwrapResult[key] = unref(computedResult[key]);
    });
    return unwrapResult as { [K in keyof T]: T[K] };
  });
}
