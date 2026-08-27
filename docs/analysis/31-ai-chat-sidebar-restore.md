# AI 채팅 사이드바 되살리기 (헤더 ✨ 아이콘)

> 지시: "메인화면 오른쪽에서 나타나는 AI 채팅 화면과 상단 오른쪽 아이콘이 있었다.
> 해당 기능을 사용하던 소스를 찾고 오른쪽 사이드에 나타나게 하던 기능이 무엇인지 확인해 줘."
> → 확인 후 "A 로 진행해 줘" (항상 표시, 예전과 같게)

작업일: 2026-08-27

---

## 1. 그 기능의 구조

```
fronts/packages/effects/layouts/src/widgets/ai-chat/
  state.ts             isAiChatPinned = ref(false)   ← 열림/닫힘 스위치 하나
  ai-chat-button.vue   헤더의 ✨ 아이콘. 그 값을 뒤집는다
  index.ts             AiChatButton · isAiChatPinned 내보내기

fronts/packages/effects/layouts/src/basic/layout.vue        오른쪽에 자리와 폭을 잡는다
fronts/packages/effects/layouts/src/basic/header/header.vue  아이콘을 헤더에 놓는다
fronts/apps/jsini-portal/src/components/ai-chat/ai-chat-content.vue  대화 화면 본체
```

스토어도 이벤트버스도 아니고 **모듈 스코프의 `ref` 하나**를 양쪽이 import 한다.

### 드로어가 아니라 본문 분할이다

`layout.vue` 의 `#content` 슬롯이 `flex` 로 쪼개진다.

```
│  LayoutContent (flex-1)  │▎│  #ai-chat 슬롯 (384px)  │
                            └ 드래그 스플리터
```

모달로 덮지 않으므로 뒤 화면이 가려지지 않고 스크롤도 각자 따로 된다.
그래서 다른 업무를 하면서 같이 쓸 수 있다.

- 폭은 280~800px, `localStorage['vben_ai_chat_sidebar_width']` 에 남는다
- 대화는 `localStorage['vben_ai_chat_sessions']` 에 세션별로 남고 **1주일 뒤 자동 삭제**
  (서버에 보관하지 않으므로 다른 기기에서는 보이지 않는다)

---

## 2. 왜 사라져 있었나 — 세 곳이 끊겨 있었다

셋 다 `a8e93fa`(vben 상위 동기화, 2026-08-23)에서 생겼다. 각각 따로 고장 났고,
**앞의 것을 고치면 다음 것이 드러나는** 순서였다.

| # | 끊긴 곳 | 증상 |
|---|---|---|
| 1 | `header.vue` 의 `rightSlots` 에 `'ai-chat'` 이 없다 | **아이콘이 아예 없다.** 템플릿의 `'ai-chat'` 분기는 닿을 수 없는 죽은 코드였다 |
| 2 | `layout.vue` 에 `AiChatContent` import 가 없다 | 태그는 있는데 컴포넌트를 못 찾는다 |
| 3 | `ai-chat-content.vue` 에 `VbenIconButton` import 가 없다 | 창 안의 **닫기·지난 대화 버튼이 먹지 않는다** (`<vbeniconbutton>` 로 렌더) |

동기화 때 `rightSlots` 가 `if` 나열식에서 `preferences.widget.order` + `widgetChecks` 표
방식으로 바뀌었고, 그 과정에서 우리 항목이 옮겨지지 않았다.

```diff
- // AI 어시스턴트 아이콘 추가          ← 8091049 (동기화 전)
- list.push({ index: REFERENCE_VALUE + 15, name: 'ai-chat' });
```

---

## 3. 고친 방식

### 3.1 아이콘 (지시 A — 항상 표시)

`widgetChecks` 표에 넣지 않고 그 뒤에서 직접 끼워 넣는다. 그 표는
`preferences.widget.order` 를 따라 도는데 **그 목록은 `@core/preferences` 의 기본값**이라
우리 항목을 넣으면 상위와 동기화할 때마다 부딪힌다. 블록 하나로 묶어 두면 갈라지는
지점이 한 곳에 남는다.

