<script lang="ts" setup>
import { useVbenModal } from '@vben/common-ui';

import { Button, message } from 'ant-design-vue';

const [Modal, modalApi] = useVbenModal({
  draggable: true,
  onCancel() {
    modalApi.close();
  },
  onConfirm() {
    message.info('onConfirm');
    // modalApi.close();
  },
  title: '동적 설정 수정 예제',
});

const state = modalApi.useStore();

function handleUpdateTitle() {
  modalApi.setState({ title: '내부 동적 제목' });
}

function handleToggleFullscreen() {
  modalApi.setState((prev) => {
    return { ...prev, fullscreen: !prev.fullscreen };
  });
}
</script>
<template>
  <Modal>
    <div class="flex-col-center">
      <Button class="mb-3" type="primary" @click="handleUpdateTitle()">
        내부에서 동적으로 제목 수정
      </Button>
      <Button class="mb-3" type="primary" @click="handleToggleFullscreen()">
        {{ state.fullscreen ? '전체 화면 종료' : '전체 화면 열기' }}
      </Button>
    </div>
  </Modal>
</template>
