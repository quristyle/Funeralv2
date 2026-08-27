import { preferencesManager } from '@vben/preferences';

import { requestClient } from '#/api/request';
import {
  AI_OPENROUTER_MODEL_KEY,
  AI_PROVIDER_KEY,
  AI_PROVIDERS,
} from '#/preferences';

/**
 * [지금 쓸 AI 공급자]
 *
 * 사용자가 환경설정에서 고른 값이다(`preferences.ts` 의 확장 항목).
 * AI 요청을 보내는 곳마다 이 값을 함께 보내면 서버가 그 공급자로 처리한다.
 *
 * **호출하는 화면이 신경 쓰지 않게 API 함수 안에서 부른다.** 화면마다
 * "환경설정을 읽어서 넘겨라" 를 기억해야 하면 언젠가 한 곳을 빠뜨리고,
 * 그 화면만 조용히 다른 모델로 도는 일이 생긴다. 실제로 떠 있는 AI 채팅
 * 위젯은 레이아웃이 `provide` 로 함수를 넘겨 주는 구조라 화면이 직접 부르지도 않는다.
 */
export function currentAiProvider(): string {
  const custom = preferencesManager.getCustomPreferences() as Record<
    string,
    unknown
  >;
  const selected = custom?.[AI_PROVIDER_KEY];

  // 값이 없거나 모르는 모양이면 기본으로 둔다. 서버도 모르는 값은 기본으로
  // 되돌리므로 여기서 막지 않아도 되지만, 쓸데없는 값을 보내지는 않는다.
  return typeof selected === 'string' && selected
    ? selected
    : AI_PROVIDERS.jsini;
}

/**
 * [지금 쓸 모델] — 모델을 고를 수 있는 공급자일 때만 값이 있다.
 *
 * 지금은 OpenRouter 만 해당한다(무료 모델 여러 개 중에서 고른다).
 *
 * **다른 공급자에게는 보내지 않는다.** 모델 이름은 공급자마다 전혀 다르므로,
 * OpenRouter 모델 이름(`google/gemma-4-31b-it:free`)을 Groq 에 보내면 400 이 된다.
 * 공급자를 바꿔도 모델 설정은 그대로 남아 있기 때문에 반드시 걸러야 한다.
 *
 * 이 값이 안전을 보장하지는 않는다 — 무료 여부는 **서버가** 확인한다.
 */
export function currentAiModel(): string | undefined {
  if (currentAiProvider() !== AI_PROVIDERS.openrouter) return undefined;

  const custom = preferencesManager.getCustomPreferences() as Record<
    string,
    unknown
  >;
  const model = custom?.[AI_OPENROUTER_MODEL_KEY];

  return typeof model === 'string' && model ? model : undefined;
}

/**
 * 공급자가 응답 헤더로 알려 준 사용량. 마지막으로 관측한 값이다.
 *
 * **사용량을 알려고 따로 호출하지 않는다** — 그 호출 자체가 무료 한도를 깎는다.
 * 실제 요청이 오갈 때 지나가는 헤더를 서버가 주워 둔 것이다. 그래서
 * 한 번도 부르지 않았으면 `usage` 자체가 없고, 값이 있어도 `observedAt` 기준의
 * 과거 값이다. 화면은 "몇 분 전 기준" 을 함께 보여 준다.
 */
export interface AiProviderUsage {
  callsOk: number;
  callsFailed: number;
  lastCallAt: null | string;
  lastLatencyMs: null | number;
  /** 하루 요청 상한 (Groq 무료: 1000) */
  limitRequests: null | string;
  remainingRequests: null | string;
  /** 분당 토큰 상한 (Groq 무료: 8000) */
  limitTokens: null | string;
  remainingTokens: null | string;
  /** 초기화까지 남은 시간 문자열 (예: `12m57.599s`) */
  resetRequests: null | string;
  resetTokens: null | string;
  /** 위 한도 값을 관측한 시각. null 이면 한도를 알려 주지 않는 공급자다(로컬 LLM). */
  observedAt: null | string;
}

/** 서버가 알려 주는 공급자 한 곳의 상태. */
export interface AiProviderInfo {
  key: string;
  displayName: string;
  model: string;
  /** 주소 · 키 · 모델이 다 채워져 실제로 부를 수 있는 상태인지. */
  configured: boolean;
  isDefault: boolean;
  /** 한 응답에 허용한 최대 토큰. */
  maxTokens: number;
  /** 사용자가 이 공급자의 모델을 고를 수 있는지 (OpenRouter 만). */
  allowModelChoice: boolean;
  /** 무료 모델만 쓰도록 서버가 강제하는지 (OpenRouter 만). */
  requireFreeModel: boolean;
  /** 생성(응답)을 기다리는 시간. 접속 실패와는 별개다. */
  timeoutSeconds: number;
  /** 접속까지 기다리는 시간. 공급자 공통이며, 껐으면 null. */
  connectTimeoutSeconds: null | number;
  /** 우리 쪽 하루 상한. 0 이면 세지 않는다. */
  maxRequestsPerDay: number;
  usedToday: number;
  /**
   * 한도에 걸렸을 때 대신 쓸 무료 모델들. 적힌 순서대로 시도한다.
   *
   * **비어 있으면 바꿔치기를 하지 않는다** — 예전처럼 한도 초과로 끝난다.
   * 즉 이 값이 그 기능의 켜짐/꺼짐이다.
   */
  fallbackModels: string[];
  /** 한 번의 질문에 시도할 모델 수의 상한(첫 모델을 포함한다). */
  maxModelAttempts: number;
  /** 한 번도 부르지 않았으면 null. */
  usage: AiProviderUsage | null;
}

