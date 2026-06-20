<script lang="ts" setup>
import { onMounted } from 'vue';
import { useIsMobile } from '@vben/hooks';
import { useCommonCode } from './composables/use-common-code';
import CommonCodeDesktop from './modules/common-code-desktop.vue';
import CommonCodeMobile from './modules/common-code-mobile.vue';

// 전역 반응형 모바일 감지
const { isMobile } = useIsMobile();

// 비즈니스 데이터 및 공통 훅 로드
const codeContext = useCommonCode();

onMounted(() => {
  codeContext.loadGroups();
});
</script>

<template>
  <CommonCodeMobile v-if="isMobile" :context="codeContext" />
  <CommonCodeDesktop v-else :context="codeContext" />
</template>
