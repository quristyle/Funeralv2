# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:55
- **작업명**: 메뉴 수정 폼 비동기 검증 레이스 컨디션 및 값 누락 수정
- **예상 소요 시간**: 10분

## 1) Problem Summary (문제 요약)
- 메뉴 수정 드로워 오픈 시 이름/경로의 비동기 중복 검증 API(`/name-exists`, `/path-exists`)가 불필요하게 서버로 전송되고, 이로 인한 타이밍 꼬임(Race Condition)으로 제목(`meta.title`)과 컴포넌트(`component`) 값이 폼 화면에서 유실(누락)되는 버그 발생.
- `formData.value`가 반응형으로 확정되기 전에 데이터 주입이 비동기 검사 로직을 즉각 작동시켜, 본인의 값과 비교 조건을 충족하지 못하고 강제로 검증 API가 실행되는 문제를 원천 차단함.

## 2) Design Summary (설계 요약)
- **비동기 검사 우회 및 1회 동기화**:
  - `onOpenChange` 내부에서 동기식 `setValues` 호출을 완전히 제거.
  - 오직 `nextTick` 내부에서 단 1회 정밀하게 `formApi.setValues(formValues)`를 수행하게 일원화하여, `formData.value`가 완벽히 적용된 안전한 틱에서 폼 바인딩이 돌도록 보장.
  - 세팅 직후 `formApi.clearValidate()`를 호출하여 불필요한 초기 검증 오류 표시를 정리함.
- **제목 입력란 (`CustomTitleInput`) 반응형 바인딩 적용**:
  - `CustomTitleInput` 컴포넌트 내부에도 로컬 `innerValue` ref와 `watch(() => props.value)` 감시 로직을 결합하여 값의 누락을 완벽히 방지함.
  - 로드 시에도 `titleSuffix.value = val && $te(val) ? $t(val) : undefined;`를 수행해 번역된 한글 명칭이 즉시 노출되도록 보강.

---

## 3) Implementation Plan (구현 계획)
- **Step 1**: `form.vue` 내 `CustomTitleInput` 정의부에 `innerValue` ref, `watch` 로직 추가 및 `watch` 임포트 복원
- **Step 2**: `drawerApi.onOpenChange` 내부에서 첫 번째 동기식 `formApi.setValues` 호출을 제거하고 `nextTick` 내부에서 `setValues`와 `clearValidate`를 세트로 구동
- **Step 3**: 프론트엔드 정적 타입 검증(`pnpm run typecheck`) 수행

---

## 4) Testing Plan (테스트 계획)
- **동작 및 네트워크 검증**: 수정 창이 열릴 때 `/name-exists` 및 `/path-exists` API가 불필요하게 쏘아지지 않고, 제목 입력란에는 번역키가, 컴포넌트 입력란에는 파싱된 `/system/company-user/index` 텍스트가 완벽하게 복원되어 표시되는지 검증.
