# AI 대화형 인터페이스(AI Chat) UI/UX 디자인 표준 가이드

본 문서는 `/site/ai/chat` 및 AI 서랍 등 시스템 전반에 적용된 **ChatGPT 스타일의 현대적 모바일·웹 대화형 인터페이스 표준 규격서**입니다. 향후 새로운 대화형 AI 서비스, 챗봇, 어시스턴트 기능 구축 시 본 표준을 준수하여 일관된 사용자 경험을 제공합니다.

---

## 1. 핵심 디자인 철학 및 원칙 (Core Principles)

1. **콘텐츠 중심의 미니멀리즘 (Content-First & Clutter-Free)**
   - 대화의 본질은 **메시지 내용의 신속하고 명확한 전달**입니다.
   - AI 답변 행에서 불필요한 로봇/모델 아바타 아이콘을 배제하여 좌우 읽기 영역을 100% 확보하고 시각적 피로도를 최소화합니다.
   - 사용자 메시지는 시각적으로 명확히 구분되는 라운드 버블(우측 정렬)로, AI 답변은 자연스러운 플랫 텍스트(좌측 정렬)로 렌더링합니다.

2. **중앙 집중형 반응형 레이아웃 (Center-Aligned Feed)**
   - 대화 스레드는 화면 폭에 관계없이 최대 `48rem`(약 768px)의 중앙 집중형 컨테이너를 유지합니다.
   - 와이드 모니터에서도 시선이 좌우로 과도하게 분산되지 않으며, 모바일 디바이스에서는 자연스럽게 100% 너비로 전환됩니다.

3. **시그니처 플로팅 캡슐형 입력창 (Floating Capsule Input)**
   - 입력 영역은 ChatGPT 특유의 둥근 알약(Pill/Capsule) 형태로 화면 하단에 플로팅 배치합니다.
   - 텍스트 입력 길이에 따라 부드럽게 세로로 자동 확장되며, 전송/중단 원형 액션 단추가 우측 하단에 정렬됩니다.

4. **모바일 뷰포트 및 안전 영역(Safe-Area) 완벽 통합**
   - 모바일 브라우저 환경에서 주소창 및 가상 키보드 노출 시 스크롤 오동작이나 하단 가림이 없도록 `height: 100%` 기반의 유연한 Flexbox 구조를 적용합니다.
   - 하단 시스템 네비게이션 바 및 노치/홈 인디케이터에 맞춘 `env(safe-area-inset-bottom)` 여백을 기본 지원합니다.

---

## 2. 화면 구조 및 레이아웃 아키텍처 (Layout Architecture)

