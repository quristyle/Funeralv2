# 작업 계획서: 공통코드 그룹 수정 팝업창 모달 개선

## 1) 문제 요약
* **현상**: 공통코드 관리 화면에서 코드 그룹을 수정하기 위해 편집 단추를 눌렀을 때, 수정 모드로 데이터가 채워져서 나타나지 않고 신규 코드 그룹 추가(Create) 모드의 빈 화면으로 팝업창이 뜨는 버그가 존재합니다.
* **원인**:
  1. 백엔드 `CommonCodeEndpoints.cs`에 코드 그룹 수정 엔드포인트(`PUT /groups/{id}`)가 누락되어 있었습니다.
  2. 프론트엔드 API 클라이언트(`common-code.ts`)에 `updateCommonCodeGroup` 함수 정의가 빠져 있었습니다.
  3. `group-form.vue`에서 팝업 오픈 파라미터(`record`)를 받아 수정 모드로 변환하는 분기(타이틀 바인딩, 폼 값 자동 채우기)가 부재했습니다.

---

## 2) 설계 요약
* **수정 대상 파일**: 
  1. `microservices/AuthServer/Endpoints/CommonCodeEndpoints.cs`
  2. `fronts/apps/funeralv2/src/api/system/common-code.ts`
  3. `fronts/apps/funeralv2/src/views/system/common-code/modules/group-form.vue`
* **설계 상세**:
  * **백엔드**: `group.MapPut("/groups/{id}", ...)`을 통해 `ICommonCodeService.UpdateGroupAsync`를 엔드포인트로 연동합니다.
  * **프론트 API**: `updateCommonCodeGroup`을 추가해 `PUT /auth/system/common-code/groups/{id}`를 호출하도록 매핑합니다.
  * **프론트 모달 (`group-form.vue`)**:
    * `isEdit` 및 `editRecordId` 상태 변수를 도입합니다.
    * `openModal(record)`가 들어올 경우 `isEdit`를 참으로 하고, `nextTick`을 사용하여 폼 렌더링이 완료된 후 `formApi.setValues(record)`로 기존 값을 채웁니다.
    * `onConfirm` 시 `isEdit` 여부에 따라 `create` 또는 `update` API를 다르게 호출합니다.
    * `onOpenChange(isOpen)`이 `false`로 닫힐 때 폼과 상태를 완전히 초기화(reset)하여 추가/수정 상태의 사이드이펙트 혼선을 방지합니다.

---

## 3) 구현 계획
* **Task 1: 백엔드 API 엔드포인트 보완** (완료)
* **Task 2: 프론트 API 정의 추가** (완료)
* **Task 3: group-form.vue 폼 수정 처리** (완료)
* **Task 4: 검증 및 컴파일 체크** (진행 중)

---

## 4) 자가 코드 리뷰 계획
* 수정 팝업이 나타날 때 데이터 복구 및 리셋이 확실하게 이루어지는지 라이프사이클을 추적합니다.
* 닫힘 액션 시 `editRecordId` 및 `isEdit` 상태가 초기화되어 다음 "추가" 클릭 시 수정 모드가 꼬여서 나타나지 않는지 체크합니다.
* TypeScript strict 모드 하에서 캐스팅(`as unknown as CommonCodeGroupParams`)을 사용해 타입 적합성을 충족합니다.
