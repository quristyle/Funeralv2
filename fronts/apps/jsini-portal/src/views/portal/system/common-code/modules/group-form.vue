<script lang="ts" setup>
import { ref, nextTick } from 'vue';
import { useBaseModal } from '#/adapter/modal';
import { useVbenForm } from '#/adapter/form';
import { createCommonCodeGroup, updateCommonCodeGroup, type CommonCodeGroupParams } from '#/api/portal/system/common-code';
import { message } from 'ant-design-vue';
import AiCodeSuggester from '#/components/ai-code-suggester/ai-code-suggester.vue';
import { groupFormSchema } from '../data';

const emit = defineEmits(['success']);

const groupNameVal = ref('');
const editRecordId = ref<string | null>(null);
const isEdit = ref(false);

const schema = (groupFormSchema.schema ?? []).map((item) => {
  if (item.fieldName === 'groupName') {
    return {
      ...item,
      componentProps: {
        ...item.componentProps,
        onChange: (e: any) => {
          groupNameVal.value = e?.target?.value || e;
        },
        onInput: (e: any) => {
          groupNameVal.value = e?.target?.value || e;
        },
      },
    };
  }
  return item;
});

const [Form, formApi] = useVbenForm({
  layout: 'vertical',
  schema: schema,
  showDefaultActions: false,
});

const [BaseModal, modalApi] = useBaseModal({
  async onConfirm() {
    const { valid } = await formApi.validate();
    if (valid) {
      modalApi.lock();
      const values = (await formApi.getValues()) as unknown as CommonCodeGroupParams;
      try {
        if (isEdit.value && editRecordId.value) {
          await updateCommonCodeGroup(editRecordId.value, values);
          message.success('그룹이 수정되었습니다.');
        } else {
          await createCommonCodeGroup(values);
          message.success('그룹이 생성되었습니다.');
        }
        modalApi.close();
        emit('success');
      } finally {
        modalApi.lock(false);
      }
    }
  },
  onOpenChange(isOpen) {
    if (isOpen) {
      if (!isEdit.value) {
        formApi.resetForm();
        groupNameVal.value = '';
        formApi.updateSchema([
          {
            fieldName: 'groupCode',
            componentProps: {
              disabled: false,
              placeholder: '그룹코드를 입력하세요',
            },
          },
        ]);
      }
    } else {
      isEdit.value = false;
      editRecordId.value = null;
      groupNameVal.value = '';
      formApi.resetForm();
      formApi.updateSchema([
        {
          fieldName: 'groupCode',
          componentProps: {
            disabled: false,
            placeholder: '그룹코드를 입력하세요',
          },
        },
      ]);
    }
  },
});

function openModal(record?: any) {
  if (record && record.id) {
    isEdit.value = true;
    editRecordId.value = record.id;
    groupNameVal.value = record.groupName || '';
    nextTick(() => {
      formApi.setValues(record);
      formApi.updateSchema([
        {
          fieldName: 'groupCode',
          componentProps: {
            disabled: true,
            placeholder: '수정 시 그룹코드는 변경할 수 없습니다.',
          },
        },
      ]);
    });
  } else {
    isEdit.value = false;
    editRecordId.value = null;
    groupNameVal.value = '';
    nextTick(() => {
      formApi.updateSchema([
        {
          fieldName: 'groupCode',
          componentProps: {
            disabled: false,
            placeholder: '그룹코드를 입력하세요',
          },
        },
      ]);
    });
  }
  modalApi.open();
}

defineExpose({ openModal });
</script>

<template>
  <BaseModal :title="isEdit ? '코드 그룹 수정' : '코드 그룹 추가'">
    <Form class="mx-4">
      <template #groupName-suffix>
        <!-- 추후 폼 스키마에 슬롯을 열어주면 삽입 가능 -->
      </template>
    </Form>
    <!-- 입력 폼 하단에 추천 코드 표시 (수정 모드가 아닐 때만 렌더링) -->
    <AiCodeSuggester 
      v-if="!isEdit && groupNameVal"
      :input-text="groupNameVal" 
      @select="(code) => formApi.setFieldValue('groupCode', code)" 
    />
  </BaseModal>
</template>