```
┌─────────────────────────────────────────────────────────────┐
│ 1. 상단 헤더 바 (.jsini-chat__header)                       │
│  [≡ 히스토리]   [AI 모델 뱃지]  [대화 제목 수정]   [+새대화] [🗑]│
├─────────────────────────────────────────────────────────────┤
│ 2. 대화 피드 영역 (.jsini-chat__body)                       │
│    최대 너비 48rem 중앙 정렬 스레드                          │
│                                                             │
│    [환영 화면 .jsini-chat__welcome]                         │
│      - 브랜드 심볼 (4rem 원형 그라디언트)                   │
│      - 헤드라인 & 안내 서브텍스트                           │
│      - 추천 프롬프트 그리드 (2x2 반응형 카드)               │
│                                                             │
│    [메시지 스레드 .jsini-chat__thread]                      │
│      - 사용자 메시지 (우측 정렬 라운드 버블)                │
│      - AI 응답 (좌측 정렬, 아바타 없는 클린 텍스트, 복사버튼)│
│      - 스트리밍 타이핑 인디케이터 (3-dot 바운스 애니메이션) │
├─────────────────────────────────────────────────────────────┤
│ 3. 하단 입력 영역 (.jsini-chat__input-area)                 │
│    ┌───────────────────────────────────────────────────┐    │
│    │ [캡슐 입력창 .jsini-chat__capsule]                 │    │
│    │  입력 텍스트에어리어 (자동 확장)          (↑전송버튼)│    │
│    └───────────────────────────────────────────────────┘    │
│    "AI는 실수를 할 수 있습니다. 중요한 정보는 확인하세요."    │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. 세부 컴포넌트 표준 규격 (Component Specifications)

### 3.1 상단 네비게이션 헤더 (`.jsini-chat__header`)

- **높이 및 여백**: 높이 약 `3.25rem`(52px), 좌우 여백 `0.75rem`, 하단 1px 경계선 (`var(--jsini-border)`).
- **좌측 컨트롤**: 대화 목록을 여는 오프캔버스 드로어 토글 단추 (원형/투명 hover, 36x36px).
- **중앙 정보**:
  - **모델 뱃지 (`.jsini-chat__model-badge`)**: 캡슐형 뱃지, 은은한 배경색과 액센트 폰트, 현재 작동 중인 AI 모델명 명시.
  - **세션 제목 (`.jsini-chat__title-input`)**: 인라인 편집 가능 input. 기본 스타일은 텍스트처럼 보이나 포커스 시 테두리 노출. 텍스트 말줄임(`text-overflow: ellipsis`) 적용.
- **우측 액션**:
  - `새 대화 (+)`: 신규 세션 즉시 시작 단추.
  - `삭제 (휴지통)`: 현재 대화 초기화 단추.

### 3.2 오프캔버스 대화 히스토리 드로어 (`.jsini-chat__history-drawer`)

- **너비**: 모바일 및 데스크톱 공통 너비 `min(280px, 80vw)`.
- **배경 딤 처리 (`.jsini-chat__backdrop`)**: `rgba(0, 0, 0, 0.45)`의 반투명 딤 처리로 대화 본문과 시각적 계층 분리. 터치/클릭 시 즉시 닫힘.
- **슬라이드 애니메이션**: `transform: translateX(-100%)` → `translateX(0)` (0.25s ease-out).
- **항목 구성**:
  - 상단 고정: `새 대화 시작` 버튼 (`+ New Chat`).
  - 본문 스크롤: 최근 대화 목록 (활성 대화 하이라이트 배경색, 삭제 단추 마우스오버/선택 시 노출).

### 3.3 초기 환영 화면 (`.jsini-chat__welcome`)

대화 내역이 없는 초기 상태에서 사용자에게 친근감과 활용 가이드를 제공합니다.
- **심볼 아이콘 (`.jsini-chat__welcome-symbol`)**:
  - 직경 `4rem`(64px), 완전한 원형 (`border-radius: 50%`).
  - 브랜드 액센트 컬러의 부드러운 그라디언트 배경 + 은은한 그림자.
- **헤드라인 & 리드문**:
  - 타이틀: 1.35rem, 볼드, "무엇이든 물어보세요"
  - 서브텍스트: 0.9rem, 뮤트 컬러, 친절한 사용 유도 문구.
- **추천 프롬프트 카드 (`.jsini-chat__prompts`)**:
  - 2열 그리드 (`grid-template-columns: repeat(auto-fit, minmax(220px, 1fr))`).
  - 카드 스타일: 둥근 모서리(`border-radius: 0.85rem`), 1px 보더, 투명 배경, hover 시 테두리 액센트화 및 위로 살짝 떠오름(`translateY(-2px)`).
  - 카드 내용: 볼드 타이틀 + 서브 설명 문구. 클릭 시 즉시 질문 전송 처리.

### 3.4 메시지 행 및 버블 (`.jsini-chat__row`)

#### A. 사용자 메시지 (`.jsini-chat__row--user`)
- **정렬**: 우측 정렬 (`justify-content: flex-end`).
- **버블 스타일**:
  - 배경: 브랜드 액센트 컬러 믹스 (`color-mix(in srgb, var(--jsini-accent) 14%, var(--jsini-surface))`).
  - 보더: 액센트 컬러 투명 보더 (`color-mix(in srgb, var(--jsini-accent) 28%, transparent)`).
  - 라운드 코너: `border-radius: 1.25rem 1.25rem 0.25rem 1.25rem` (우측 하단만 뾰족한 대화 버블 형태).
  - 최대 너비: `max-width: 82%` (모바일 가독성 고려).
  - 텍스트: `font-size: 0.95rem`, 줄간격 `1.55`.

#### B. AI 메시지 (`.jsini-chat__row--ai`)
- **정렬**: 좌측 정렬 (`justify-content: flex-start`).
- **아이콘 배제 원칙**:
  - **AI 로봇/모델 아바타 아이콘을 표시하지 않습니다.** (불필요한 가로 공간 낭비 방지 및 클린 텍스트 뷰 제공).
  - 전체 너비 `max-width: 100%`를 사용하여 읽기 편안한 문단 레이아웃을 형성합니다.
- **발신자 라벨 (`.jsini-chat__author-name`)**:
  - `JSini AI` 등 시스템/어시스턴트 식별자를 상단에 작고 연하게(`0.75rem`, `var(--jsini-text-muted)`) 표시.
- **버블 스타일**:
  - 배경: `var(--jsini-surface)` (배경 카드형) 및 미세 보더 `1px solid var(--jsini-border)`.
  - 라운드 코너: `border-radius: 1.25rem 1.25rem 1.25rem 0.25rem`.
  - 텍스트: `white-space: pre-wrap; word-break: break-word;` 코드 및 마크다운 텍스트 자동 줄바꿈.
- **하단 액션 도구바 (`.jsini-chat__message-tools`)**:
  - 메시지 하단에 미니멀한 복사(Copy) 단추 제공.
  - 클릭 시 클립보드 복사 실행 및 1.5초간 "복사됨!" 피드백 아이콘/라벨 토글.

#### C. 실시간 스트리밍 커서 (`.jsini-chat__cursor`)
- AI 답변 수신 중 메시지 끝에 위치하는 인라인 블록 커서.
- 폭 0.45rem, 높이 1rem, 액센트 컬러.
- 0.8초 주기의 `blink` 애니메이션 적용.

#### D. 타이핑 인디케이터 (`.jsini-chat__typing`)
- 첫 응답 토큰이 도착하기 전 로딩 상태 표시.
- 아이콘 없이 3개의 닷이 순차적으로 튀어 오르는 바운스 애니메이션 (`.jsini-chat__typing-dot`).
- 옆에 은은한 안내 텍스트 ("답변을 생성하고 있습니다…") 병기.

### 3.5 하단 플로팅 캡슐 입력 영역 (`.jsini-chat__input-area`)

- **배치**: 하단 플로팅 컨테이너. 그라디언트 페이드아웃 배경으로 스크롤되는 대화 메시지와 자연스럽게 블렌딩.
- **최대 너비**: 상단 대화 피드와 일치하는 `48rem` 중앙 정렬.
- **캡슐 컨테이너 (`.jsini-chat__capsule`)**:
  - 모서리: `border-radius: 1.75rem` (완전한 타원/캡슐형).
  - 테두리 및 그림자: `border: 1px solid var(--jsini-border)`, `box-shadow: 0 4px 18px rgba(0, 0, 0, 0.08)`.
  - 포커스 상태: `border-color: var(--jsini-accent)` 및 은은한 액센트 글로우 링.
- **입력 텍스트에어리어**:
  - 테두리 없는 투명 배경, 패딩 최소화.
  - 키 입력 제어: `Enter`는 전송, `Shift + Enter`는 줄바꿈.
  - 모바일에서는 가상 키보드 높이에 따라 자연스럽게 상단으로 밀려 올라감.
- **원형 액션 버튼 (`.jsini-chat__send-btn`)**:
  - 크기: `34x34px`, 원형 (`border-radius: 50%`).
  - 일반 상태: 텍스트 미입력 시 비활성화(연한 회색), 입력 시 액센트 컬러 배경 활성화 (`jsini-icon-arrow-up` 또는 종이비행기).
  - 스트리밍 중: 사각형 중단 아이콘(`jsini-icon-square`)으로 전환되어 클릭 시 스트리밍 즉시 취소.
- **하단 안내 문구 (`.jsini-chat__disclaimer`)**:
  - 폰트 크기: `0.75rem`, 중앙 정렬, 뮤트 컬러.
  - 문구: "AI는 실수를 할 수 있습니다. 중요한 정보는 직접 확인하세요."

---

## 4. 디자인 토큰 및 CSS 변수 매핑 가이드

본 디자인은 시스템 글로벌 테마 변수를 기반으로 작동하며, 라이트 모드 및 다크 모드에 자동 대응합니다.

| 분류 | 변수명 | 용도 및 권장 값 |
|---|---|---|
| **액센트** | `--jsini-accent` | 버튼 활성화, 모델 뱃지, 사용자 버블 틴트의 기준 색상 |
| **배경** | `--jsini-surface` | 메시지 버블, 입력 캡슐, 환영 카드의 배경색 |
| **경계선** | `--jsini-border` | 헤더 분할선, 입력 캡슐 보더, 추천 카드 외곽선 |
| **텍스트** | `--jsini-text` | 본문 텍스트 기본 색상 |
| **텍스트(보조)** | `--jsini-text-muted` | 발신자명, 안내문, 팁 텍스트 색상 |
| **모바일 높이** | `--jsini-bottomnav-h` | 모바일 하단 탭 바 높이 (기본 `3.5rem`) |

---

## 5. 모바일 최적화 규칙 (Mobile Optimization Rules)

1. **상단 및 좌우 여백 리셋**:
   - 일반 화면용 기본 패딩(2rem 등)을 `:has(> .site-ai-chat)`를 통해 모바일/데스크톱 모두 `padding: 0 !important`로 무효화하여 엣지 투 엣지(Edge-to-Edge) 경험을 제공합니다.

2. **하단 Safe-Area 계산**:
   ```css
   @media (max-width: 767px) {
     .jsini-shell__content:has(> .site-ai-chat) {
       padding-bottom: calc(var(--jsini-bottomnav-h) + env(safe-area-inset-bottom)) !important;
     }

     .jsini-shell--bottomnav-hidden .jsini-shell__content:has(> .site-ai-chat) {
       padding-bottom: env(safe-area-inset-bottom) !important;
     }
   }
   ```

3. **터치 타겟 및 글꼴 크기**:
   - 모바일 사파리/크롬에서 입력창 포커스 시 브라우저가 화면을 자동 확대(Zoom)하지 않도록 입력 텍스트 크기를 최소 `16px`(또는 `0.95rem`) 이상으로 유지합니다.
   - 모든 대화형 버튼(헤더 버튼, 프롬프트 카드, 전송 버튼)의 터치 영역을 최소 36px 이상으로 보장합니다.

---

## 6. 표준 마크업 템플릿 (Markup Reference)

```html
<div class="jsini-chat">
  <!-- 1. 헤더 영역 -->
  <div class="jsini-chat__header">
    <div class="jsini-chat__header-left">
      <button type="button" class="jsini-chat__icon-btn" title="대화 목록">
        <span class="jsini-icon-menu"></span>
      </button>
      <div class="jsini-chat__model-badge">AI Assistant</div>
      <input type="text" class="jsini-chat__title-input" value="새로운 대화" />
    </div>
    <div class="jsini-chat__header-right">
      <button type="button" class="jsini-chat__icon-btn" title="새 대화">
        <span class="jsini-icon-plus"></span>
      </button>
      <button type="button" class="jsini-chat__icon-btn" title="대화 비우기">
        <span class="jsini-icon-trash"></span>
      </button>
    </div>
  </div>

  <!-- 2. 대화 본문 피드 -->
  <div class="jsini-chat__body">
    <div class="jsini-chat__thread">
      <!-- AI 메시지 예시: 아바타 아이콘 없이 본문과 복사 도구 구성 -->
      <div class="jsini-chat__row jsini-chat__row--ai">
        <div class="jsini-chat__message-content">
          <div class="jsini-chat__author-name">JSini AI</div>
          <div class="jsini-chat__bubble">
            안녕하세요! 무엇을 도와드릴까요?
          </div>
          <div class="jsini-chat__message-tools">
            <button type="button" class="jsini-chat__tool-btn">
              <span class="jsini-icon-copy"></span>
              <span>복사</span>
            </button>
          </div>
        </div>
      </div>

      <!-- 사용자 메시지 예시: 우측 정렬 캡슐 버블 -->
      <div class="jsini-chat__row jsini-chat__row--user">
        <div class="jsini-chat__message-content">
          <div class="jsini-chat__bubble">
            오늘 주요 일정 요약해 줘.
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- 3. 하단 캡슐 입력 영역 -->
  <div class="jsini-chat__input-area">
    <div class="jsini-chat__input-container">
      <div class="jsini-chat__capsule">
        <textarea class="jsini-chat__input" placeholder="메시지를 입력하세요… (Shift+Enter 줄바꿈)"></textarea>
        <div class="jsini-chat__capsule-actions">
          <button type="button" class="jsini-chat__send-btn" title="메시지 전송">
            <span class="jsini-icon-arrow-up"></span>
          </button>
        </div>
      </div>
      <div class="jsini-chat__disclaimer">
        AI는 실수를 할 수 있습니다. 중요한 정보는 직접 확인하세요.
      </div>
    </div>
  </div>
</div>
```

---

## 7. 결론 및 향후 확장 지침

- 신규 AI 기능(코드 어시스턴트, 상담 봇, 문서 Q&A 등) 개발 시 본 규격의 클래스 네이밍 규칙(`jsini-chat__*`)과 구조를 재사용하십시오.
- AI 답변의 시각적 청결성을 위해 아바타 아이콘을 재도입하지 않으며, 복사·좋아요·재생성 등 추가 액션은 메시지 하단의 `.jsini-chat__message-tools`에 미니멀하게 배치하십시오.
