<script lang="ts" setup>
import { useVbenModal } from '@vben/common-ui';

import { Button, message } from 'ant-design-vue';

const [Modal, modalApi] = useVbenModal({
  onCancel() {
    modalApi.close();
  },
  onClosed() {
    message.info('onClosed: 닫기 애니메이션 종료');
  },
  onConfirm() {
    message.info('onConfirm');
    // modalApi.close();
  },
  onOpened() {
    message.info('onOpened: 열기 애니메이션 종료');
  },
});

function lockModal() {
  modalApi.lock();
  setTimeout(() => {
    modalApi.unlock();
  }, 3000);
}
</script>
<template>
  <Modal class="w-150" title="기본 팝업 예제" title-tooltip="제목 툴팁 내용">
    base demo
    <Button type="primary" @click="lockModal">팝업 잠금</Button>
  </Modal>
</template>
