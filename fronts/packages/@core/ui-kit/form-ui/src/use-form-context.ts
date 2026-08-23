import type { ZodType } from 'zod';

import type { ComputedRef } from 'vue';

import type { ExtendedFormApi, FormActions, VbenFormProps } from './types';

import { computed, toRaw, unref, useSlots } from 'vue';

import { createContext } from '@vben-core/shadcn-ui';
import { isString, mergeWithArrayOverride, set } from '@vben-core/shared/utils';

import { object, ZodIntersection, ZodNumber, ZodObject, ZodString } from 'zod';
import { getDefaultsForSchema } from 'zod-defaults';

import { useFormRuntime } from './form-runtime';

type ExtendFormProps = VbenFormProps & {
  formApi?: ExtendedFormApi<any, any, any>;
};

export const [injectFormProps, provideFormProps] =
  createContext<[ComputedRef<ExtendFormProps> | ExtendFormProps, FormActions]>(
    'VbenFormProps',
  );

export const [injectComponentRefMap, provideComponentRefMap] =
  createContext<Map<string, unknown>>('ComponentRefMap');

export function useFormInitial(
  props: ComputedRef<VbenFormProps> | VbenFormProps,
) {
  const slots = useSlots();
  const initialValues = generateInitialValues();

  const form = useFormRuntime(initialValues);

  const delegatedSlots = computed(() => {
    const resultSlots: string[] = [];

    for (const key of Object.keys(slots)) {
      if (key !== 'default') {
        resultSlots.push(key);
      }
    }
    return resultSlots;
  });

  function generateInitialValues() {
    const initialValues: Record<string, any> = {};

    const zodObject: Record<string, ZodType> = {};
    (unref(props).schema || []).forEach((item) => {
      if (Reflect.has(item, 'defaultValue')) {
        set(initialValues, item.fieldName, item.defaultValue);
      } else if (item.rules && !isString(item.rules)) {
        // 규칙이 기본값을 추출하기에 적합한지 확인
        const rawRules = toRaw(item.rules);
        const customDefaultValue = getCustomDefaultValue(rawRules);
        zodObject[item.fieldName] = rawRules;
        if (customDefaultValue !== undefined) {
          initialValues[item.fieldName] = customDefaultValue;
        }
      }
    });

    const schemaInitialValues = getDefaultsForSchema(object(zodObject));

    const zodDefaults: Record<string, any> = {};
    for (const key in schemaInitialValues) {
      set(zodDefaults, key, schemaInitialValues[key]);
    }
    return mergeWithArrayOverride(initialValues, zodDefaults);
  }
  // 커스텀 기본값 추출 로직
  function getCustomDefaultValue(rule: any): any {
    rule = toRaw(rule);
    if (rule instanceof ZodString) {
      return ''; // 기본값은 빈 문자열
    } else if (rule instanceof ZodNumber) {
      return null; // 기본값은 null (0이 표시되는 것을 방지)
    } else if (rule instanceof ZodObject) {
      // 중첩된 객체의 기본값을 재귀적으로 추출
      const defaultValues: Record<string, any> = {};
      for (const [key, valueSchema] of Object.entries(rule.shape)) {
        defaultValues[key] = getCustomDefaultValue(valueSchema);
      }
      return defaultValues;
    } else if (rule instanceof ZodIntersection) {
      return getDefaultsForSchema(rule);
    } else {
      return undefined; // 기타 타입은 기본값을 제공하지 않음
    }
  }

  return {
    delegatedSlots,
    form,
  };
}
