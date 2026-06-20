<script lang="ts" setup>
import { ref, nextTick } from 'vue';
import { useBaseModal } from '#/adapter/modal';
import { useVbenForm } from '#/adapter/form';
import { codeFormSchema } from '../data';
import { createCommonCode, updateCommonCode, type CommonCodeParams } from '#/api/system/common-code';
import { message } from 'ant-design-vue';
import AiCodeSuggester from '#/components/ai-code-suggester/ai-code-suggester.vue';

const emit = defineEmits(['success']);
const isUpdate = ref(false);
const currentId = ref('');
const currentGroupId = ref('');
const currentParentId = ref<string | undefined>(undefined);
const codeNameVal = ref('');

const schema = (codeFormSchema.schema ?? []).map((item) => {
  if (item.fieldName === 'codeName') {
    return {
      ...item,
      componentProps: {
        ...item.componentProps,
        onChange: (e: any) => {
          codeNameVal.value = e?.target?.value || e;
        },
        onInput: (e: any) => {
          codeNameVal.value = e?.target?.value || e;
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
      const values = await formApi.getValues();
      
      const statusValue = typeof values.status === 'boolean' 
        ? (values.status ? 1 : 0) 
        : values.status;

      const params = {
        ...values,
        status: statusValue,
        groupId: currentGroupId.value,
        parentId: currentParentId.value,
      } as unknown as CommonCodeParams;

      try {
        if (isUpdate.value) {
          await updateCommonCode(currentId.value, params);
          message.success('코드가 수정되었습니다.');
        } else {
          await createCommonCode(params);
          message.success('코드가 생성되었습니다.');
        }
        
        modalApi.close();
        emit('success');
      } finally {
        modalApi.lock(false);
      }
    }
  },
  onOpenChange(isOpen) {
    if (!isOpen) {
      isUpdate.value = false;
      currentId.value = '';
      codeNameVal.value = '';
      formApi.resetForm();
      formApi.updateSchema([
        {
          fieldName: 'codeValue',
          componentProps: {
            disabled: false,
            placeholder: '코드값을 입력하세요',
          },
        },
      ]);
    }
  },
});

function openModal(groupId: string, record?: any, parentId?: string) {
  currentGroupId.value = groupId;
  currentParentId.value = parentId;
  isUpdate.value = !!record;
  currentId.value = record?.id || '';
  codeNameVal.value = record?.codeName || '';

  if (record) {
    const bindRecord = {
      ...record,
      status: record.status === 1,
    };
    nextTick(() => {
      formApi.setValues(bindRecord);
      formApi.updateSchema([
        {
          fieldName: 'codeValue',
          componentProps: {
            disabled: true,
            placeholder: '수정 시 코드값은 변경할 수 없습니다.',
          },
        },
      ]);
    });
  } else {
    nextTick(() => {
      formApi.resetForm();
      formApi.updateSchema([
        {
          fieldName: 'codeValue',
          componentProps: {
            disabled: false,
            placeholder: '코드값을 입력하세요',
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
  <BaseModal :title="isUpdate ? '코드 수정' : '코드 추가'">
    <Form class="mx-4" />
    <AiCodeSuggester 
      v-if="!isUpdate"
      :input-text="codeNameVal" 
      @select="(code) => formApi.setFieldValue('codeValue', code)" 
    />
  </BaseModal>
</template>
