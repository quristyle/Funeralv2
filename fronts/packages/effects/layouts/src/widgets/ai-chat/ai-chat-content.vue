<script setup lang="ts">
import { ref, nextTick, onMounted, computed, watch, triggerRef, inject } from 'vue';
import { Button, Input, Spin, List, Popconfirm, message } from 'ant-design-vue';
import { X, History, Plus, Trash2, AlertCircle, MessageSquare, Send } from '@vben/icons';
import { isAiChatPinned } from './state';

defineOptions({
  name: 'AiChatContent',
});

// 의존성 주입: apps 단에서 공급한 AI Chat Stream API 활용
const streamChatMessage = inject<(
  messages: ChatMessage[],
  callback: (content: string) => void
) => Promise<void>>('AI_CHAT_STREAM_API');

const STORAGE_KEY = 'vben_ai_chat_sessions';
const RETENTION_PERIOD = 7 * 24 * 60 * 60 * 1000; // 1주일

interface ChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

interface ChatSession {
  id: string;
  title: string;
  messages: ChatMessage[];
  updatedAt: number;
}

const sessions = ref<ChatSession[]>([]);
const currentSessionId = ref<string>('');
const inputMessage = ref('');
const isLoading = ref(false);
const showHistoryList = ref(false);
const chatContainer = ref<HTMLElement | null>(null);
const textareaRef = ref<{ focus: () => void } | null>(null);

/**
 * 질문 입력창에 포커스를 맞춥니다.
 */
function focusInput() {
  nextTick(() => {
    if (textareaRef.value) {
      textareaRef.value.focus();
    }
  });
}

const currentSession = computed(() =>
  sessions.value.find((s) => s.id === currentSessionId.value)
);

/**
 * 로컬 저장소 세션 로드 및 1주일 경과 삭제
 */
function loadSessions() {
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved) {
    try {
      const allSessions: ChatSession[] = JSON.parse(saved);
      const now = Date.now();
      const validSessions = allSessions.filter(
        (s) => now - s.updatedAt < RETENTION_PERIOD
      );
      sessions.value = validSessions;

      if (validSessions.length !== allSessions.length) {
        saveSessions();
      }

      if (validSessions.length > 0) {
        currentSessionId.value = validSessions.sort(
          (a, b) => b.updatedAt - a.updatedAt
        )[0]!.id;
      } else {
        createNewChat();
      }
    } catch (e) {
      console.error('Failed to parse chat sessions', e);
      createNewChat();
    }
  } else {
    createNewChat();
  }
}

function saveSessions() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions.value));
}

function createNewChat() {
  const newId = Date.now().toString();
  const newSession: ChatSession = {
    id: newId,
    title: `새 채팅 ${sessions.value.length + 1}`,
    messages: [
      {
        role: 'assistant',
        content:
          '안녕하세요! 저는 장례 관리 시스템(Funeral V2)의 AI 어시스턴트입니다.\n무엇을 도와드릴까요?',
      },
    ],
    updatedAt: Date.now(),
  };

  sessions.value.unshift(newSession);
  currentSessionId.value = newId;
  saveSessions();
  showHistoryList.value = false;
  focusInput();
}

function selectSession(id: string) {
  if (isLoading.value) {
    message.warning('답변이 작성되는 중에는 채팅을 변경할 수 없습니다.');
    return;
  }
  currentSessionId.value = id;
  showHistoryList.value = false;
  nextTick(() => {
    scrollToBottom(true);
    focusInput();
  });
}

function deleteSession(id: string) {
  if (isLoading.value && currentSessionId.value === id) {
    message.warning('현재 진행 중인 채팅은 삭제할 수 없습니다.');
    return;
  }
  const index = sessions.value.findIndex((s) => s.id === id);
  if (index !== -1) {
    sessions.value.splice(index, 1);
    saveSessions();

    if (currentSessionId.value === id) {
      if (sessions.value.length > 0) {
        currentSessionId.value = sessions.value[0]!.id;
      } else {
        createNewChat();
      }
    }
  }
}

function scrollToBottom(force = false) {
  nextTick(() => {
    setTimeout(() => {
      if (chatContainer.value) {
        const { scrollTop, scrollHeight, clientHeight } = chatContainer.value;
        const isNearBottom = scrollHeight - scrollTop - clientHeight <= 80;
        if (force || isNearBottom) {
          chatContainer.value.scrollTop = scrollHeight;
        }
      }
    }, 50);
  });
}

