# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:40
- **작업명**: 메뉴 컴포넌트 입력 필드 인풋 그룹 애드온(ant-input-group-addon) 적용
- **예상 소요 시간**: 20분

## 1) Problem Summary (문제 요약)
- 메뉴의 "컴포넌트"(`component`) 입력란에 Ant Design Vue 본연의 인풋 그룹 애드온 스타일(`ant-input-group-addon`)이 올바르게 나타나도록 개선해야 함.
- 자동완성 컴포넌트(`AutoComplete`) 단독으로는 외부에 회색 애드온 박스가 붙지 않는 한계가 있으므로, 자식 슬롯에 애드온 설정(`#/views`, `.vue`)이 반영된 `Input` 컴포넌트를 주입하여 네이티브 스타일 마크업을 트리거함.

## 2) Design Summary (설계 요약)
- **커스텀 자동완성 컴포넌트 (`CustomComponentInput`) 개발**:
  - `AutoComplete` 컴포넌트의 자식(default slot)으로 `h(Input, { addonBefore: '#/views', addonAfter: '.vue', value: props.value })`를 주입.
  - 이를 통해 브라우저가 자동으로 좌측 회색 애드온 `#/views`와 우측 회색 애드온 `.vue`를 인풋 주위에 감싸는 `ant-input-group` 클래스 규격대로 그리도록 유도함.
- **스키마 연동**:
  - `component` 스키마 항목의 `component` 속성을 `'AutoComplete'` 대신 `markRaw(CustomComponentInput)`로 맵핑.

---

## 3) Implementation Plan (구현 계획)
- **Step 1**: `form.vue` 내에 `AutoComplete` 임포트 추가 (이미 하단 `ant-design-vue` 임포트 영역에 `Input`, `Button`이 있으므로 거기에 추가)
- **Step 2**: 파일 상단 `CustomTitleInput` 정의 바로 아래에 `CustomComponentInput` 선언 추가
- **Step 3**: `schema` 배열 내 `component` 필드 설정을 `component: markRaw(CustomComponentInput)`로 변경하고, 기존 `addonBefore`/`addonAfter` 스키마 속성 제거 (커스텀 인풋에 이식되었으므로)
- **Step 4**: 정적 컴파일 검증(`pnpm run typecheck`) 수행

---

## 4) Testing Plan (테스트 계획)
- **동작 분석**: 수정 후 드로워를 열었을 때, 컴포넌트 경로 입력 창 좌우측에 정상적으로 회색 애드온 블록이 렌더링되며 자동완성 목록도 깨지지 않고 동일하게 나타나는지 확인.
