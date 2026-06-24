# 작업 계획서: 메뉴 폼 데이터 바인딩 개선 및 불필요한 API 호출 최적화

- **작성일**: 2026년 6월 24일 22시 55분
- **예상 소요 시간**: 20분

## 1. 문제 요약
- 메뉴 수정 드로워(`form.vue`) 오픈 시, 백엔드 중복 체크 API(`/auth/system/menu/name-exists`, `/auth/system/menu/path-exists`) 및 메뉴 목록 조회 API가 불필요하게 여러 번 호출됨.
- 수정 드로워 진입 시 제목 필드의 번역키와 컴포넌트 경로 필드의 값이 빈 값으로 노출되거나 누락되는 현상 발생.

## 2. 원인 분석
- `CustomTitleInput`과 `CustomComponentInput` 커스텀 컴포넌트가 `modelValue` prop 및 `update:modelValue` 이벤트를 양방향 바인딩하지 않고 `value`만 수용하여 Vben Form의 주입 데이터를 정상적으로 읽지 못함.
- `onOpenChange` 함수에서 `formApi.setValues`를 동기/비동기(`nextTick`)로 연속 2회 호출하여 각종 중복 체크 validation과 `ApiTreeSelect` 조회 동작이 연속으로 트리거됨.
- `refine` 비동기 validation 규칙 내부에 폼이 데이터 주입 중인 상황(초기화 상태) 및 빈 값일 때의 예외 스킵 처리가 누락되어 불필요한 네트워크 요청이 백엔드로 전송됨.

## 3. 해결 설계 및 구현 계획
1. **커스텀 컴포넌트 양방향 바인딩 보강**:
   - `CustomTitleInput` 및 `CustomComponentInput`에 `modelValue` 프롭 수용 및 `update:modelValue` emit 지원 추가.
   - `CustomTitleInput` 내부에서 값이 바인딩될 때 하단 번역 결과 텍스트를 즉시 반응형으로 업데이트하도록 `watch` 추가.
2. **데이터 초기 주입 시점 최적화 및 중복 호출 차단**:
   - `isBinding` 플래그(`ref`) 변수를 도입하여, 수정창 오픈 시 데이터 주입이 끝날 때까지 검사 규칙(`refine`)을 스킵하도록 처리.
   - `onOpenChange`에서 `setValues` 호출을 `nextTick` 내부에서 단 1회만 호출하도록 일원화하여 불필요한 호출 횟수 감소.
3. **Zod 검증 예외 가드 추가**:
   - `value`가 2자 미만이거나 형식이 맞지 않는 초기 상태, 혹은 `isBinding` 상태일 때는 중복 체크를 즉시 통과하도록 하여 백엔드 API 요청 원천 방지.
