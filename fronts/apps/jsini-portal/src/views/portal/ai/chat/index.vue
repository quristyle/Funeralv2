<script lang="ts" setup>
import { ref, nextTick, onMounted, computed, watch, triggerRef } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Card, Input, Spin, message, List, Popconfirm, Tooltip } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import { streamChatMessage, type ChatMessage } from '#/api/ai/chat';

/**
 * 채팅 세션 타입 정의
 */
interface ChatSession {
  id: string;
  title: string;
  messages: ChatMessage[];
  updatedAt: number;
}

const STORAGE_KEY = 'vben_ai_chat_sessions';
const RETENTION_PERIOD = 7 * 24 * 60 * 60 * 1000; // 1주일 (밀리초)

const sessions = ref<ChatSession[]>([]);
const currentSessionId = ref<string>('');
const inputMessage = ref('');
const isLoading = ref(false);
const chatContainer = ref<HTMLElement | null>(null);

/**
 * 현재 선택된 세션 계산
 */
const currentSession = computed(() => 
  sessions.value.find(s => s.id === currentSessionId.value)
);

/**
 * 로컬 저장소에서 세션 데이터 로드 및 1주일 경과 데이터 삭제
 */
function loadSessions() {
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved) {
    try {
      const allSessions: ChatSession[] = JSON.parse(saved);
      const now = Date.now();
      
      // 1주일이 지난 세션은 필터링하여 삭제
      const validSessions = allSessions.filter(s => (now - s.updatedAt) < RETENTION_PERIOD);
      sessions.value = validSessions;
      
      // 저장소 동기화 (삭제된 데이터 반영)
      if (validSessions.length !== allSessions.length) {
        saveSessions();
      }

      // 마지막으로 업데이트된 세션을 기본 선택
      if (validSessions.length > 0) {
        currentSessionId.value = validSessions.sort((a, b) => b.updatedAt - a.updatedAt)[0]!.id;
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

/**
 * 로컬 저장소에 현재 세션 상태 저장
 */
function saveSessions() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions.value));
}

/**
 * 새로운 채팅 세션 생성
 */
function createNewChat() {
  const newId = Date.now().toString();
  const newSession: ChatSession = {
    id: newId,
    title: `새 채팅 ${sessions.value.length + 1}`,
    messages: [
      { 
        role: 'assistant', 
        content: '안녕하세요! 저는 장례 관리 시스템(Funeral V2)의 AI 어시스턴트입니다.\n무엇을 도와드릴까요?' 
      }
    ],
    updatedAt: Date.now()
  };
  
  sessions.value.unshift(newSession);
  currentSessionId.value = newId;
  saveSessions();
}

/**
 * 채팅 세션 선택
 */
function selectSession(id: string) {
  if (isLoading.value) {
    message.warning('답변이 작성되는 중에는 채팅을 변경할 수 없습니다.');
    return;
  }
  currentSessionId.value = id;
  // 세션이 전환될 때 항상 하단으로 강제 스크롤
  nextTick(() => {
    scrollToBottom(true);
  });
}

/**
 * 채팅 세션 삭제
 */
function deleteSession(id: string) {
  if (isLoading.value && currentSessionId.value === id) {
    message.warning('현재 진행 중인 채팅은 삭제할 수 없습니다.');
    return;
  }
  const index = sessions.value.findIndex(s => s.id === id);
  if (index !== -1) {
    sessions.value.splice(index, 1);
    saveSessions();
    
    // 삭제된 세션이 현재 세션인 경우 다른 세션 선택
    if (currentSessionId.value === id) {
      if (sessions.value.length > 0) {
        currentSessionId.value = sessions.value[0]!.id;
      } else {
        createNewChat();
      }
    }
  }
}

/**
 * 스크롤을 하단으로 이동시킵니다.
 * @param force true일 경우 사용자의 스크롤 위치와 무관하게 무조건 하단으로 이동합니다.
 */
function scrollToBottom(force: boolean = false) {
  nextTick(() => {
    // 렌더링이 완료된 후 스크롤을 이동시키기 위해 약간의 지연 시간(50ms) 추가
    setTimeout(() => {
      if (chatContainer.value) {
        const { scrollTop, scrollHeight, clientHeight } = chatContainer.value;
        // 스크롤이 맨 아래에서 80px 이내에 있을 때만 자동 스크롤
        const isNearBottom = scrollHeight - scrollTop - clientHeight <= 80;
        
        if (force || isNearBottom) {
          chatContainer.value.scrollTop = scrollHeight;
        }
      }
    }, 50);
  });
}

