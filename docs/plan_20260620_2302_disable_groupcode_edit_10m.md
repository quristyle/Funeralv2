# 작업 계획서: 공통코드 그룹 수정 시 그룹코드 편집 차단 및 AI 추천 제거

## 1) 문제 요약
* **요청 사항**:
  1. 코드 그룹 수정(Edit) 시에는 데이터의 핵심 고유키인 "그룹코드" 값을 사용자가 변경할 수 없도록 수정 불가능(disabled) 처리해야 합니다.
  2. 수정 모드일 때는 인공지능 코드 추천을 받을 필요가 없으므로 `AiCodeSuggester` 컴포넌트가 화면에 나타나지 않아야 합니다.

---

## 2) 설계 요약
* **수정 대상 파일**: 
  * `fronts/apps/funeralv2/src/views/system/common-code/modules/group-form.vue`
* **설계 상세**:
  * **그룹코드 편집 차단**:
    * `openModal(record)`가 수정 모드로 실행될 때 `formApi.updateSchema`를 호출하여 `groupCode` 필드의 `componentProps` 내 `disabled` 속성을 `true`로 설정하고 안내 플레이스홀더를 변경합니다.
    * 추가 모드로 진입하거나 모달이 완전히 닫힐 때(`onOpenChange` false) 스키마를 원래 상태(`disabled: false`)로 초기화하여 다음 동작 시의 레이아웃 부작용을 원천 봉쇄합니다.
  * **AI 코드 추천기 제거**:
    * 템플릿의 `<AiCodeSuggester>` 태그에 `v-if="!isEdit"` 속성을 바인딩하여 수정 상태일 때는 돔(DOM)에서 제거합니다.

---

## 3) 구현 계획
* **Task 1: group-form.vue 스키마 업데이트 로직 보완**
  * `openModal` 및 `onOpenChange` 함수 내에 `formApi.updateSchema` 동작을 구현합니다.
* **Task 2: group-form.vue 템플릿 v-if 조건부 렌더링**
  * `<AiCodeSuggester>` 컴포넌트에 `v-if="!isEdit"`를 적용합니다.
* **Task 3: 컴파일 및 빌드 테스트**
  * 린트 및 번들러 빌드 무결성을 체크합니다.

---

## 4) 자가 코드 리뷰 계획
* 추가(Create) 시에는 그룹코드가 활성화되고, 수정(Edit) 시에는 명확히 비활성화되는지 상태 롤백 처리를 추적 검사합니다.
* `isEdit` 상태의 변화에 따라 템플릿 상에서 `AiCodeSuggester` 마운트가 해제 및 재장착되는지 라이프사이클을 점검합니다.
