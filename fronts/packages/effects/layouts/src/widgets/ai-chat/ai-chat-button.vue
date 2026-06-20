<script lang="ts" setup>
import { Sparkles } from '@vben/icons';
import { VbenIconButton } from '@vben-core/shadcn-ui';
import { isAiChatPinned } from './state';

defineOptions({
  name: 'AiChatButton',
});

function handleToggleChat() {
  isAiChatPinned.value = !isAiChatPinned.value;
}
</script>

<template>
  <VbenIconButton class="hover:text-primary" @click="handleToggleChat">
    <!-- AI 채팅 활성화 여부에 따라 아이콘 컬러 강조로 시각적 피드백 제공 -->
    <Sparkles 
      class="size-4 transition-colors" 
      :class="isAiChatPinned ? 'text-primary font-bold' : 'text-foreground hover:text-primary'"
    />
  </VbenIconButton>
</template>
