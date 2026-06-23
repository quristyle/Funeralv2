# Implementation Plan - 역할 관리 화면 기능 통합

---

## 1. 개요 (Problem Summary)
- 현재 역할 관리 기능(`list.vue`)과 권한 설정 기능(`index.vue`)이 분리되어 있어 사용성이 떨어짐.
- `index.vue`의 역할 목록(좌측 그리드)에서 직접 역할 생성, 수정, 삭제를 수행할 수 있도록 화면을 통합하고 기존 `list.vue`를 삭제하고자 함.

## 2. 설계 요약 (Design Summary)
- **목적**: 역할 목록에서 CRUD 기능 직접 수행 및 화면 간결화.
- **입력**: 역할 정보 입력 Form 및 삭제 시 Confirm 확인.
- **출력**: 성공/실패 토스트 메시지 및 그리드 데이터 동적 갱신.
- **예외**: 역할 ID 중복 검사 실패 시 에러 팁 노출 및 API 요청 오류 발생 시 안내 메시지.
- **주요 모듈**:
  - `index.vue`: 왼쪽 그리드에 CRUD 기능 통합.
  - `form.vue`: 역할 생성/수정을 위한 Form Drawer 컴포넌트.
  - `system.ts`: 라우팅 대상 컴포넌트를 `list.vue`에서 `index.vue`로 변경.

## 3. 구현 계획 (Implementation Plan)
1. **역할 CRUD 기능 결합 (`index.vue`)**:
   - `useVbenDrawer`와 `form.vue`를 활용하여 생성/수정용 Drawer 정의.
   - 그리드 컬럼에 수정/삭제 팝오버 확인(`Popconfirm`) 버튼 추가.
   - 카드 헤더 오른쪽에 "역할 생성" 버튼을 추가하기 위해 `<template #extra>` 배치.
2. **라우터 설정 변경 (`system.ts`)**:
   - `/system/role` 주소에 매핑된 컴포넌트를 `index.vue`로 수정.
3. **사용하지 않는 파일 삭제**:
   - `list.vue` 파일 삭제.

## 4. 테스트 계획 (Testing Summary)
- **역할 생성**: 신규 역할 정보 입력 후 정상 생성 여부 및 중복 역할 ID 예외 처리 확인.
- **역할 수정**: 기존 역할 정보 로드 및 수정 성공 확인.
- **역할 삭제**: Popconfirm 확인 후 삭제 정상 작동 및 현재 활성화된 역할 삭제 시 선택 해제 확인.
