# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:35
- **작업명**: 메뉴 컴포넌트 경로 접두사/접미사 자동 결합 및 파싱 처리
- **예상 소요 시간**: 30분

## 1) Problem Summary (문제 요약)
- 메뉴의 "컴포넌트"(`component`) 입력란 앞뒤에 각각 `#/views`와 `.vue`를 고정 데코레이터로 표시하여 경로 입력의 편의성을 개선해야 함.
- 저장 시에는 입력된 값 앞뒤로 `#/views`와 `.vue`가 결합된 완전한 경로 형태로 서버에 보관하고, 데이터 로드 시에는 저장된 전체 경로에서 앞뒤 접사들을 제거한 순수 경로명만 입력란에 매핑 노출해야 함.

## 2) Design Summary (설계 요약)
- **UI 데코레이터 적용 (`component` 필드)**:
  - Ant Design Vue `AutoComplete` 컴포넌트는 `Input`을 내장하고 있어 `addonBefore` 및 `addonAfter` 슬롯(또는 props)을 제공할 수 있음.
  - Vben Form Schema의 `componentProps` 또는 `renderComponentContent` 슬롯을 활용하여 자동완성 컴포넌트 전후에 접두사 `#/views`와 접미사 `.vue`를 배치.
- **수정 모드 데이터 로드 시 파싱 (onOpen)**:
  - `drawerApi.onOpenChange` 내에서 데이터 셋업 시, `data.component` 값이 존재하고 `#/views`로 시작하며 `.vue`로 끝난다면 앞뒤를 잘라낸 경로(예: `/xxxx/yyyy/index` 또는 `xxxx/yyyy/index`)로 가공해 `formApi.setValues`에 바인딩함.
  - *예시*: `#/views/system/menu/list.vue` -> `system/menu/list`
- **저장(제출) 시 접사 결합 (onSubmit)**:
  - `onSubmit` 시 `formApi.getValues`로 읽어온 값 중 `component` 필드 값이 존재한다면, 앞뒤에 `#/views`와 `.vue`가 붙어있지 않은 경우에 한해 결합 처리를 수행한 후 저장 API를 호출함.
  - *예시*: `system/menu/list` -> `#/views/system/menu/list.vue`

---

## 3) Assumption-Based Interim Solution (가정 기반 임시 솔루션)
- **Assumption (가정)**: 데이터 로드 시 가져온 `component` 값이 비어있거나 접사 규격(`#/views`, `.vue`)을 따르지 않는 임의의 값(예: 타 링크나 빈 값)일 경우, 안전하게 파싱을 건너뛰고 원본 상태 그대로 입력란에 노출한다.
- **Risk (위험)**: 간혹 다른 형식의 컴포넌트 경로가 이미 저장되어 있을 때, 잘못 파싱되어 일부 유실될 위험이 있음.
- **Fallback (대체 설계)**: 파싱 로직에 정규식 가드를 정교하게 입혀 `startsWith('#/views')` 및 `endsWith('.vue')` 조건을 확실히 타는 경우에만 슬라이싱(`substring`) 처리하도록 방어 코드를 작성한다.

---

## 4) Implementation Plan (구현 계획)
- **Step 1**: `form.vue` 내 `component` 스키마 항목에 `addonBefore: '#/views'`, `addonAfter: '.vue'`를 `componentProps`로 전달하여 입력란 앞뒤에 텍스트 붙이기
- **Step 2**: 드로워가 열려 데이터를 세팅하는 `drawerApi.onOpenChange(isOpen)` 내부 블록에서 `formData.value` 가공 처리 추가 (앞뒤 잘라내기)
- **Step 3**: 데이터를 전송하는 `onSubmit` 함수 내부 블록에서 `data.component` 가공 처리 추가 (앞뒤 이어붙이기)
- **Step 4**: 프론트엔드 정적 컴파일 및 타입 에러 여부(`pnpm run typecheck`) 검증 실행

---

## 5) Testing Plan (테스트 계획)
- **파싱 유닛 시나리오**:
  - 입력값: `#/views/system/menu/list.vue` -> 파싱결과: `/system/menu/list` 또는 `system/menu/list` (정상 파싱)
  - 입력값: `CustomComponent` -> 파싱결과: `CustomComponent` (스킵)
- **결합 유닛 시나리오**:
  - 입력값: `system/menu/list` -> 결합결과: `#/views/system/menu/list.vue`
  - 입력값: `#/views/system/menu/list.vue` -> 결합결과: `#/views/system/menu/list.vue` (중복 결합 방지)
