import type { PreferencesExtension } from '@vben/preferences';

import { defineOverridesPreferences } from '@vben/preferences';

/**
 * @description 프로젝트 설정 파일
 * 프로젝트의 일부 설정만 덮어쓰면 되며, 필요하지 않은 설정은 덮어쓸 필요 없이 자동으로 기본 설정이 사용됩니다.
 * !!! 설정을 변경한 후에는 캐시를 비워주세요. 그렇지 않으면 적용되지 않을 수 있습니다.
 */
export const overridesPreferences = defineOverridesPreferences({
  // overrides
  app: {
    name: import.meta.env.VITE_APP_TITLE,
    enableCheckUpdates: false,
    accessMode: 'backend',
    // 로그인 후 첫 화면과 루트('/') 리다이렉트 대상.
    // 프레임워크 기본값은 '/analytics' 인데 이 포털의 첫 화면은 '/workspace' 다.
    //
    // 계정마다 다르게 두려면 이 값이 아니라 계정 프로필의 HomePath 를 쓴다
    // (scom.account_profile_details, detail_type='HomePath').
    // 그 값이 있으면 그쪽이 우선이고, 없을 때 여기 값이 쓰인다.
    defaultHomePath: '/workspace',
  },
  // 로고 — 브랜드 키트에서 온다. 원본은 `docs/brand/` 이고 `public/brand/` 는 그 복사본이다.
  // 규칙은 `docs/brand/README.md`, 파일은 `docs/brand/generate.py` 가 만든다.
  // **SVG 를 손으로 고치지 않는다.**
  //
  // **지금 이 값을 읽는 화면은 없다.** 사이드바와 로그인 화면 둘 다 브랜드를 직접 그린다
  // (`packages/effects/layouts/src/basic/layout.vue` 의 `#logo`, `layouts/auth.vue`).
  // 워드마크를 화면 글꼴로 흉내내지 않으려고 그렇게 했다.
  //
  // 그래도 비워 두지 않는다. 이 설정을 비우면 프레임워크 기본값이 쓰이는데 그것이
  // **unpkg CDN 주소**라, 어디선가 읽는 순간 바깥으로 요청이 나간다 (준수사항 5 위반).
  // 그래서 브랜드 자산을 가리키는 안전한 기본값으로 남겨 둔다.
  //
  // `favicon.svg`(블록형)가 아니라 `app-icon`(블레이드 J 글자만)을 둔 이유는,
  // 이 값이 쓰이는 자리가 대개 이름 글자 옆이기 때문이다. 32px 잉크 블록은 글자와
  // 무게가 맞지 않고 다크 모드에서는 흰 블록이 된다.
  logo: {
    source: '/brand/app-icon.svg',
    sourceDark: '/brand/app-icon-knockout.svg',
  },
  theme: {
    // 기본 제공 테마. 라이트/다크에 쓸 주 색상을 테마가 각각 들고 있으므로
    // colorPrimary 를 여기서 따로 박지 않는다.
    //   gray → 라이트 hsl(240 5.9% 10%) / 다크 hsl(0 0% 98%)
    //
    // 예전에는 colorPrimary: 'hsl(0 0% 98%)' 가 박혀 있었다. 다크 모드용 값이라
    // 라이트 모드에서 선택된 메뉴 글자(--menu-item-active-color: hsl(var(--primary)))가
    // 배경과 같은 색이 되어 보이지 않았다.
    builtinType: 'gray',
    // 시스템 기본 글자 크기(px). 프레임워크 기본값은 16 인데 이 포털은 표·그리드가
    // 많아 한 화면에 들어가는 양을 늘리려고 낮춰 쓴다.
    //
    // 이 값은 "저장된 설정이 없을 때"의 기본값이다. 사용자가 환경설정에서 한 번
    // 바꾸면 그 값이 로컬스토리지(`jsini-portal-web-preferences`)에 남고 그쪽이 우선한다.
    // 그래서 여기를 고쳐도 이미 쓰던 브라우저에는 바로 반영되지 않는다 —
    // 환경설정 창의 초기화를 누르거나 로컬스토리지를 비워야 한다.
    fontSize: 14,
    mode: 'auto',
    radius: '0.25',
  },
});

