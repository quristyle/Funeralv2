import { requestClient } from '#/api/request';

import { currentAiModel, currentAiProvider } from './provider';

export interface ChatMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

/**
 * AI 어시스턴트와 일반 채팅
 * @param messages 다중 턴 대화 내역 배열
 *
 * `provider` 는 사용자가 환경설정에서 고른 AI 모델이다. 부르는 쪽이 신경 쓰지
 * 않도록 여기서 읽어 붙인다(`provider.ts` 주석 참고).
 */
export function sendChatMessage(messages: ChatMessage[]) {
  // 로컬 LLM 응답이 생성되는 데 시간이 오래 걸릴 수 있으므로
  // 기본 10초 타임아웃을 60초(60000ms)로 넉넉하게 늘립니다.
  return requestClient.post<string>('/ai/chat', {
    messages,
    provider: currentAiProvider(),
    model: currentAiModel(),
  }, {
    timeout: 60000,
  });
}

/** 서버가 답과 함께 보내는 안내 한 줄. **답 글자가 아니다.** */
export interface ChatNotice {
  /** 사람에게 보여 줄 문장. */
  notice: string;
  /**
   * `provider` 다른 공급자로 전환 / `model` 한도로 모델 전환 /
   * `history` 길이 제한으로 오래된 대화를 보내지 않음
   */
  kind?: string;
}

/**
 * AI 어시스턴트와 실시간 스트리밍 채팅
 * @param messages 대화 내역 배열
 * @param onUpdate 답 조각을 받을 때마다 실행할 콜백 함수
 * @param onNotice 안내를 받을 때 실행할 콜백 함수
 *
 * [안내를 답과 갈라서 받는다]
 *
 * 서버는 답 글자를 JSON **문자열**로, 안내를 JSON **객체**로 보낸다.
 * 안내를 답에 이어 붙이면 **화면이 그것을 답의 일부로 저장하고 다음 턴 문맥으로
 * 다시 올려보낸다** — 모델이 자기가 하지 않은 말을 자기 말로 읽는다.
 * 그래서 갈라 받아 말풍선 밖에 보여 주고 기록에는 넣지 않는다.
 */
export async function streamChatMessage(
  messages: ChatMessage[],
  onUpdate: (content: string) => void,
  onNotice?: (notice: ChatNotice) => void,
) {
  const response = await fetch('/api/ai/chat/stream', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'text/event-stream',
    },
    body: JSON.stringify({
      messages,
      provider: currentAiProvider(),
      model: currentAiModel(),
    }),
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
              const parsed = JSON.parse(dataStr);

              // 문자열이면 답 글자, 객체면 안내다. 옛 서버는 문자열만 보내므로
              // 이렇게 갈라 두면 서버가 먼저 올라가도 나중에 올라가도 깨지지 않는다.
              if (typeof parsed === 'string') {
                onUpdate(parsed);
              } else if (parsed && typeof parsed.notice === 'string') {
                onNotice?.({ notice: parsed.notice, kind: parsed.kind });
              }
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
