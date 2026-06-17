<script lang="ts" setup>
import { useVbenDrawer } from '@vben/common-ui';

import { Button, message } from 'ant-design-vue';

const [Drawer, drawerApi] = useVbenDrawer({
  onCancel() {
    drawerApi.close();
  },
  onClosed() {
    drawerApi.setState({ overlayBlur: 0, placement: 'right' });
  },
  onConfirm() {
    message.info('onConfirm');
    // drawerApi.close();
  },
});

function lockDrawer() {
  drawerApi.lock();
  setTimeout(() => {
    drawerApi.unlock();
  }, 3000);
}
</script>
<template>
  <Drawer title="기본 드로어 예시" title-tooltip="제목 툴팁 내용">
    <template #extra> extra </template>
    기본 데모
    <Button type="primary" @click="lockDrawer">드로어 상태 잠금</Button>
    <!-- <template #prepend-footer> slot </template> -->
    <!-- <template #append-footer> prepend slot </template> -->
    <!-- <template #center-footer> center slot </template> -->
  </Drawer>
</template>
