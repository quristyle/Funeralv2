<script lang="ts" setup>
import { useVbenDrawer } from '@vben/common-ui';

import { useVbenForm } from '#/adapter/form';

defineOptions({
  name: 'FormDrawerDemo',
});

const [Form, formApi] = useVbenForm({
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
  ],
  showDefaultActions: false,
});
const [Drawer, drawerApi] = useVbenDrawer({
  onCancel() {
    drawerApi.close();
  },
  onConfirm: async () => {
    await formApi.submitForm();
    drawerApi.close();
  },
  onOpenChange(isOpen: boolean) {
    if (isOpen) {
      const { values } = drawerApi.getData<Record<string, any>>();
      if (values) {
        formApi.setValues(values);
      }
    }
  },
  title: '임베디드 폼 예시',
});
</script>
<template>
  <Drawer>
    <Form />
  </Drawer>
</template>
