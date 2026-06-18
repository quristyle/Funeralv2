<script lang="ts" setup>
import { useVbenModal } from '@vben/common-ui';
import { groupFormSchema } from '../data';
import { createCommonCodeGroup } from '#/api/system/common-code';
import { message } from 'ant-design-vue';

const emit = defineEmits(['success']);

const [Modal, modalApi] = useVbenModal({
  connectedComponent: true,
  draggable: true,
  onConfirm: async () => {
    try {
      const values = await modalApi.getFormApi().validate();
      modalApi.setState({ confirmLoading: true });
      await createCommonCodeGroup(values);
      message.success('그룹이 생성되었습니다.');
      modalApi.close();
      emit('success');
    } finally {
      modalApi.setState({ confirmLoading: false });
    }
  },
});

function openModal() {
  modalApi.setFormProps(groupFormSchema);
  modalApi.open();
}

defineExpose({ openModal });
</script>

<template>
  <Modal title="코드 그룹 추가" />
</template>
