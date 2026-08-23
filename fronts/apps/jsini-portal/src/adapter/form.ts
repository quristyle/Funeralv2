import type {
  VbenFormProps as FormProps,
  VbenFormSchema as FormSchema,
  FormValues,
} from '@vben/common-ui';

import type { ComponentType } from './component';

import { setupVbenForm, useVbenForm as useForm, z } from '@vben/common-ui';
import { $t } from '@vben/locales';

/**
 * 컴포넌트별 `componentProps` 타입 표.
 *
 * 상위(vben-admin)는 여기에 컴포넌트마다 실제 props 타입을 적어 두고
 * 스키마에서 `componentProps` 를 자동 완성·검증한다.
 *
 * 이 포털은 그렇게 하지 않는다. 화면이 100곳이 넘고 antd 를 감싸 쓰는 자리가 많아
 * 정확한 표를 만들려면 그 화면들을 모두 손봐야 한다. 지금은 느슨하게 두고,
 * 필요해질 때 컴포넌트 단위로 하나씩 좁혀 나간다.
 */
export type ComponentPropsMap = Record<ComponentType, Record<string, any>>;

async function initSetupVbenForm() {
  setupVbenForm<ComponentType>({
    config: {
      // Ant Design Vue 컴포넌트 라이브러리는 기본적으로 v-model:value를 사용합니다.
      baseModelPropName: 'value',
      // 일부 컴포넌트는 v-model:checked 또는 v-model:fileList를 사용합니다.
      modelPropNameMap: {
        Checkbox: 'checked',
        Radio: 'checked',
        Switch: 'checked',
        Upload: 'fileList',
      },
    },
    rules: {
      // 입력 항목 필수 국제화 어댑팅
      required: (value, _params, ctx) => {
        if (value === undefined || value === null || value.length === 0) {
          return $t('ui.formRules.required', [ctx.label]);
        }
        return true;
      },
      // 선택 항목 필수 국제화 어댑팅
      selectRequired: (value, _params, ctx) => {
        if (value === undefined || value === null) {
          return $t('ui.formRules.selectRequired', [ctx.label]);
        }
        return true;
      },
    },
  });
}

function useVbenForm<
  TFormValues extends FormValues = FormValues,
  TSubmitValues extends FormValues = TFormValues,
>(
  options: FormProps<
    ComponentType,
    ComponentPropsMap,
    TFormValues,
    TSubmitValues
  >,
) {
  return useForm<TFormValues, ComponentType, ComponentPropsMap, TSubmitValues>(
    options,
  );
}

export { initSetupVbenForm, useVbenForm, z };

export type VbenFormSchema<TValues extends FormValues = FormValues> =
  FormSchema<ComponentType, ComponentPropsMap, TValues>;
export type VbenFormProps<
  TFormValues extends FormValues = FormValues,
  TSubmitValues extends FormValues = TFormValues,
> = FormProps<ComponentType, ComponentPropsMap, TFormValues, TSubmitValues>;