/**
 * 메시지 전송 로직 (스트리밍 및 부드러운 타이핑 방식)
 */
async function handleSend() {
  if (!inputMessage.value.trim() || isLoading.value || !currentSession.value) return;

  const userMsg = inputMessage.value.trim();
  const session = currentSession.value;

  // 1. 사용자 메시지 추가
  session.messages.push({ role: 'user', content: userMsg });
  
  if (session.title.startsWith('새 채팅') && session.messages.length <= 3) {
    session.title = userMsg.substring(0, 15) + (userMsg.length > 15 ? '...' : '');
  }

  inputMessage.value = '';
  session.updatedAt = Date.now();
  
  scrollToBottom(true); // 질문 전송 시 강제 하단 이동
  isLoading.value = true;
  saveSessions();

  // 2. AI의 답변을 위한 빈 메시지 객체 생성
  const assistantMsg: ChatMessage = { role: 'assistant', content: '' };
  session.messages.push(assistantMsg);

  // --- 부드러운 타이핑을 위한 큐(Queue) 제어 변수 ---
  let charQueue = '';
  let typeInterval: number | null = null;

  // 큐에 있는 글자를 화면에 일정 간격으로 렌더링하는 함수
  const processQueue = () => {
    if (charQueue.length > 0) {
      // 자연스러운 타이핑 속도 조절
      const charsToTake = charQueue.length > 40 ? 3 : (charQueue.length > 15 ? 2 : 1);
      
      assistantMsg.content += charQueue.substring(0, charsToTake);
      charQueue = charQueue.substring(charsToTake);
      
      triggerRef(sessions);
      scrollToBottom(false); // 스트리밍 중에는 스마트 스크롤(80px 기준) 적용
    } else if (!isLoading.value) {
      // 로딩(통신)이 끝났고 큐도 비었으면 타이머 종료
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

    // 타이머 시작 (약 20ms 간격으로 화면 업데이트, 부드러움 극대화)
    typeInterval = window.setInterval(processQueue, 20);

    await streamChatMessage(history, (content) => {
      // 통신으로 받은 조각을 즉시 화면에 그리지 않고 큐에만 누적
      charQueue += content;
    });

  } catch (error) {
    console.error('Chat Streaming Error:', error);
    assistantMsg.content = '⚠️ 죄송합니다. 응답을 생성하는 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.';
    triggerRef(sessions);
    scrollToBottom(true);
  } finally {
    isLoading.value = false;
  }
}

/**
 * Enter 키 전송 처리
 */
function handleKeyDown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    handleSend();
  }
}

onMounted(() => {
  loadSessions();
  // 초기 로드 후 마지막 대화가 보이도록 하단 스크롤
  nextTick(() => {
    scrollToBottom(true);
  });
});

// 세션 변경 시 스크롤 하단 이동
watch(currentSessionId, () => {
  nextTick(() => {
    scrollToBottom(true);
  });
});
</script>

