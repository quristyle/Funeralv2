# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:25
- **작업명**: 메뉴 제목 번역 명칭 노출 레이아웃 정밀화
- **예상 소요 시간**: 20분

## 1) Problem Summary (문제 요약)
- 메뉴 제목의 번역 결과값(`titleSuffix`)이 제목 입력 창의 바로 아래쪽 라인(하단 1열 라인)에 명확히 밀착되어 표시되도록 개선해야 함.
- 폼의 에러 검증 영역이나 폼 스위치 레이아웃 마진과 충돌하지 않도록, 커스텀 렌더 컴포넌트(`CustomTitleInput`)를 구현하여 입력부 바로 하단에 밀착 노출되도록 디자인함.

## Design Summary (설계 요약)
- **커스텀 입력 컴포넌트 (`CustomTitleInput`) 정의**:
  - `Input` 컴포넌트의 `addonAfter` 슬롯에 기존처럼 다국어 아이콘(지구본 버튼)을 제공하여 제목 입력란 오른쪽 끝 정렬을 유지함.
  - 입력창 노드 밑에 조건부로 번역 명칭 텍스트 노드(`h('div', { class: 'text-xs text-gray-400 mt-1 pl-1 text-left' }, titleSuffix.value)`)를 렌더링하여 하나의 컴포넌트로 묶음.
- **스키마 맵핑**:
  - `meta.title` 필드의 `component`를 `'Input'` 대신 `markRaw(CustomTitleInput)`로 지정.
  - Vben Form의 데이터 동기화(`v-model:value`) 및 이벤트 핸들링(`change`)을 투명하게 어댑팅함.

---

## 2) Implementation Plan (구현 계획)
- **Step 1**: `form.vue` 파일 상단 임포트문에 `defineComponent` 및 `markRaw` 추가
- **Step 2**: 파일 하단 `ant-design-vue` 임포트문에 `Input` 컴포넌트 추가
- **Step 3**: `schema` 선언부 바로 위에 `CustomTitleInput` 커스텀 컴포넌트 구현 정의
- **Step 4**: `schema` 배열 내 `meta.title` 필드의 설정을 `component: markRaw(CustomTitleInput)`로 변경하고, 기존 임시 `help` 속성 제거
- **Step 5**: 프론트엔드 정적 타입 검증(`pnpm run typecheck`) 수행을 통한 무결성 확인

---

## 3) Testing Plan (테스트 계획)
- **타입 분석**: `vue-tsc` 타입 검사를 수행하여 커스텀 컴포넌트 Props 및 이벤트 어댑팅 부근에 에러가 없는지 체크함.
- **동작 분석**: 메뉴 수정창을 열었을 때, 인풋 바로 밑에 회색 계열의 작고 깔끔한 번역 텍스트가 줄바꿈하여 제목 입력 박스 아래 밀착하여 정상 출력되는지 확인함.
