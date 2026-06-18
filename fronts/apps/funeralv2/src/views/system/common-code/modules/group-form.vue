<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenModal } from '@vben/common-ui';
import { useVbenForm } from '#/adapter/form';
import { createCommonCodeGroup, suggestCommonCodeByAI } from '#/api/system/common-code';
import { message, Tag, Spin } from 'ant-design-vue';
import { useDebounceFn } from '@vueuse/core';
import type { VbenFormSchema } from '#/adapter/form';

const emit = defineEmits(['success']);

const suggestedCode = ref('');
const isSuggesting = ref(false);

// 한글 그룹명 변경 감지 및 AI 추천 호출 (500ms 디바운스)
const handleGroupNameChange = useDebounceFn(async (e: any) => {
  const newName = e?.target?.value || e;
  if (!newName || typeof newName !== 'string' || newName.trim().length === 0) {
    suggestedCode.value = '';
    return;
  }
  
  isSuggesting.value = true;
  try {
    const code = await suggestCommonCodeByAI(newName);
    suggestedCode.value = code;
  } catch (error) {
    console.error('AI 추천 실패:', error);
    suggestedCode.value = '';
  } finally {
    isSuggesting.value = false;
  }
}, 500);

const schema: VbenFormSchema[] = [
  {
    component: 'Input',
    componentProps: {
      placeholder: '그룹코드를 입력하세요',
    },
    fieldName: 'groupCode',
    label: '그룹코드',
    rules: 'required',
  },
  {
    component: 'Input',
    componentProps: {
      placeholder: '그룹명을 입력하세요',
      onChange: handleGroupNameChange,
      onInput: handleGroupNameChange,
    },
    fieldName: 'groupName',
    label: '그룹명',
    rules: 'required',
  },
  {
    component: 'Switch',
    fieldName: 'isHierarchical',
    label: '계층구조 여부',
    defaultValue: false,
  },
  {
    component: 'Textarea',
    fieldName: 'remark',
    label: '비고',
  },
];

const [Form, formApi] = useVbenForm({
  layout: 'vertical',
  schema: schema,
  showDefaultActions: false,
});

function applySuggestedCode() {
  if (suggestedCode.value) {
    formApi.setFieldValue('groupCode', suggestedCode.value);
  }
}

const [Modal, modalApi] = useVbenModal({
  async onConfirm() {
    const { valid } = await formApi.validate();
    if (valid) {
      modalApi.lock();
      const values = await formApi.getValues();
      try {
        await createCommonCodeGroup(values);
        message.success('그룹이 생성되었습니다.');
        modalApi.close();
        emit('success');
      } finally {
        modalApi.lock(false);
      }
    }
  },
  onOpenChange(isOpen) {
    if (isOpen) {
      formApi.resetForm();
      suggestedCode.value = '';
    }
  },
});

function openModal() {
  modalApi.open();
}

defineExpose({ openModal });
</script>

<template>
  <Modal title="코드 그룹 추가">
    <Form class="mx-4">
      <template #groupName-suffix>
        <!-- 추후 폼 스키마에 슬롯을 열어주면 삽입 가능 -->
      </template>
    </Form>
    <!-- 입력 폼 하단에 추천 코드 표시 -->
    <div class="mx-4 mt-2 px-1 text-sm" style="min-height: 24px;">
      <Spin v-if="isSuggesting" size="small" />
      <div v-else-if="suggestedCode" class="flex items-center gap-2">
        <span class="text-gray-500">💡 AI 추천 코드:</span>
        <Tag color="blue" class="cursor-pointer hover:opacity-80" @click="applySuggestedCode">
          {{ suggestedCode }}
        </Tag>
        <span class="text-xs text-gray-400">(클릭하여 적용)</span>
      </div>
    </div>
  </Modal>
</template>
