<script setup lang="ts">
import type { Recordable } from '@vben-core/typings';

import type { ExtendedFormApi, VbenFormProps } from './types';

// import { toRaw, watch } from 'vue';
import { nextTick, onMounted, watch } from 'vue';

import { useForwardPriorityValues } from '@vben-core/composables';
import { cloneDeep, get, isEqual, set } from '@vben-core/shared/utils';

import { useDebounceFn } from '@vueuse/core';

import FormActions from './components/form-actions.vue';
import {
  COMPONENT_BIND_EVENT_MAP,
  COMPONENT_MAP,
  DEFAULT_FORM_COMMON_CONFIG,
} from './config';
import { Form } from './form-render';
import {
  provideComponentRefMap,
  provideFormProps,
  useFormInitial,
} from './use-form-context';
// extends를 사용하면 HMR(Hot Module Replacement)이 멈출 수 있어 다시 작성함
interface Props extends VbenFormProps {
  formApi?: ExtendedFormApi;
}

const props = defineProps<Props>();

const state = props.formApi?.useStore?.();

const forward = useForwardPriorityValues(props, state);

const componentRefMap = new Map<string, unknown>();

const { delegatedSlots, form } = useFormInitial(forward);

provideFormProps([forward, form]);
provideComponentRefMap(componentRefMap);

props.formApi?.mount?.(form, componentRefMap);

const handleUpdateCollapsed = (value: boolean) => {
  props.formApi?.setState({ collapsed: value });
  // 접힘/펼침 상태 변경 콜백 트리거
  forward.value.handleCollapsedChange?.(value);
};

function handleKeyDownEnter(event: KeyboardEvent) {
  if (!state?.value.submitOnEnter || !forward.value.formApi?.isMounted) {
    return;
  }
  // textarea인 경우 기본 동작을 방지하지 않습니다. 그렇지 않으면 줄바꿈을 할 수 없게 됩니다.
  // textarea의 엔터 키 제출 처리를 건너뜜
  if (event.target instanceof HTMLTextAreaElement) {
    return;
  }
  event.preventDefault();

  forward.value.formApi?.validateAndSubmitForm();
}

const handleValuesChangeDebounced = useDebounceFn(async () => {
  state?.value.submitOnChange && forward.value.formApi?.validateAndSubmitForm();
}, 300);

const valuesCache: Recordable<any> = {};

onMounted(async () => {
  // 마운트 후에만 리스닝을 시작합니다. form.values에는 초기화 과정이 있습니다.
  await nextTick();
  watch(
    () => form.values,
    async (newVal) => {
      if (forward.value.handleValuesChange) {
        const fields = state?.value.schema?.map((item) => {
          return item.fieldName;
        });

        if (fields && fields.length > 0) {
          const changedFields: string[] = [];
          fields.forEach((field) => {
            const newFieldValue = get(newVal, field);
            const oldFieldValue = get(valuesCache, field);
            if (!isEqual(newFieldValue, oldFieldValue)) {
              changedFields.push(field);
              set(valuesCache, field, newFieldValue);
            }
          });

          if (changedFields.length > 0) {
            // 모든 폼 값의 깊은 복사본과 변경된 필드 목록을 전달하여 handleValuesChange 콜백을 호출합니다.
            const values = await forward.value.formApi?.getValues();
            forward.value.handleValuesChange(
              cloneDeep(values ?? {}) as Record<string, any>,
              changedFields,
            );
          }
        }
      }
      handleValuesChangeDebounced();
    },
    { deep: true },
  );
});
</script>

<template>
  <Form
    @keydown.enter="handleKeyDownEnter"
    v-bind="forward"
    :collapsed="state?.collapsed"
    :component-bind-event-map="COMPONENT_BIND_EVENT_MAP"
    :component-map="COMPONENT_MAP"
    :form="form"
    :global-common-config="DEFAULT_FORM_COMMON_CONFIG"
  >
    <template
      v-for="slotName in delegatedSlots"
      :key="slotName"
      #[slotName]="slotProps"
    >
      <slot :name="slotName" v-bind="slotProps"></slot>
    </template>
    <template #default="slotProps">
      <slot v-bind="slotProps">
        <FormActions
          v-if="forward.showDefaultActions"
          :model-value="state?.collapsed"
          @update:model-value="handleUpdateCollapsed"
        >
          <template #reset-before="resetSlotProps">
            <slot name="reset-before" v-bind="resetSlotProps"></slot>
          </template>
          <template #submit-before="submitSlotProps">
            <slot name="submit-before" v-bind="submitSlotProps"></slot>
          </template>
          <template #expand-before="expandBeforeSlotProps">
            <slot name="expand-before" v-bind="expandBeforeSlotProps"></slot>
          </template>
          <template #expand-after="expandAfterSlotProps">
            <slot name="expand-after" v-bind="expandAfterSlotProps"></slot>
          </template>
        </FormActions>
      </slot>
    </template>
  </Form>
</template>
