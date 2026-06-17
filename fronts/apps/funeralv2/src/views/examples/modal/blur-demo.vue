<script lang="ts" setup>
import { ref, watch } from 'vue';

import { useVbenModal } from '@vben/common-ui';

import { Slider } from 'ant-design-vue';

const blur = ref(5);
const [Modal, modalApi] = useVbenModal({
  overlayBlur: blur.value,
});
watch(blur, (val) => {
  modalApi.setState({
    overlayBlur: val,
  });
});
</script>
<template>
  <Modal title="마스크 레이어 블러">
    <p>슬라이더를 조절하여 마스크 레이어의 블러 정도를 변경하세요: {{ blur }}</p>
    <Slider v-model:value="blur" :max="30" :min="0" />
  </Modal>
</template>
