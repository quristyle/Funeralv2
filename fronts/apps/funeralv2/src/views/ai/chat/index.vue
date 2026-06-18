<script lang="ts" setup>
import { ref, nextTick, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Card, Input, Spin, message } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import { sendChatMessage, type ChatMessage } from '#/api/ai/chat';

const messages = ref<ChatMessage[]>([]);
const inputMessage = ref('');
const isLoading = ref(false);
const chatContainer = ref<HTMLElement | null>(null);

/**
 * 스크롤을 항상 가장 아래로 이동시킵니다.
 */
function scrollToBottom() {
  nextTick(() => {
    if (chatContainer.value) {
      chatContainer.value.scrollTop = chatContainer.value.scrollHeight;
    }
  });
}

/**
 * 메시지 전송 로직
 */
async function handleSend() {
  if (!inputMessage.value.trim() || isLoading.value) return;

  const userMsg = inputMessage.value.trim();
  messages.value.push({ role: 'user', content: userMsg });
  inputMessage.value = '';

  scrollToBottom();
  isLoading.value = true;

  try {
    // 문맥 유지를 위해 최근 10개의 메시지만 전송하여 토큰 한도 초과 방지
    const history = messages.value.slice(-10);
    const response = await sendChatMessage(history);
    
    messages.value.push({ role: 'assistant', content: response });
  } catch (error) {
    console.error('Chat API Error:', error);
    messages.value.push({ 
      role: 'assistant', 
      content: '⚠️ 죄송합니다. 응답을 생성하는 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.' 
    });
  } finally {
    isLoading.value = false;
    scrollToBottom();
  }
}

/**
 * 줄바꿈 방지 및 전송 처리 (Shift + Enter 또는 Ctrl + Enter 처리도 가능, 여기서는 기본 Enter 전송 적용)
 */
function handleKeyDown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    handleSend();
  }
}

onMounted(() => {
  // 초기 인사말
  messages.value.push({ 
    role: 'assistant', 
    content: '안녕하세요! 저는 장례 관리 시스템(Funeral V2)의 AI 어시스턴트입니다.\n무엇을 도와드릴까요?' 
  });
});
</script>

<template>
  <Page title="AI 어시스턴트" description="로컬 LLM을 활용한 다목적 일반 채팅 화면입니다. (문맥 기반 대화 지원)">
    <Card class="flex flex-col h-[700px] border-0 rounded-xl overflow-hidden">
      <!-- 대화 목록 영역 -->
      <div 
        ref="chatContainer" 
        class="flex-1 overflow-y-auto p-6 space-y-6"
      >
        <div 
          v-for="(msg, index) in messages" 
          :key="index" 
          :class="['flex', msg.role === 'user' ? 'justify-end' : 'justify-start']"
        >
          <div 
            :class="[
              'max-w-[75%] p-4 rounded-2xl shadow-sm text-sm leading-relaxed border', 
              msg.role === 'user' 
                ? 'bg-primary text-primary-foreground rounded-br-sm border-primary' 
                : 'bg-card text-card-foreground rounded-bl-sm border-border'
            ]"
          >
            <pre class="whitespace-pre-wrap font-sans m-0">{{ msg.content }}</pre>
          </div>
        </div>
        
        <!-- 로딩 인디케이터 -->
        <div v-if="isLoading" class="flex justify-start">
          <div 
            class="p-4 rounded-2xl rounded-bl-sm shadow-sm flex items-center gap-3 border bg-card border-border"
          >
            <Spin size="small" /> 
            <span class="text-sm font-medium animate-pulse text-muted-foreground">답변을 작성하는 중...</span>
          </div>
        </div>
      </div>

      <!-- 입력 영역 -->
      <div class="p-4 flex gap-3 items-end border-t border-border bg-card">
        <Input.TextArea
          v-model:value="inputMessage"
          :auto-size="{ minRows: 1, maxRows: 4 }"
          placeholder="메시지를 입력하세요 (Enter로 전송, Shift+Enter로 줄바꿈)"
          class="flex-1 rounded-lg text-sm transition-all duration-200"
          @keydown="handleKeyDown"
        />
        <Button 
          type="primary" 
          size="large"
          class="h-auto rounded-lg px-6 flex items-center justify-center shadow-md transition-all duration-200 disabled:opacity-50"
          :loading="isLoading" 
          :disabled="!inputMessage.trim() || isLoading"
          @click="handleSend"
        >
          <span v-if="!isLoading" class="font-medium mr-2">전송</span>
          <IconifyIcon v-if="!isLoading" icon="lucide:send" class="size-4" />
        </Button>
      </div>
    </Card>
  </Page>
</template>

<style scoped>
/* 커스텀 스크롤바 디자인 - 테마 호환성을 위해 연한 회색/투명 계열 사용 */
.overflow-y-auto::-webkit-scrollbar {
  width: 6px;
}
.overflow-y-auto::-webkit-scrollbar-track {
  background: transparent;
}
.overflow-y-auto::-webkit-scrollbar-thumb {
  background-color: rgba(128, 128, 128, 0.3);
  border-radius: 10px;
}
</style>