/**
 * 서버에 설정된 AI 공급자 목록.
 *
 * 환경설정의 선택 목록은 고정이지만(사람이 읽는 이름이 필요하다), **정말로 쓸 수
 * 있는 상태인지는 서버만 안다** — Groq 키를 안 넣었으면 골라도 동작하지 않는다.
 * 그것을 화면에서 미리 알려 주는 데 쓴다.
 */
export async function getAiProviders() {
  const res = await requestClient.get<any>('/ai/providers');

  // 응답이 `ApiResponse` 로 한 겹 싸여 온다 — 목록형 응답은 `result` 배열에 담기므로
  // 객체 하나를 돌려주는 이 경로도 `result[0]` 에 들어간다.
  // (`deepCheckLlm` 도 같은 방식으로 벗긴다.)
  const raw = res?.result?.[0] ?? res?.result ?? res;

  return {
    defaultProvider: String(raw?.defaultProvider ?? ''),
    failoverEnabled: Boolean(raw?.failoverEnabled),
    lastFailover: (raw?.lastFailover ?? null) as AiFailover | null,
    lastModelSubstitution: (raw?.lastModelSubstitution ??
      null) as AiModelSubstitution | null,
    lastModelRotation: (raw?.lastModelRotation ??
      null) as AiModelRotation | null,
    restingModels: (raw?.restingModels ?? []) as AiRestingModel[],
    providers: (raw?.providers ?? []) as AiProviderInfo[],
  };
}

/**
 * 모델이 **한도(429)에 걸려** 다른 무료 모델로 바꿔 부른 사실.
 *
 * `AiModelSubstitution` 과 **다른 칸이다.** 원인이 다르고 할 일이 다르다 —
 * 저쪽은 "고른 모델이 무료가 아니게 됐다"(설정 목록을 손볼 때),
 * 이쪽은 "그 모델이 지금 붐빈다"(시간이 지나면 풀린다).
 *
 * 이것이 자주 뜨면 환경설정의 기본 모델을 실제로 잘 답하는 것으로 바꿀 때다.
 */
export interface AiModelRotation {
  provider: string;
  /** 한도에 걸린 모델. 사용자가 고른 것일 수 있다. */
  from: string;
  /** 대신 부른 모델. **이것도 무료 확인을 통과한 것이다.** */
  to: string;
  reason: string;
  at: string;
  count: number;
}

/**
 * 지금 쉬는(건너뛰는) 모델.
 *
 * 한도에 걸린 모델은 `until` 까지 건너뛴다. 매 요청이 같은 벽을 다시 치면
 * 왕복 한 번과 하루 요청 한 개를 버리기 때문이다.
 *
 * **사용자가 고른 모델이 여기 있으면 고른 것과 다른 모델이 답하고 있다는 뜻**이라
 * 화면에 반드시 보여야 한다.
 */
export interface AiRestingModel {
  provider: string;
  model: string;
  /** 이 시각이 지나면 다시 1순위가 된다. */
  until: string;
  reason: string;
}

/**
 * 고른 모델이 무료가 아니어서 기본 모델로 바꿔 부른 사실.
 *
 * OpenRouter 는 무료 모델을 수시로 바꾼다. 어제 무료였던 모델이 오늘 사라지면
 * **서버가 부르지 않고 기본 모델로 돌린다**(과금되지 않는다). 그 사실이 여기 뜨면
 * 환경설정의 모델 목록(`preferences.ts`)을 손볼 때다.
 */
export interface AiModelSubstitution {
  provider: string;
  /** 고른, 쓸 수 없던 모델. */
  from: string;
  /** 대신 쓴 모델. */
  to: string;
  reason: string;
  at: string;
  count: number;
}

/**
 * 지금 실제로 무료인 모델 목록.
 *
 * 카탈로그(가격표)만 읽으므로 **AI 사용 한도를 쓰지 않는다.**
 */
export async function getFreeModels(provider: string) {
  const res = await requestClient.get<any>('/ai/models', {
    params: { provider },
  });
  const raw = res?.result?.[0] ?? res?.result ?? res;

  return {
    provider: String(raw?.provider ?? provider),
    freeOnly: Boolean(raw?.freeOnly),
    /** 목록을 받았는지. `false` 면 '무료 모델이 없다' 가 아니라 '못 받았다' 다. */
    available: raw?.available !== false,
    currentModel: String(raw?.currentModel ?? ''),
    currentModelIsFree: Boolean(raw?.currentModelIsFree),
    models: (raw?.models ?? []) as string[],
  };
}

/**
 * 마지막 자동 전환 (결정 D-A3).
 *
 * **접속 실패에만** 일어난다. 한도 초과·인증 실패·생성 시간 초과는 전환하지 않는다.
 * 전환이 잦으면 로컬 장비를 손봐야 한다는 신호다.
 */
export interface AiFailover {
  /** 접속에 실패한 공급자 키. */
  from: string;
  /** 대신 답한 공급자 키. */
  to: string;
  at: string;
  /** 서버 기동 이후 전환 횟수. */
  count: number;
}
