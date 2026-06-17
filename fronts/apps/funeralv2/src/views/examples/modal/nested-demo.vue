<script lang="ts" setup>
import { useVbenModal } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

import DragDemo from './drag-demo.vue';

const [Modal] = useVbenModal({
  destroyOnClose: true,
});
const [BaseModal, baseModalApi] = useVbenModal({
  connectedComponent: DragDemo,
});

function openNestedModal() {
  baseModalApi.open();
}
</script>
<template>
  <Modal title="중첩 팝업 예제">
    <Button @click="openNestedModal" type="primary">하위 팝업 열기</Button>
    <BaseModal />
  </Modal>
</template>