async function handleSend() {
  if (!inputMessage.value.trim() || isLoading.value || !currentSession.value) return;
  if (!streamChatMessage) {
    message.error('AI 스트리밍 API가 제공되지 않았습니다. 레이아웃을 확인해주세요.');
    return;
  }

  const userMsg = inputMessage.value.trim();
  const session = currentSession.value;

  session.messages.push({ role: 'user', content: userMsg });

  if (session.title.startsWith('새 채팅') && session.messages.length <= 3) {
    session.title =
      userMsg.substring(0, 15) + (userMsg.length > 15 ? '...' : '');
  }

  inputMessage.value = '';
  session.updatedAt = Date.now();

  scrollToBottom(true);
  isLoading.value = true;
  saveSessions();

  const assistantMsg: ChatMessage = { role: 'assistant', content: '' };
  session.messages.push(assistantMsg);

  let charQueue = '';
  let typeInterval: number | null = null;

  const processQueue = () => {
    if (charQueue.length > 0) {
      const charsToTake =
        charQueue.length > 40 ? 3 : charQueue.length > 15 ? 2 : 1;
      assistantMsg.content += charQueue.substring(0, charsToTake);
      charQueue = charQueue.substring(charsToTake);
      triggerRef(sessions);
      scrollToBottom(false);
    } else if (!isLoading.value) {
      if (typeInterval) {
        clearInterval(typeInterval);
        typeInterval = null;
      }
      session.updatedAt = Date.now();
      saveSessions();
    }
  };

  try {
    const history = session.messages.slice(0, -1).slice(-20);
    typeInterval = window.setInterval(processQueue, 20);

    await streamChatMessage(history, (content) => {
      charQueue += content;
    });
  } catch (error) {
    console.error('Chat Streaming Error:', error);
    assistantMsg.content =
      '⚠️ 죄송합니다. 응답을 생성하는 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.';
    triggerRef(sessions);
    scrollToBottom(true);
  } finally {
    isLoading.value = false;
  }
}

function handleKeyDown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    handleSend();
  }
}

function handleStorageChange(e: StorageEvent) {
  if (e.key === STORAGE_KEY) {
    loadSessions();
  }
}

onMounted(() => {
  loadSessions();
  window.addEventListener('storage', handleStorageChange);
  nextTick(() => {
    scrollToBottom(true);
    focusInput();
  });
});

watch(currentSessionId, () => {
  nextTick(() => {
    scrollToBottom(true);
  });
});

watch(showHistoryList, (newVal) => {
  if (!newVal) {
    focusInput();
  }
});
</script>

