# 작업 계획서: 미사용 AI Drawer 컴포넌트 정리

## 1) 문제 요약
* **현상**: AI 아이콘 클릭 시 즉시 핀 고정(사이드바) 형태로 AI 채팅 화면(`ai-chat-content.vue`)이 나타나고 토글되도록 이미 구현이 변경되었습니다.
* **과제**: 이전에 사용되었으나 더 이상 필요하지 않은 레거시 컴포넌트인 `ai-chat-drawer.vue` 및 `ai-chat.vue` 파일이 디스크에 남아 있어, 이를 영구 삭제하고 모듈 수출(`index.ts`)을 사후 정리하여 코드베이스의 청결성을 유지합니다.

---

## 2) 설계 요약
* **정리 대상 파일**:
  1. `fronts/packages/effects/layouts/src/widgets/ai-chat/ai-chat.vue` (삭제)
  2. `fronts/packages/effects/layouts/src/widgets/ai-chat/ai-chat-drawer.vue` (삭제)
* **수정 대상 파일**:
  1. `fronts/packages/effects/layouts/src/widgets/ai-chat/index.ts` (불필요한 export문 제거)

---

## 3) 구현 계획
* **Task 1: index.ts 수정**
  * `widgets/ai-chat/index.ts`에서 `AiChat`과 `AiChatDrawer`에 대한 export 구문을 지우고, 오직 `AiChatButton`만 내보내도록 수정합니다.
* **Task 2: 불필요한 파일 삭제**
  * `ai-chat.vue`와 `ai-chat-drawer.vue`를 삭제합니다.
* **Task 3: 검증**
  * 프로젝트 루트에서 빌드와 타입 검사(`pnpm run check:type`)를 실행하여 제거 작업으로 인한 부작용(broken imports 등)이 없는지 확인합니다.

---

## 4) 자가 코드 리뷰 계획
* 삭제된 파일들을 참조하는 다른 컴포넌트나 모듈이 없는지 `grep` 검색 결과를 교차 검증합니다.
* TypeScript 컴파일 에러가 발생하지 않는지 검증합니다.
