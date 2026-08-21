<script lang="ts" setup>
import { ref } from 'vue';

import { useVbenDrawer } from '@vben/common-ui';

import { Input, message } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

const value = ref('');

const [Form] = useVbenForm({
  schema: [
    {
      component: 'Input',
      componentProps: {
        placeholder: 'KeepAlive 테스트：내부 컴포넌트',
      },
      fieldName: 'field1',
      hideLabel: true,
      label: '필드1',
    },
  ],
  showDefaultActions: false,
});

const [Drawer, drawerApi] = useVbenDrawer({
  destroyOnClose: false,
  onCancel() {
    drawerApi.close();
  },
  onConfirm() {
    message.info('onConfirm');
    // drawerApi.close();
  },
});
</script>
<template>
  <Drawer append-to-main title="기본 드로어 예시" title-tooltip="제목 툴팁 내용">
    <template #extra> extra </template>
    이 팝업은 콘텐츠 영역에서 열리도록 지정되었으며, 닫힌 후에도 팝업 콘텐츠가 파괴되지 않습니다.
    <Input
      v-model:value="value"
      placeholder="KeepAlive 테스트:connectedComponent"
    />
    <Form />
  </Drawer>
</template>
