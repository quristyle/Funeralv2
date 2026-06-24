# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:20
- **작업명**: 메뉴 등록/수정 폼(form.vue) 레이아웃 및 컨트롤 개선
- **예상 소요 시간**: 30분

## 1) Problem Summary (문제 요약)
- `system/menu/modules/form.vue`에서 몇몇 입력란 공간이 협소함.
- "컴포넌트" 항목의 입력을 한 줄 전체로 확장하고, "제목" 항목 또한 한 줄 전체를 사용하며 번역 명칭을 필드 아래에 노출하도록 변경이 필요함.
- "다국어 아이콘"을 제목 입력부 우측 끝으로 이동하고, "정렬 순서" 항목을 아래쪽으로 재배치하며 넓은 너비의 `InputNumber` 컨트롤을 활용하게 개선함.

## 2) Design Summary (설계 요약)
- **컴포넌트 항목 (`component`)**:
  - `formItemClass`를 `'col-span-2 md:col-span-2'`로 변경하여 한 행을 독점 사용하도록 함.
- **제목 항목 (`meta.title`)**:
  - `formItemClass: 'col-span-2 md:col-span-2'`를 주어 한 줄 전체를 차지하게 함.
  - `help` 속성에 함수 `() => titleSuffix.value ? titleSuffix.value : ''`를 맵핑하여 번역된 문자열이 입력창 바로 아래 가독성 있게 나타나도록 개선.
  - `addonAfter` 렌더링에서 텍스트(`titleSuffix`)를 제거하고, 다국어 지구본 아이콘 버튼만 남겨 입력란 우측 끝에 깨끗하게 버튼형태로 제공.
- **정렬 순서 항목 (`meta.order`)**:
  - `schema` 배열 내 위치를 최상단에서 상태(`status`) 입력 항목 다음으로 이동.
  - `InputNumber` 컴포넌트 특성상 너비가 작게 나오므로 `componentProps: { class: 'w-full' }` 속성을 더해 전체 너비를 확보함.

---

## 3) Assumption-Based Interim Solution (가정 기반 임시 솔루션)
- **Assumption (가정)**: 번역된 명칭은 한글/영문 등 다국어 번역 결과 텍스트가 노출되는 것이므로 별도 텍스트 접두사 없이 문자열 자체만 `help`에 나타내며, `titleSuffix` 값이 없을 때는 헬프 텍스트 공간이 빈 값으로 동작하도록 한다.
- **Risk (위험)**: 텍스트가 없을 때 빈 줄로 인한 레이아웃 변형이 있을 수 있으나, Vben Form은 빈 헬프 텍스트 렌더링 시 레이아웃 무너짐을 방어하므로 안전함.
- **Fallback (대체 설계)**: 텍스트 무부 시 라벨 아래 공간을 더 좁히고 싶다면 조건부 렌더링 슬롯이나 custom 렌더러를 도입할 수 있으나, 표준 `help` 속성으로 우선 구현한다.

---

## 4) Implementation Plan (구현 계획)
- **Step 1**: `system/menu/modules/form.vue`에서 `schema` 내 `meta.order` 정의 부분을 `status` 하단으로 이동
- **Step 2**: `meta.order`에 `componentProps: { class: 'w-full' }` 추가
- **Step 3**: `meta.title` 스키마 항목에 `formItemClass: 'col-span-2 md:col-span-2'` 및 `help: () => titleSuffix.value ? titleSuffix.value : ''` 속성 부여
- **Step 4**: `meta.title` 스키마 항목의 `addonAfter` 렌더러에서 텍스트(`titleSuffix.value`) 렌더링 코드 제거
- **Step 5**: `component` 스키마 항목에 `formItemClass: 'col-span-2 md:col-span-2'` 추가
- **Step 6**: 프론트엔드 타입 체커(`pnpm run typecheck`)를 통한 빌드 무결성 확인 및 저장 검증

---

## 5) Testing Plan (테스트 계획)
- **정적 컴파일 검증**: 
  - `pnpm run typecheck`를 수행하여 `form.vue` 수정 후 문법 오류 및 타입 경고가 발생하지 않는지 검증.
- **레이아웃 확인**: 
  - 화면상에서 컴포넌트 명칭, 메뉴 제목 입력란이 정상적으로 전체 너비를 차지하고, 정렬순서 입력기가 `status` 아래에 full width로 노출되는지 점검.
