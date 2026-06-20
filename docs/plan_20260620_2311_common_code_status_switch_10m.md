# 작업 계획서: 공통코드 상태 컴포넌트 스위치(Switch) 전환 및 양방향 변환 적용

## 1) 문제 요약
* **요구 사항**: 공통코드 상태 필드를 라디오 그룹 대신 스위치(Switch) 컴포넌트로 변경하여 직관적인 토글 작동 방식으로 UX를 개선합니다.
* **기술적 과제**:
  * 백엔드 API는 `status` 필드를 정수(`1`: 사용, `0`: 미사용)로 입출력합니다.
  * 프론트엔드의 `'Switch'` 컴포넌트는 불리언(`true`/`false`) 값을 핸들링하므로, 백엔드 정수 데이터와 프론트엔드 불리언 데이터 사이의 양방향 형변환 파이프라인 처리가 누락되면 값이 오작동하거나 통신 에러가 생깁니다.

---

## 2) 설계 요약
* **수정 대상 파일**:
  1. `fronts/apps/funeralv2/src/views/system/common-code/data.ts`
  2. `fronts/apps/funeralv2/src/views/system/common-code/modules/code-form.vue`
* **설계 상세**:
  * **스키마 변경 (`data.ts`)**:
    * `status` 컴포넌트를 `'Switch'`로 변경하고 스위치 라벨(`checkedChildren: '사용'`, `unCheckedChildren: '미사용'`) 및 `defaultValue: true`를 지정합니다.
  * **양방향 형변환 적용 (`code-form.vue`)**:
    * **DB -> UI (오픈 시)**: `openModal` 시 백엔드 레코드의 `status`가 `1`이면 `true`, `0`이면 `false`로 치환한 사본 객체(`bindRecord`)를 만들어 `formApi.setValues`에 주입합니다.
    * **UI -> DB (저장 시)**: `onConfirm` 시 `formApi.getValues()`의 `status` 값이 `true`이면 `1`, `false`이면 `0`으로 가공하여 백엔드 수정/생성 API에 전달합니다.

---

## 3) 구현 계획
* **Task 1: data.ts 스키마 스위치 변경** (완료)
* **Task 2: code-form.vue 양방향 변환 적용 및 리셋 처리** (완료)
* **Task 3: 컴파일 및 빌드 테스트** (진행 중)

---

## 4) 자가 코드 리뷰 계획
* 스위치 컴포넌트 바인딩 시 `status` 값이 숫자로 들어왔을 때 오작동하지 않도록 확실한 불리언 형변환 처리가 되었는지 검토합니다.
* 스위치를 토글한 뒤 저장할 때 백엔드로 전달되는 페이로드(Payload) 내부의 `status` 값이 `1` 또는 `0` 정수로 완벽히 인코딩되는지 분석합니다.