<template>
  <Page auto-content-height title="AI 어시스턴트" description="로컬 저장소 기반의 다중 채팅 환경을 제공합니다. (데이터는 1주일간 보관됩니다)">
    <div class="flex gap-4 h-full overflow-hidden p-1">
      
      <!-- 사이드바: 채팅 목록 -->
      <div class="w-72 flex flex-col border border-border rounded-xl shadow-sm overflow-hidden bg-card">
        <div class="p-4 border-b border-border">
          <Button type="primary" block @click="createNewChat">
            <template #icon><IconifyIcon icon="lucide:plus" class="size-4" /></template>
            새 채팅 시작
          </Button>
        </div>
        
        <div class="flex-1 overflow-y-auto">
          <List item-layout="horizontal" :data-source="sessions">
            <template #renderItem="{ item }">
              <List.Item 
                :class="[
                  'cursor-pointer px-4 py-3 transition-colors group', 
                  currentSessionId === item.id ? 'bg-primary/10 border-r-4 border-primary' : 'hover:bg-accent'
                ]"
                @click="selectSession(item.id)"
              >
                <div class="flex items-center justify-between w-full overflow-hidden">
                  <div class="flex items-center gap-2 overflow-hidden">
                    <IconifyIcon icon="lucide:message-square" class="size-4 shrink-0 text-muted-foreground" />
                    <span class="truncate text-sm font-medium">{{ item.title }}</span>
                  </div>
                  
                  <!-- 삭제 버튼 (호버 시 표시) -->
                  <Popconfirm
                    title="이 대화를 삭제하시겠습니까?"
                    @confirm.stop="deleteSession(item.id)"
                  >
                    <template #icon><IconifyIcon icon="lucide:alert-circle" style="color: red" /></template>
                    <div 
                      class="opacity-0 group-hover:opacity-100 p-1 hover:bg-destructive/10 rounded transition-opacity"
                      @click.stop
                    >
                      <IconifyIcon icon="lucide:trash-2" class="size-3.5 text-destructive" />
                    </div>
                  </Popconfirm>
                </div>
              </List.Item>
            </template>
          </List>
        </div>
      </div>

      <!-- 메인: 채팅창 -->
      <div class="flex-1 flex flex-col border border-border rounded-xl shadow-sm overflow-hidden bg-card">
        <!-- 대화 목록 영역 -->
        <div 
          ref="chatContainer" 
          class="flex-1 overflow-y-auto p-6 space-y-6 bg-background/50 custom-scrollbar"
        >
          <template v-if="currentSession">
            <div 
              v-for="(msg, index) in currentSession.messages" 
              :key="index" 
              :class="['flex', msg.role === 'user' ? 'justify-end' : 'justify-start']"
            >
              <div 
                :class="[
                  'max-w-[80%] p-4 rounded-2xl shadow-sm  leading-relaxed border', 
                  msg.role === 'user' 
                    ? 'bg-primary text-primary-foreground rounded-br-sm border-primary' 
                    : 'bg-card text-card-foreground rounded-bl-sm border-border'
                ]"
              >
                <pre class="whitespace-pre-wrap  m-0 text-inherit">{{ msg.content }}</pre>
              </div>
            </div>
          </template>
          
          <!-- 로딩 인디케이터 -->
          <div v-if="isLoading" class="flex justify-start">
            <div 
              class="p-4 rounded-2xl rounded-bl-sm shadow-sm flex items-center gap-3 border bg-card border-border"
            >
              <Spin size="small" /> 
              <span class=" font-medium animate-pulse text-muted-foreground">답변을 작성하는 중...</span>
            </div>
          </div>
        </div>

        <!-- 입력 영역 -->
        <div class="p-4 flex gap-3 items-end border-t border-border bg-card">
          <Input.TextArea
            v-model:value="inputMessage"
            :auto-size="{ minRows: 1, maxRows: 4 }"
            placeholder="메시지를 입력하세요 (Enter로 전송, Shift+Enter로 줄바꿈)"
            class="flex-1 rounded-lg text-sm transition-all duration-200 border-border focus:border-primary bg-background"
            :disabled="!currentSession"
            @keydown="handleKeyDown"
          />
          <Button 
            type="primary" 
            size="large"
            class="h-10 rounded-lg px-6 flex items-center justify-center shadow-md transition-all duration-200"
            :loading="isLoading" 
            :disabled="!inputMessage.trim() || isLoading || !currentSession"
            @click="handleSend"
          >
            <span v-if="!isLoading" class="font-medium mr-2">전송</span>
            <IconifyIcon v-if="!isLoading" icon="lucide:send" class="size-4" />
          </Button>
        </div>
      </div>
    </div>
  </Page>
</template>

<style scoped>
/* 커스텀 스크롤바 디자인 - 가시성 확보 */
.custom-scrollbar::-webkit-scrollbar {
  width: 8px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background-color: hsl(var(--muted) / 30%);
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: hsl(var(--muted-foreground) / 40%);
  border-radius: 4px;
  border: 2px solid transparent;
  background-clip: content-box;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background-color: hsl(var(--muted-foreground) / 60%);
}

/* 사이드바 스크롤바 디자인 */
.overflow-y-auto::-webkit-scrollbar {
  width: 5px;
}
.overflow-y-auto::-webkit-scrollbar-track {
  background: transparent;
}
.overflow-y-auto::-webkit-scrollbar-thumb {
  background-color: hsl(var(--muted-foreground) / 20%);
  border-radius: 4px;
}

/* 애니메이션 효과 */
.bg-accent {
  background-color: rgba(0, 0, 0, 0.02);
}

pre {
  white-space: pre-wrap;
  word-break: break-all;
}
</style>