/**
 * [환경설정에 추가한 우리 항목]
 *
 * 프레임워크가 주는 확장 자리다(`initPreferences({ extension })`). 여기에 적으면
 * 헤더 톱니의 드로어와 `/setting/environment` 화면에 **탭이 하나 생긴다.**
 * 프레임워크 코드는 손대지 않는다 — 상위 동기화 때 충돌할 일이 없다.
 *
 * 값은 계정에 붙어 서버에 저장된다(`store/preferences-sync.ts`).
 * 다른 PC 에서 로그인해도 따라온다.
 */
export interface JsiniCustomPreferences {
  /** 쓸 AI 공급자. AIAgentServer 의 `AI:Providers` 키와 같은 값이어야 한다. */
  aiProvider: string;
  /** OpenRouter 를 골랐을 때 쓸 모델. **무료 모델만 목록에 둔다.** */
  aiOpenRouterModel: string;
}

/**
 * AI 공급자 선택.
 *
 * 로컬 LLM(JSINI) 장비가 자주 꺼져 있어서, 꺼져 있는 동안 Groq 무료 플랜으로
 * 옮겨 쓸 수 있게 사람이 고르게 했다. 고른 값은 AI 요청마다 서버로 넘어가고
 * (`api/portal/ai/provider.ts`), 서버가 그 공급자로 처리한다.
 *
 * **기본값은 `jsini` 다.** 지금까지와 똑같이 동작한다 — Groq 를 쓰려면 사람이 골라야 한다.
 */
export const AI_PROVIDER_KEY = 'aiProvider';

/** OpenRouter 모델을 담는 설정 키. */
export const AI_OPENROUTER_MODEL_KEY = 'aiOpenRouterModel';

/** 고를 수 있는 값. 서버 `AI:Providers` 의 키와 짝이다. */
export const AI_PROVIDERS = {
  groq: 'groq',
  jsini: 'jsini',
  openrouter: 'openrouter',
} as const;

/**
 * OpenRouter 에서 고를 수 있는 모델. **무료(`:free`) 모델만 넣는다.**
 *
 * [이 목록은 편의일 뿐 안전장치가 아니다]
 * 과금을 막는 것은 **서버**다. 브라우저에서 온 모델 이름은 믿을 수 없으므로
 * AIAgentServer 가 부르기 전에 두 가지를 확인한다 — 이름이 `:free` 로 끝나는지,
 * 그리고 OpenRouter 카탈로그의 실제 단가가 0 인지(`FreeModelGuard`).
 * 요청 본문에도 `max_price=0` · `allow_fallbacks=false` 를 함께 보낸다.
 *
 * [목록이 낡는다]
 * OpenRouter 는 무료 모델을 수시로 바꾼다. 여기 적힌 모델이 유료로 바뀌거나
 * 사라지면 **서버가 부르지 않고 기본 모델로 돌린다**(과금되지 않는다).
 * 그 사실은 상태 화면의 AI 공급자 블록에 뜨므로, 그때 이 목록을 손보면 된다.
 * 지금 실제로 무료인 목록은 `GET /api/ai/models?provider=openrouter` 로 볼 수 있다.
 *
 * [순서가 뜻을 가진다 — 첫 번째가 기본값이다]
 * 무료 모델은 상류 제공자가 자주 붐벼서(429 "temporarily rate-limited upstream")
 * **이름만 무료인 것과 실제로 답하는 것이 다르다.** 그래서 실제로 답한 것을 앞에 둔다.
 *
 * 2026-08-27 실측:
 *   · minimax-m3          — 한국어로 깔끔하게 답함 (GMICloud)     ✔ 기본값
 *   · nemotron-3-super    — 답하지만 생각 과정이 섞여 나옴
 *   · gemma-4-31b-it      — 계속 429 (상류 혼잡)
 *   · gemma-4-26b-a4b-it  — 계속 429
 *   · glm-5.2             — 계속 429
 *   · inkling            — 403 (이 키로 접근 불가) → 목록에서 뺐다
 *
 * 429 는 고장이 아니라 그때 붐빈 것이라 남겨 둔다. 시간이 지나면 풀린다.
 *
 * [한도에 걸리면 서버가 알아서 바꾼다]
 * 여기서 고른 모델이 429 를 주면 서버가 **다른 무료 모델로 바꿔** 답한다
 * (AIAgentServer 설정의 `AI:Providers:openrouter:FallbackModels` 순서를 따른다).
 * 그러니 이 목록에서 붐비는 모델을 골라 두어도 답은 나온다.
 *
 * **이 목록과 서버의 `FallbackModels` 는 다른 목록이다.** 여기는 "사람이 고를 수
 * 있는 것", 저기는 "한도에 걸렸을 때 서버가 대신 쓸 것"이다. 굳이 같게 맞출
 * 필요는 없지만, 여기에만 있는 모델은 대체 대상이 되지 않는다.
 */
