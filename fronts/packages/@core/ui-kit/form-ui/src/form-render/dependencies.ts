import type {
  FormItemDependencies,
  FormSchemaRuleType,
  MaybeComponentProps,
} from '../types';

import { computed, ref, watch } from 'vue';

import { get, isBoolean, isFunction } from '@vben-core/shared/utils';

import { useFormValues } from 'vee-validate';

import { injectRenderFormProps } from './context';

/**
 * 중첩된 객체(Nested Objects)에 해당하는 필드 값 해석
 * @param values 폼 값
 * @param fieldName 필드명
 */
function resolveValueByFieldName(
  values: Record<string, any>,
  fieldName: string,
) {
  // vee-validate: []는 중첩 비활성화를 의미함
  if (fieldName.startsWith('[') && fieldName.endsWith(']')) {
    const rawKey = fieldName.slice(1, -1);
    return values[rawKey];
  }

  return get(values, fieldName);
}

export default function useDependencies(
  getDependencies: () => FormItemDependencies | undefined,
) {
  const values = useFormValues();

  const formRenderProps = injectRenderFormProps();
  const formApi = formRenderProps.form;

  if (!formApi) {
    throw new Error('Form api is required in useDependencies');
  }

  if (!values) {
    throw new Error('useDependencies should be used within <VbenForm>');
  }

  const isIf = ref(true);
  const isDisabled = ref(false);
  const isShow = ref(true);
  const isRequired = ref(false);
  const dynamicComponentProps = ref<MaybeComponentProps>({});
  const dynamicRules = ref<FormSchemaRuleType>();

  const triggerFieldValues = computed(() => {
    // 이 필드는 여러 필드에 의해 트리거될 수 있음
    const triggerFields = getDependencies()?.triggerFields ?? [];
    return triggerFields.map((dep) => {
      return resolveValueByFieldName(values.value, dep);
    });
  });

  const resetConditionState = () => {
    isDisabled.value = false;
    isIf.value = true;
    isShow.value = true;
    isRequired.value = false;
    dynamicRules.value = undefined;
    dynamicComponentProps.value = {};
  };

  watch(
    [triggerFieldValues, getDependencies],
    async ([_values, dependencies]) => {
      if (!dependencies || !dependencies?.triggerFields?.length) {
        return;
      }
      resetConditionState();
      const {
        componentProps,
        disabled,
        if: whenIf,
        required,
        rules,
        show,
        trigger,
      } = dependencies;

      // 1. if를 우선 판단하며, if가 false인 경우 DOM을 렌더링하지 않고 후속 판단도 실행하지 않음
      const formValues = values.value;

      if (isFunction(whenIf)) {
        isIf.value = !!(await whenIf(formValues, formApi));
        // 렌더링하지 않음
        if (!isIf.value) return;
      } else if (isBoolean(whenIf)) {
        isIf.value = whenIf;
        if (!isIf.value) return;
      }

      // 2. show를 판단하며, show가 false인 경우 숨김
      if (isFunction(show)) {
        isShow.value = !!(await show(formValues, formApi));
      } else if (isBoolean(show)) {
        isShow.value = show;
      }

      if (isFunction(componentProps)) {
        dynamicComponentProps.value = await componentProps(formValues, formApi);
      }

      if (isFunction(rules)) {
        dynamicRules.value = await rules(formValues, formApi);
      }

      if (isFunction(disabled)) {
        isDisabled.value = !!(await disabled(formValues, formApi));
      } else if (isBoolean(disabled)) {
        isDisabled.value = disabled;
      }

      if (isFunction(required)) {
        isRequired.value = !!(await required(formValues, formApi));
      }

      if (isFunction(trigger)) {
        trigger(formValues, formApi);
      }
    },
    { deep: true, immediate: true },
  );

  return {
    dynamicComponentProps,
    dynamicRules,
    isDisabled,
    isIf,
    isRequired,
    isShow,
  };
}
