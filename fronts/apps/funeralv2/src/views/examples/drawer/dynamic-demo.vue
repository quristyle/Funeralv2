<script lang="ts" setup>
import { useVbenDrawer } from '@vben/common-ui';

import { Button, message } from 'ant-design-vue';

const [Drawer, drawerApi] = useVbenDrawer({
  onCancel() {
    drawerApi.close();
  },
  onConfirm() {
    message.info('onConfirm');
    // drawerApi.close();
  },
  title: '동적 설정 수정 예시',
});

// const state = drawerApi.useStore();

function handleUpdateTitle() {
  drawerApi.setState({ title: '내부 동적 제목' });
}
</script>
<template>
  <Drawer>
    <div class="flex-col-center">
      <Button class="mb-3" type="primary" @click="handleUpdateTitle()">
        내부에서 동적으로 제목 수정
      </Button>
    </div>
  </Drawer>
</template>