export const AI_OPENROUTER_FREE_MODELS = [
  'minimax/minimax-m3:free',
  'nvidia/nemotron-3-super-120b-a12b:free',
  'minimax/minimax-m2.7:free',
  'google/gemma-4-31b-it:free',
  'google/gemma-4-26b-a4b-it:free',
  'z-ai/glm-5.2:free',
] as const;

export const jsiniPreferencesExtension: PreferencesExtension<JsiniCustomPreferences> =
  {
    tabLabel: 'AI',
    title: 'AI 모델',
    fields: [
      {
        component: 'select',
        defaultValue: AI_PROVIDERS.jsini,
        key: AI_PROVIDER_KEY,
        label: '사용할 AI 모델',
        options: [
          { label: 'JSINI (로컬 LLM)', value: AI_PROVIDERS.jsini },
          { label: 'Groq Free (클라우드)', value: AI_PROVIDERS.groq },
          { label: 'OpenRouter Free (클라우드)', value: AI_PROVIDERS.openrouter },
        ],
        tip:
          'JSINI 는 사내 장비의 로컬 LLM 이고, Groq · OpenRouter 는 설치 없이 쓰는 ' +
          '무료 클라우드다. 로컬 장비가 꺼져 있을 때 바꾸면 계속 쓸 수 있다. ' +
          '무료 한도를 넘으면 잠시 차단되며, 과금되지 않는다.',
      },
      {
        component: 'select',
        defaultValue: AI_OPENROUTER_FREE_MODELS[0],
        key: AI_OPENROUTER_MODEL_KEY,
        label: 'OpenRouter 모델',
        options: AI_OPENROUTER_FREE_MODELS.map((model) => ({
          // 접두사(제공사)를 떼고 보여 주면 목록이 훨씬 읽기 쉽다.
          // 값은 서버가 그대로 써야 하므로 전체 이름을 유지한다.
          label: model.replace(':free', ''),
          value: model,
        })),
        tip:
          '위에서 OpenRouter 를 골랐을 때만 쓰인다. **무료 모델만 나열한다.** ' +
          '고른 모델을 먼저 쓰고, 그 모델이 사용 한도에 걸리면 서버가 ' +
          '다른 무료 모델로 바꿔 답한다(바꾼 사실은 답 앞에 한 줄로 알려 준다). ' +
          '고른 모델이 무료가 아니게 되면 부르지 않고 기본 모델로 돌린다 — ' +
          '어느 경우에도 과금되지 않는다.',
      },
    ],
  };
