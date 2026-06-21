# Implementation Plan - 계정 관리 화면 리팩토링 및 `data.ts` 분리

- **작업명**: 계정 관리 화면 리팩토링 및 스키마 분리
- **작업 시간**: 약 30분 (Estimated: 30m)
- **작업자**: Lead Engineer Agent
- **일시**: 2026-06-21 23:20

---

## 1. 개요 (Problem Summary)
`fronts/apps/funeralv2/src/views/system/account/index.vue`의 Grid 컬럼 설정 및 수정/등록 Form 구조가 한 파일 내에 모여 있어 코드의 복잡성이 높고 유지보수가 어렵습니다. 이를 `system/company` 등의 타 도메인과 마찬가지로 `data.ts` 파일로 스키마(컬럼 및 폼)를 분리하고, `useVbenForm` 기반으로 개선합니다.

---

## 2. 설계 요약 (Design Summary)
- **목적**: 코드 가독성 향상, Vben Admin 아키텍처 규칙 준수, 스키마 일관성 유지.
- **입력**: 
  - 계정 목록 데이터 (API)
  - 부서 목록 데이터 (API, Form Select Option용)
- **출력**: 
  - VXE Grid를 통한 사용자 계정 목록 렌더링
  - VbenForm을 탑재한 모달을 통한 계정 CRUD
- **핵심 모듈**:
  - `data.ts`: VXE Grid 컬럼 설정(`useColumns`), VbenForm 설정(`formSchema`)
  - `index.vue`: 목록 및 모달을 관리하며 `data.ts`에서 가져온 스키마로 화면을 조립하고 API와 상태를 바인딩

---

## 3. 구현 계획 (Implementation Plan)

### Step 1: 다국어(i18n) 키 정의
- `fronts/apps/funeralv2/src/locales/langs/ko-KR/system.json` 및 `en-US/system.json`에 `account` 관련 다국어 리소스 추가.
- 키 명칭: `system.account.*`

### Step 2: `data.ts` 생성
- 경로: `C:/Funeralv2/fronts/apps/funeralv2/src/views/system/account/data.ts`
- 내용:
  - `useColumns(onActionClick)`: 액션 이벤트(수정/삭제)를 처리하는 헬퍼 함수를 주입받아 VXE Grid Column Array 반환.
  - `formSchema`: 계정 관리 폼 정의 (`loginId`, `userName`, `deptId`, `email`, `phone`, `status`).

### Step 3: `index.vue` 리팩토링
- **Grid 설정**:
  - `columns` 속성을 `data.ts`의 `useColumns`로 대체.
  - 테이블 내 액션 버튼 핸들링 방식을 `onActionClick`으로 통합.
- **Form 및 Modal 설정**:
  - 기존의 수동 `<Form>` `<Form.Item>` 마크업 제거.
  - `useVbenForm`을 정의하여 `data.ts`의 `formSchema` 연동.
  - 모달의 `onConfirm` 이벤트 발생 시 `formApi.validateAndSubmitForm()` 호출.
  - `handleSubmit`을 통해 수정/생성 API (`createAccount`, `updateAccount`) 호출 및 성공 메시지 노출.
  - `onOpenChange` 및 `onCreate`/`onEdit` 시점 제어:
    - 신규 등록 시: `loginId` 필드 `disabled: false` 처리, 폼 리셋.
    - 수정 시: `loginId` 필드 `disabled: true` 처리, 폼 값 세팅 (`formApi.setValues`).
  - 부서 목록 조회(`fetchDepts`) 성공 후, `formApi.updateSchema`를 호출하여 `deptId` 필드의 `options` 동적 업데이트.

---

## 4. 테스트 계획 (Testing Plan)
- **정상 케이스**:
  - 계정 목록 조회 및 정상 렌더링 확인 (계정 상태 Badge 포매팅)
  - 신규 계정 등록 시 필수값 미기입 유효성 검사 작동 여부 확인
  - 신규 계정 등록 정상 작동 확인
  - 계정 수정 시 ID 수정 불가(Disabled) 상태 및 정상 저장 작동 확인
  - 계정 삭제 정상 작동 확인
- **에외 케이스**:
  - 부서 목록 로드 실패 시 예외 처리 및 에러 메시지 확인
  - 계정 저장 실패 시 API 예외 처리 확인
