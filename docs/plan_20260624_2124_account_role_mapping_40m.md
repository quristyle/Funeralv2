# 구현 계획서 - 사용자 계정 및 역할 매핑 기능 추가

- **작성일시**: 2026-06-24 21:24
- **예상 작업 시간**: 40분 (worktime-40m)

---

## 1) Problem Summary
- 사용자 관리 화면(`account/index.vue`)에 역할 컬럼이 없어 각 사용자에게 어떤 권한(역할)이 할당되어 있는지 조회하기 어려움.
- 계정 생성/수정 팝업에서 사용자의 역할을 추가하거나 제거할 수 있는 수단이 없음.
- 백엔드 계정 DTO 및 서비스에서 역할 정보를 누락하고 있어 해당 연동 로직 보완이 필요함.

---

## 2) Design Summary
- **UI/UX**:
  - **사용자 목록 그리드**: '역할' 컬럼을 추가하고, Ant Design Vue의 `Tag` 컴포넌트를 사용하여 할당된 역할을 시각적으로 표시.
  - **계정 등록/수정 팝업**: 다중 선택(Select multiple) 컴포넌트를 제공하여 사용자에게 여러 역할을 할당하거나 제거할 수 있도록 지원.
- **백엔드 DTO 및 서비스**:
  - `AccountDto`, `CreateAccountDto`, `UpdateAccountDto` 에 `RoleIds` 및 `RoleNames` 속성 추가.
  - `UserService` 의 `GetAccountsAsync`, `CreateAccountAsync`, `UpdateAccountAsync` 에서 `RoleAccount` 테이블을 연계하여 사용자와 역할 매핑 데이터를 관리하도록 수정.
- **프론트엔드 API 및 스키마**:
  - `SystemAccountApi.Account` 에 역할 속성 추가.
  - `system/account/data.ts` 의 테이블 컬럼과 폼 스키마 정의 수정.
  - `system/account/index.vue` 에서 역할 목록 API(`getRoleList`) 호출 및 폼 스키마에 바인딩.

---

## 3) Implementation Plan

### Task 1: 백엔드 DTO 수정 및 서비스 연동 로직 수정
- **대상 파일**:
  - `microservices/AuthServer/DTOs/AccountDto.cs`
  - `microservices/AuthServer/Services/UserService.cs`
- **구현 내용**:
  - DTO 파일에 `RoleIds`, `RoleNames` 속성 추가.
  - `UserService.GetAccountsAsync()`에서 `role_accounts`와 `roles` 테이블을 로드하여 각 DTO에 역할 정보 맵핑.
  - `CreateAccountAsync` 및 `UpdateAccountAsync`에서 `RoleIds` 배열 정보를 받아 `role_accounts` 테이블의 매핑 데이터를 업데이트(삭제 후 일괄 삽입 등) 처리.

### Task 2: 프론트엔드 API 인터페이스 및 데이터 정의 수정
- **대상 파일**:
  - `fronts/apps/funeralv2/src/api/system/account.ts`
  - `fronts/apps/funeralv2/src/views/system/account/data.ts`
- **구현 내용**:
  - `SystemAccountApi.Account`에 `roleIds`, `roleNames` 필드 추가.
  - `useColumns`에 '역할' 컬럼(`slots: { default: 'role-tag' }`) 추가.
  - `useSchema`에 다중 선택(`Select`, `mode: 'multiple'`) 컴포넌트를 사용하는 `roleIds` 폼 아이템 추가.

### Task 3: 프론트엔드 뷰 컴포넌트 수정
- **대상 파일**:
  - `fronts/apps/funeralv2/src/views/system/account/index.vue`
- **구현 내용**:
  - `getRoleList` API 임포트 및 온마운트 시 역할 목록 조회.
  - 조회한 역할 목록 데이터를 Select 옵션(`options`) 데이터로 폼 스키마에 동적 업데이트(`formApi.updateSchema`).
  - 그리드 템플릿에 `#role-tag` 슬롯을 추가하여 `Tag` 컴포넌트로 역할명 렌더링.
  - `onOpenChange`에서 수정 시 `roleIds` 기본값을 바인딩하고, `handleSave`에서 `roleIds`를 페이로드에 포함하여 호출.

---

## 4) Testing Plan
- **단위 테스트 및 통합 확인**:
  - 계정 목록 조회 시 역할 컬럼에 사용자의 역할명이 태그로 잘 출력되는지 확인.
  - 신규 계정 생성 시 특정 역할을 부여하고 DB 매핑 상태 확인.
  - 기존 계정 수정 시 역할을 제거하거나 추가한 결과가 그리드에 즉시 반영되는지 검증.
