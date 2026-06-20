# 작업 계획서: 공통코드 수정 시 입력 제한 및 정렬순서/상태 컴포넌트 개선

## 1) 문제 요약
* **요구 사항**:
  1. 코드 수정 모드일 때 고유키인 `코드값`을 수정할 수 없도록 차단(disabled)합니다.
  2. 수정 모드일 때는 `AiCodeSuggester` 컴포넌트가 화면에 나타나지 않도록 가드(v-if)를 씌웁니다.
  3. `정렬순서` 필드에서 정확한 정수 입력을 보장하기 위해 `InputNumber` 스펙에 정수 연산 가이드(precision, step)를 보강합니다.
  4. `상태` 필드의 조작 인터페이스를 기존 드롭다운(Select)에서 라디오 버튼 그룹(RadioGroup)으로 직관성을 개선합니다.

---

## 2) 설계 요약
* **수정 대상 파일**: 
  1. `fronts/apps/funeralv2/src/views/system/common-code/data.ts` (정렬순서 및 상태 필드 컴포넌트 스펙 변경)
  2. `fronts/apps/funeralv2/src/views/system/common-code/modules/code-form.vue` (수정 모드에 따른 코드값 필드 비활성화 및 AI 추천 노출 조건 처리)
* **설계 상세**:
  * **스키마 변경 (`data.ts`)**:
    * `sortOrder`: `precision: 0`, `step: 1` 속성을 지정하여 정수만 허용합니다.
    * `status`: `component`를 `'RadioGroup'`으로 바인딩하여 1/0 상태값을 라디오 형태로 즉시 토글 선택하게 합니다.
  * **수정 제약 부여 (`code-form.vue`)**:
    * `openModal` 시 수정 상황일 경우 `formApi.updateSchema`로 `codeValue` 필드를 `disabled: true` 처리합니다.
    * 모달이 닫힐 때(`onOpenChange` false) 스키마 리셋 및 `isUpdate` 상태를 해제하여 오작동을 차단합니다.
    * 템플릿의 `<AiCodeSuggester>` 컴포넌트에 `v-if="!isUpdate"`를 부여합니다.

---

## 3) 구현 계획
* **Task 1: data.ts 스키마 스펙 업데이트** (완료)
* **Task 2: code-form.vue 폼 및 스키마 업데이트 로직 보완** (완료)
* **Task 3: 컴파일 및 빌드 테스트** (진행 중)

---

## 4) 자가 코드 리뷰 계획
* 추가 모드로 다시 열었을 때 `codeValue` 비활성화가 완전히 해제(disabled: false)되는지 복구 생명주기를 점검합니다.
* 라디오 버튼과 정밀 정수 컴포넌트(`InputNumber`)의 렌더링 무결성을 체크합니다.