위치는 설정 버튼 바로 뒤 — 예전과 같다(설정 버튼이 헤더에 없으면 맨 앞).
다른 위젯 열 개가 모두 툴팁을 갖게 되었으므로 `ui.widgets.aiChat`("AI 채팅" / "AI Chat")을
추가해 함께 붙였다.

### 3.2 채팅 본체를 앱으로 옮겼다

2번을 고치자 **`pnpm vite build` 가 깨졌다.**

```
Rolldown failed to resolve import "ant-design-vue" from
  packages/effects/layouts/src/widgets/ai-chat/ai-chat-content.vue
```

`ai-chat-content.vue` 는 antd 를 쓰는데 **antd 는 프레임워크 계층 어디에도 없다** —
워크스페이스 전체에서 `ant-design-vue` 를 의존성으로 가진 것은 `apps/jsini-portal`
하나뿐이고, 프레임워크는 `@vben-core/shadcn-ui` 만 쓴다. 지금까지 안 터진 이유는
import 가 빠져 있어 **이 파일이 번들에 들어간 적이 없었기** 때문이다.

두 갈래였다.

| | 방법 | 대가 |
|---|---|---|
| 1 | `layouts/package.json` 에 antd 추가 | 한 줄. 그러나 프레임워크가 antd 를 물게 되는 **첫 사례**가 되고 상위와 갈라지는 지점이 늘어난다 |
| 2 | **본체를 앱으로 옮기고 슬롯만 남긴다** | 파일 이동. 프레임워크는 antd 를 모른 채로 남는다 |

**2를 택했다.** `apps/jsini-portal` 은 상위와 무관하게 우리 것이고(CLAUDE.md),
프레임워크에 남는 것은 스위치·버튼·자리뿐이다.

```
layout.vue        <AiChatContent mode="pinned" />  →  <slot name="ai-chat">
basic.vue (앱)    <template #ai-chat><AiChatContent /></template>
```

덤으로 `provide('AI_CHAT_STREAM_API', …)` 우회로가 없어졌다. 그것은 프레임워크
패키지가 앱의 API 를 모르게 하려고 있던 것인데, 이제 컴포넌트가 앱 안에 있으므로
`streamChatMessage` 를 그냥 import 한다.

`onUnmounted` 에 `storage` 리스너 제거를 넣었다. 없으면 사이드바를 여닫을 때마다
리스너가 쌓인다.

---

## 4. 확인한 것

개발 서버(:5555)에서 실제로 눌러 확인했다.

| | 결과 |
|---|---|
| 헤더 ✨ 아이콘 | 있음. 위치는 설정 버튼 뒤(= 테마 전환 앞) — 예전과 같다 |
| 툴팁 | "AI 채팅" (다국어키 그대로 나오지 않음) |
| 클릭 → 사이드바 | 폭 384px, 본문 773px + 스플리터 4px = 1280px 로 정확히 나뉜다 |
| 창 안 버튼 | **3개 모두 동작** (지난 대화 목록 · 닫기 · 전송). 미해결 컴포넌트 0 |
| 폭 드래그 | 384 → 504px, `localStorage` 에 저장됨 |
| 대화 | "핑" → "퐁!" 수신. `POST /api/ai/chat/stream` 200 |
| `pnpm vite build` | 통과 |
| eslint | 손댄 파일에서 새로 생긴 오류 없음 |

대화 확인 때 응답이 **"JSINI(로컬 LLM) 에 접속할 수 없어 Groq Free 로 답합니다"** 로
왔다 — 로컬 LLM 장비가 꺼져 있고 자동 전환이 동작한 것이다(30번 문서).

확인용으로 보낸 대화와 바꾼 사이드바 폭은 되돌렸다.

---

## 5. 곁에서 발견한 것 (고치지 않음) 🟡

`layout.vue:664` 의 메뉴 검색창이 `<Input>` 을 쓰는데 **import 가 없다**(HEAD 부터
그렇다). 콘솔에 `Failed to resolve component: Input` 이 매번 뜬다. 다만 태그 이름이
HTML 의 `input` 과 같아 브라우저가 native `<input>` 으로 만들고 `v-model` 도 그대로
동작하므로 **기능은 되고 antd 스타일만 빠진 상태**다. 위 3번과 같은 종류의 누락이다.