<template>
  <div class="flex h-full flex-col justify-between overflow-hidden bg-background">
    <!-- 헤더 영역 -->
    <div class="flex items-center justify-between border-b px-4 py-3 bg-muted/40 shrink-0">
      <div class="flex items-center gap-2">
        <span class="text-blue-600 text-base">💡</span>
        <span class="font-semibold text-foreground text-sm">AI 어시스턴트</span>
      </div>
      <div class="flex items-center gap-1.5">
        <!-- 채팅 기록 목록 아이콘 -->
        <VbenIconButton 
          :tooltip="showHistoryList ? '대화 화면으로' : '지난 대화 목록'" 
          class="size-7"
          :class="{ 'bg-accent text-primary': showHistoryList }"
          @click="showHistoryList = !showHistoryList"
        >
          <History class="size-3.5" />
        </VbenIconButton>
        <!-- 닫기 버튼 (사이드바 닫기) -->
        <VbenIconButton 
          tooltip="닫기" 
          class="size-7"
          @click="isAiChatPinned = false"
        >
          <X class="size-3.5" />
        </VbenIconButton>
      </div>
    </div>

    <!-- 메인 콘텐츠 영역: 리스트 토글 구조 -->
    <div class="flex-1 min-h-0 overflow-hidden flex flex-col relative">
      <!-- 1. 지난 대화 기록 목록 뷰 -->
      <div v-if="showHistoryList" class="flex-1 min-h-0 flex flex-col overflow-hidden">
        <div class="p-3 border-b shrink-0">
          <Button type="primary" block size="small" @click="createNewChat">
            <template #icon><Plus class="size-3" /></template>
            새 채팅 시작
          </Button>
        </div>
        <div class="flex-1 overflow-y-auto p-1 custom-scrollbar">
          <List item-layout="horizontal" :data-source="sessions">
            <template #renderItem="{ item }">
              <List.Item 
                :class="[
                  'cursor-pointer px-3 py-2 transition-colors group border-b border-border/40', 
                  currentSessionId === item.id ? 'bg-primary/5 border-r-2 border-primary' : 'hover:bg-accent'
                ]"
                @click="selectSession(item.id)"
              >
                <div class="flex items-center justify-between w-full overflow-hidden">
                  <div class="flex items-center gap-2 overflow-hidden">
                    <MessageSquare class="size-3.5 shrink-0 text-muted-foreground" />
                    <span class="truncate text-xs font-medium text-foreground">{{ item.title }}</span>
                  </div>
                  
                  <!-- 삭제 버튼 -->
                  <Popconfirm
                    title="이 대화를 삭제하시겠습니까?"
                    ok-text="삭제"
                    cancel-text="취소"
                    @confirm.stop="deleteSession(item.id)"
                  >
                    <template #icon><AlertCircle class="size-3.5" style="color: red" /></template>
                    <div 
                      class="opacity-0 group-hover:opacity-100 p-1 hover:bg-destructive/10 rounded transition-opacity"
                      @click.stop
                    >
                      <Trash2 class="size-3 text-destructive" />
                    </div>
                  </Popconfirm>
                </div>
              </List.Item>
            </template>
          </List>
        </div>
      </div>

      <!-- 2. 실시간 대화 화면 뷰 -->
      <div v-else class="flex-1 min-h-0 flex flex-col overflow-hidden">
        <!-- 말풍선 스크롤 영역 -->
        <div 
          ref="chatContainer" 
          class="flex-1 overflow-y-auto p-4 space-y-4 bg-background/50 custom-scrollbar"
        >
          <template v-if="currentSession">
            <div 
              v-for="(msg, index) in currentSession.messages" 
              :key="index" 
              :class="['flex', msg.role === 'user' ? 'justify-end' : 'justify-start']"
            >
              <!-- AI 프로필 아이콘 (사용자 아닐 때 노출) -->      
              <div 
                :class="[
                  'max-w-[95%] leading-relaxed ', 
                  msg.role === 'user' 
                    ? 'p-3 bg-primary text-primary-foreground rounded-br-sm border-primary' 
                    : 'bg-card text-card-foreground rounded-bl-sm border-border'
                ]"
              >
                <pre class="whitespace-pre-wrap m-0 text-inherit font-sans">{{ msg.content }}</pre>
              </div>
            </div>
          </template>
          
          <!-- 로딩 스피너 -->
          <div v-if="isLoading" class="flex justify-start">
            <div class="flex size-6 shrink-0 items-center justify-center rounded-full bg-primary text-primary-foreground text-[9px] font-bold mr-2 mt-0.5 animate-pulse">
              AI
            </div>
            <div class="p-3 rounded-xl rounded-bl-sm shadow-sm flex items-center gap-2 border bg-card border-border">
              <Spin size="small" /> 
              <span class="text-xs font-medium animate-pulse text-muted-foreground">답변을 작성하는 중...</span>
            </div>
          </div>
        </div>

        <!-- 입력 영역 -->
        <div class="p-3 flex gap-2 items-end border-t border-border bg-card shrink-0">
          <Input.TextArea
            ref="textareaRef"
            v-model:value="inputMessage"
            :auto-size="{ minRows: 1, maxRows: 4 }"
            placeholder="메시지 입력 (Enter로 전송)"
            class="flex-1 rounded-lg text-xs transition-all duration-200 border-border focus:border-primary bg-background custom-textarea"
            :disabled="!currentSession"
            @keydown="handleKeyDown"
          />
          <Button 
            type="primary" 
            size="small"
            class="h-7 w-7 rounded-lg p-0 flex items-center justify-center shadow-md transition-all duration-200 shrink-0"
            :loading="isLoading" 
            :disabled="!inputMessage.trim() || isLoading || !currentSession"
            @click="handleSend"
          >
            <Send v-if="!isLoading" class="size-3.5" />
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* 가비지 마진 및 폰트 레이아웃 정돈 */
pre {
  white-space: pre-wrap;
  word-break: break-all;
}

/* 커스텀 스크롤바 디자인 */
.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background-color: hsl(var(--muted) / 30%);
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: hsl(var(--muted-foreground) / 40%);
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background-color: hsl(var(--muted-foreground) / 60%);
}

.custom-textarea :deep(textarea) {
  padding: 6px 12px !important;
  font-size: 12px !important;
}
</style>
