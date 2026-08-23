<script lang="ts" setup>
import { ref } from 'vue';

import { useVbenDrawer } from '@vben/common-ui';

import { message } from 'ant-design-vue';

const data = ref();

const [Drawer, drawerApi] = useVbenDrawer<Record<string, any>>({
  onCancel() {
    drawerApi.close();
  },
  onConfirm() {
    message.info('onConfirm');
    // drawerApi.close();
  },
  onOpenChange(isOpen: boolean) {
    if (isOpen) {
      data.value = drawerApi.getData();
    }
  },
});
</script>
<template>
  <Drawer title="데이터 공유 예시">
    <div class="flex-col-center">외부 전달 데이터： {{ data }}</div>
  </Drawer>
</template>
