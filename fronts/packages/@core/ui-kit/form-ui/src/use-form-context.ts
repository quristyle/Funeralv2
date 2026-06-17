import type { ZodRawShape } from 'zod';

import type { ComputedRef } from 'vue';

import type { ExtendedFormApi, FormActions, VbenFormProps } from './types';

import { computed, unref, useSlots } from 'vue';

import { createContext } from '@vben-core/shadcn-ui';
import { isString, mergeWithArrayOverride, set } from '@vben-core/shared/utils';

import { useForm } from 'vee-validate';
import { object, ZodIntersection, ZodNumber, ZodObject, ZodString } from 'zod';
import { getDefaultsForSchema } from 'zod-defaults';

type ExtendFormProps = VbenFormProps & { formApi?: ExtendedFormApi };

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

  const form = useForm({
    ...(Object.keys(initialValues)?.length ? { initialValues } : {}),
  });

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

    const zodObject: ZodRawShape = {};
    (unref(props).schema || []).forEach((item) => {
      if (Reflect.has(item, 'defaultValue')) {
        set(initialValues, item.fieldName, item.defaultValue);
      } else if (item.rules && !isString(item.rules)) {
        // 규칙이 기본값을 추출하기에 적합한지 확인
        const customDefaultValue = getCustomDefaultValue(item.rules);
        zodObject[item.fieldName] = item.rules;
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
      // 교차 타입(Intersection)의 경우, 스키마에서 기본값을 추출
      const leftDefaultValue = getCustomDefaultValue(rule._def.left);
      const rightDefaultValue = getCustomDefaultValue(rule._def.right);

      // 좌우 양쪽 모두에서 기본값을 추출할 수 있는 경우, 이를 병합
      if (
        typeof leftDefaultValue === 'object' &&
        typeof rightDefaultValue === 'object'
      ) {
        return { ...leftDefaultValue, ...rightDefaultValue };
      }

      // 그렇지 않으면 왼쪽의 기본값을 우선적으로 사용
      return leftDefaultValue ?? rightDefaultValue;
    } else {
      return undefined; // 기타 타입은 기본값을 제공하지 않음
    }
  }

  return {
    delegatedSlots,
    form,
  };
}
