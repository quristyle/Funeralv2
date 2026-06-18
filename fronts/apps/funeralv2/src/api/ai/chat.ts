import { requestClient } from '#/api/request';

export interface ChatMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

/**
 * AI 어시스턴트와 일반 채팅
 * @param messages 다중 턴 대화 내역 배열
 */
export function sendChatMessage(messages: ChatMessage[]) {
  // 로컬 LLM 응답이 생성되는 데 시간이 오래 걸릴 수 있으므로 
  // 기본 10초 타임아웃을 60초(60000ms)로 넉넉하게 늘립니다.
  return requestClient.post<string>('/ai/chat', { messages }, {
    timeout: 60000,
  });
}
