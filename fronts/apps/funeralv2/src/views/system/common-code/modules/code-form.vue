<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenModal } from '@vben/common-ui';
import { useVbenForm } from '#/adapter/form';
import { codeFormSchema } from '../data';
import { createCommonCode, updateCommonCode } from '#/api/system/common-code';
import { message } from 'ant-design-vue';

const emit = defineEmits(['success']);
const isUpdate = ref(false);
const currentId = ref('');
const currentGroupId = ref('');
const currentParentId = ref<string | undefined>(undefined);

const [Form, formApi] = useVbenForm({
  layout: 'vertical',
  schema: codeFormSchema.schema,
  showDefaultActions: false,
});

const [Modal, modalApi] = useVbenModal({
  async onConfirm() {
    const { valid } = await formApi.validate();
    if (valid) {
      modalApi.lock();
      const values = await formApi.getValues();
      
      const params = {
        ...values,
        groupId: currentGroupId.value,
        parentId: currentParentId.value,
      };

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
});

function openModal(groupId: string, record?: any, parentId?: string) {
  currentGroupId.value = groupId;
  currentParentId.value = parentId;
  isUpdate.value = !!record;
  currentId.value = record?.id || '';

  if (record) {
    formApi.setValues(record);
  } else {
    formApi.resetForm();
  }
  
  modalApi.open();
}

defineExpose({ openModal });
</script>

<template>
  <Modal :title="isUpdate ? '코드 수정' : '코드 추가'">
    <Form class="mx-4" />
  </Modal>
</template>
