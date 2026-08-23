<script setup lang="ts">
import type { ExtendedFormApi, VbenFormProps, VbenFormSlots } from './types';

import { nextTick, onMounted, readonly, watch } from 'vue';

import { useForwardPriorityValues } from '@vben-core/composables';
import { get, isEqual } from '@vben-core/shared/utils';

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
  formApi?: ExtendedFormApi<any, any, any, any>;
}

const props = defineProps<Props>();
defineSlots<
  Record<string, ((props: Record<string, any>) => any) | undefined> &
    VbenFormSlots<any, any, any>
>();

const formApi = props.formApi;
if (!formApi) {
  throw new Error('Form api is required in <VbenUseForm />');
}

const state = formApi.useStore();

const forward = useForwardPriorityValues(props, state);

const componentRefMap = new Map<string, unknown>();

const { delegatedSlots, form } = useFormInitial(forward);
const values = form.useValues();

provideFormProps([forward, form]);
provideComponentRefMap(componentRefMap);

formApi.mount(form, componentRefMap);

function handleUpdateCollapsed(value: boolean) {
  props.formApi?.setState({ collapsed: value });
  // 접힘/펼침 상태 변경 콜백 트리거
  forward.value.handleCollapsedChange?.(value);
}

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

  forward.value.formApi?.validateAndSubmit();
}

const handleValuesChangeDebounced = useDebounceFn(async () => {
  state?.value.submitOnChange && forward.value.formApi?.validateAndSubmit();
}, state?.value?.changeDebouncedTime ?? 300);

let valuesChangeReady = false;

onMounted(async () => {
  // 마운트 후에만 리스닝을 시작합니다. form.values에는 초기화 과정이 있습니다.
  await nextTick();
  valuesChangeReady = true;
});

watch(values, (currentValues, previousValues) => {
  if (!valuesChangeReady) {
    return;
  }
  const handleValuesChange = forward.value.handleValuesChange;
  const submitOnChange = state?.value.submitOnChange;
  if (!handleValuesChange && !submitOnChange) {
    return;
  }
  const fields = state?.value.schema?.map((item) => item.fieldName) ?? [];
  if (handleValuesChange && fields.length > 0) {
    const changedFields = fields.filter((field) => {
      return !isEqual(
        get(currentValues, field),
        get(previousValues ?? {}, field),
      );
    });
    if (changedFields.length > 0) {
      handleValuesChange(readonly(currentValues), changedFields, () =>
        formApi.formatValues(currentValues),
      );
    }
  }
  if (submitOnChange) {
    handleValuesChangeDebounced();
  }
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
      <slot
        :name="slotName"
        v-bind="slotProps"
        :form-api="formApi"
        :values="form.values"
      ></slot>
    </template>
    <template #default="slotProps">
      <slot
        v-if="$slots.default"
        v-bind="slotProps"
        :form-api="formApi"
        :values="form.values"
      ></slot>
      <FormActions
        v-else-if="forward.showDefaultActions"
        :model-value="state?.collapsed"
        @update:model-value="handleUpdateCollapsed"
      >
        <template #reset-before="resetSlotProps">
          <slot
            name="reset-before"
            v-bind="resetSlotProps"
            :form-api="formApi"
            :values="form.values"
          ></slot>
        </template>
        <template #submit-before="submitSlotProps">
          <slot
            name="submit-before"
            v-bind="submitSlotProps"
            :form-api="formApi"
            :values="form.values"
          ></slot>
        </template>
        <template #expand-before="expandBeforeSlotProps">
          <slot
            name="expand-before"
            v-bind="expandBeforeSlotProps"
            :form-api="formApi"
            :values="form.values"
          ></slot>
        </template>
        <template #expand-after="expandAfterSlotProps">
          <slot
            name="expand-after"
            v-bind="expandAfterSlotProps"
            :form-api="formApi"
            :values="form.values"
          ></slot>
        </template>
      </FormActions>
    </template>
  </Form>
</template>
