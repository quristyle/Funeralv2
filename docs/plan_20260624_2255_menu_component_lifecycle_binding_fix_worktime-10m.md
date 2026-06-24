# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:55
- **작업명**: 메뉴 컴포넌트 비동기 마운트 시점 값 유실 수정 (nextTick 바인딩)
- **예상 소요 시간**: 10분

## 1) Problem Summary (문제 요약)
- 메뉴 수정 창이 열릴 때 보관된 컴포넌트 경로(예: `/system/company-user/index`)가 인풋에 채워지지 않고 비어있는 현상이 지속됨.
- `component` 입력 필드가 `type === 'MENU'` 조건에 의존하는 동적 필드이기 때문에, `formApi.setValues` 호출 시점에 아직 폼 아이템 DOM 마운트가 완료되지 않아 바인딩 값이 증발하는 것으로 분석됨.

## 2) Design Summary (설계 요약)
- **비동기 렌더 틱 동기화 (`nextTick` 활용)**:
  - `onOpenChange(isOpen)` 진입 후 폼 값을 세팅할 때, Vue의 렌더링 주기가 완료된 후 값을 밀어 넣도록 `nextTick(() => { formApi.setValues(formValues); })`을 수행.
  - 이를 통해 `type` 필드 세팅으로 인해 `component` 필드가 화면에 마운트(노출)된 뒤에 정밀하게 값을 주입하도록 시점을 조정하여 값 소실을 원천적으로 막음.
- **임포트 보강**:
  - `vue`로부터 `nextTick`을 추가 임포트하여 정적 에러를 차단함.

---

## 3) Implementation Plan (구현 계획)
- **Step 1**: `form.vue` 상단 임포트문에 `nextTick` 추가
- **Step 2**: `drawerApi.onOpenChange` 내부에서 `formApi.setValues(formValues)` 호출을 `nextTick` 콜백 구조로 이중 방어하여 실행
- **Step 3**: 프론트엔드 정적 타입 검증(`pnpm run typecheck`) 수행

---

## 4) Testing Plan (테스트 계획)
- **수정 드로워 오픈 검증**: 수정 버튼을 누르고 드로워가 슬라이딩되어 열리는 순간, 컴포넌트 입력 필드에 `/system/company-user/index` 텍스트가 정상 안착되는지 확인.
