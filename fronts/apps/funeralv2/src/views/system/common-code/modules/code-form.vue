<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenModal } from '@vben/common-ui';
import { codeFormSchema } from '../data';
import { createCommonCode, updateCommonCode } from '#/api/system/common-code';
import { message } from 'ant-design-vue';

const emit = defineEmits(['success']);
const isUpdate = ref(false);
const currentId = ref('');
const currentGroupId = ref('');
const currentParentId = ref<string | undefined>(undefined);

const [Modal, modalApi] = useVbenModal({
  connectedComponent: true,
  draggable: true,
  onConfirm: async () => {
    try {
      const values = await modalApi.getFormApi().validate();
      modalApi.setState({ confirmLoading: true });
      
      const params = {
        ...values,
        groupId: currentGroupId.value,
        parentId: currentParentId.value,
      };

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
      modalApi.setState({ confirmLoading: false });
    }
  },
});

function openModal(groupId: string, record?: any, parentId?: string) {
  currentGroupId.value = groupId;
  currentParentId.value = parentId;
  isUpdate.value = !!record;
  currentId.value = record?.id || '';

  modalApi.setFormProps({
    ...codeFormSchema,
    commonConfig: {
      labelWidth: 100,
    },
  });

  if (record) {
    modalApi.getFormApi().setValues(record);
  } else {
    modalApi.getFormApi().resetForm();
  }
  
  modalApi.open();
}

defineExpose({ openModal });
</script>

<template>
  <Modal :title="isUpdate ? '코드 수정' : '코드 추가'" />
</template>
