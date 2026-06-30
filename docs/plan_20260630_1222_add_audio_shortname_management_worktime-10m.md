# 구현 계획서: 음원 관리 화면 짧은명칭(ShortName) 관리 기능 추가

- **작성일시**: 2026-06-30 12:22
- **예상 소요 시간**: 10분

---

## 1. 문제 요약
음원 관리 화면(`audio/index.vue`) 및 등록/수정 팝업(`audio-upload-modal.vue`)에 음원의 `짧은 명칭 (ShortName)` 컬럼 및 입력 폼 필드를 추가하여, 장비 속성 등에서 식별 가능한 형태로 짧은 명칭을 관리할 수 있도록 합니다.

---

## 2. 디자인 요약
- **UI/UX**:
  - 음원 목록 테이블의 명칭 컬럼 옆에 `짧은 명칭 (shortName)` 열을 배치하여 빠르게 확인할 수 있도록 합니다.
  - 신규 음원 등록 및 수정 모달의 "음원 명칭" 인풋 하단에 "짧은 명칭" 입력 폼(`Form.Item`)을 추가합니다.
- **데이터 흐름**:
  - 백엔드 DTO 및 서비스에는 이미 `ShortName` 매핑이 구성되어 있으므로, 프론트엔드의 `formModel` 상태에 `shortName` 프로퍼티를 추가하고 API 요청 시 전달되도록 매핑합니다.

---

## 3. 구현 계획
- **Step 1: 음원 목록 테이블 컬럼 추가**
  - `audio/index.vue` 내 vxe-grid의 `columns`에 `shortName` 컬럼 설정 추가.
- **Step 2: 음원 등록/수정 모달에 ShortName 필드 및 인풋 추가**
  - `audio-upload-modal.vue` 내 `formModel` 상태 변수 정의에 `shortName` 추가.
  - `open` 함수의 에디트 모드 및 등록 모드 초기화 시 데이터 매핑 추가.
  - `template` 의 `Form.Item` 인풋 레이아웃 배치 추가.
