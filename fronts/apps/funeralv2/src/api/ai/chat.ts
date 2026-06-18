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

/**
 * AI 어시스턴트와 실시간 스트리밍 채팅
 * @param messages 대화 내역 배열
 * @param onUpdate 데이터 조각을 받을 때마다 실행할 콜백 함수
 */
export async function streamChatMessage(
  messages: ChatMessage[], 
  onUpdate: (content: string) => void
) {
  const response = await fetch('/api/ai/chat/stream', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'text/event-stream',
    },
    body: JSON.stringify({ messages }),
  });

  if (!response.ok) {
    throw new Error('Streaming request failed');
  }

  const reader = response.body?.getReader();
  if (!reader) return;

  const decoder = new TextDecoder();
  let buffer = '';

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });
    
    let boundary = buffer.indexOf('\n\n');
    while (boundary !== -1) {
      const chunk = buffer.substring(0, boundary);
      buffer = buffer.substring(boundary + 2);

      const lines = chunk.split('\n');
      for (const line of lines) {
        if (line.startsWith('data: ')) {
          const dataStr = line.substring(6).trim();
          if (dataStr === '[DONE]') return;
          if (dataStr) {
            try {
              // "\uC218" 와 같이 쌍따옴표로 감싸진 JSON 문자열을 파싱
              const parsedContent = JSON.parse(dataStr);
              onUpdate(parsedContent);
            } catch (e) {
              console.warn('Failed to parse SSE chunk:', dataStr, e);
            }
          }
        }
      }
      boundary = buffer.indexOf('\n\n');
    }
  }
}
