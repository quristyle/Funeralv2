<script lang="ts" setup>
import { useVbenModal } from '@vben/common-ui';

import { message } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

defineOptions({
  name: 'FormModelDemo',
});

const [Form, formApi] = useVbenForm({
  handleSubmit: onSubmit,
  schema: [
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력해주세요',
      },
      fieldName: 'field1',
      label: '필드1',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력해주세요',
      },
      fieldName: 'field2',
      label: '필드2',
      rules: 'required',
    },
    {
      component: 'Select',
      componentProps: {
        options: [
          { label: '옵션1', value: '1' },
          { label: '옵션2', value: '2' },
        ],
        placeholder: '입력해주세요',
      },
      fieldName: 'field3',
      label: '필드3',
      rules: 'required',
    },
  ],
  showDefaultActions: false,
});

const [Modal, modalApi] = useVbenModal({
  fullscreenButton: false,
  onCancel() {
    modalApi.close();
  },
  onConfirm: async () => {
    await formApi.validateAndSubmitForm();
    // modalApi.close();
  },
  onOpenChange(isOpen: boolean) {
    if (isOpen) {
      const { values } = modalApi.getData<Record<string, any>>();
      if (values) {
        formApi.setValues(values);
      }
    }
  },
  title: '내장 폼 예제',
});

function onSubmit(values: Record<string, any>) {
  message.loading({
    content: '제출 중...',
    duration: 0,
    key: 'is-form-submitting',
  });
  modalApi.lock();
  setTimeout(() => {
    modalApi.close();
    message.success({
      content: `제출 성공: ${JSON.stringify(values)}`,
      duration: 2,
      key: 'is-form-submitting',
    });
  }, 3000);
}
</script>
<template>
  <Modal>
    <Form />
  </Modal>
</template>
