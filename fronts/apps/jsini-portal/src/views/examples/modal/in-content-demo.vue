<script lang="ts" setup>
import { ref } from 'vue';

import { useVbenModal } from '@vben/common-ui';

import { Input, message } from 'ant-design-vue';

const [Modal, modalApi] = useVbenModal({
  destroyOnClose: false,
  onCancel() {
    modalApi.close();
  },
  onConfirm() {
    message.info('onConfirm');
    // modalApi.close();
  },
});
const value = ref();
</script>
<template>
  <Modal
    append-to-main
    class="w-150"
    title="기본 팝업 예제"
    title-tooltip="제목 툴팁 내용"
  >
    이 팝업은 콘텐츠 영역에서 열리도록 지정되었으며, 닫은 후에도 팝업 콘텐츠가 파괴되지 않습니다.
    <Input v-model:value="value" placeholder="KeepAlive 테스트" />
  </Modal>
</template>
