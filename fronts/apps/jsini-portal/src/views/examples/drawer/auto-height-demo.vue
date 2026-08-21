<script lang="ts" setup>
import { ref } from 'vue';

import { useVbenDrawer } from '@vben/common-ui';

import { Button, message } from 'ant-design-vue';

const list = ref<number[]>([]);

const [Drawer, drawerApi] = useVbenDrawer({
  onCancel() {
    drawerApi.close();
  },
  onConfirm() {
    message.info('onConfirm');
    // drawerApi.close();
  },
  onOpenChange(isOpen) {
    if (isOpen) {
      handleUpdate(10);
    }
  },
});

function handleUpdate(len: number) {
  drawerApi.setState({ loading: true });
  setTimeout(() => {
    list.value = Array.from({ length: len }, (_v, k) => k + 1);
    drawerApi.setState({ loading: false });
  }, 2000);
}
</script>
<template>
  <Drawer title="자동 높이 계산">
    <div
      v-for="item in list"
      :key="item"
      class="flex-center h-55 w-full bg-muted even:bg-heavy"
    >
      {{ item }}
    </div>

    <template #prepend-footer>
      <Button type="link" @click="handleUpdate(6)">데이터 업데이트 클릭</Button>
    </template>
  </Drawer>
</template>
