# Changelog

## [Unreleased] - 2026-06-21
### Added
- AuthServer 계정 관리 CRUD API 추가 (`/system/account/*`)
- `AccountDto`, `CreateAccountDto`, `UpdateAccountDto` 정의
- fronts 계정 관리 화면(`system/account`)에 대한 `data.ts` 추가 (컬럼 및 폼 스키마 분리)

### Changed
- `IUserService` 및 `UserService` 계정 CRUD 기능 구현
- fronts 계정 관리 API 엔드포인트 `/auth/system/account/...` 경로로 수정 및 실제 DB 연동
- fronts 계정 관리 화면(`system/account/index.vue`) 리팩토링: `useVbenForm` 적용, 하드코딩 마크업 제거 및 액션 버튼(수정/삭제)의 아이콘/툴팁화 개선
- ko-KR/en-US `system.json` 다국어 키에 `account` 관련 리소스 추가
- fronts 메뉴 관리 화면(`system/menu/modules/form.vue`)에서 메뉴 수정 시 자기 자신의 이름(Name) 및 경로(Path)를 중복 검증에서 제외하도록 로직 개선
- activePath 유효성 검사 시 자기 자신을 포함하여 유효성을 판별하도록 id 제외 처리
- fronts 공통코드 화면(`system/common-code/composables/use-common-code.ts`)에서 API 응답(ApiResponse) 구조의 result(배열) 데이터를 그리드에 언래핑하여 바인딩함으로써 TypeError(datas.slice) 픽스
- fronts 다국어 편집 모달(`I18nEditModal.vue`)에 자연스러운 영어 번역 추천 기능 연동
- fronts AI 코드 추천 컴포넌트(`AiCodeSuggester.vue`) 및 API에 자연스러운 영문(natural) 옵션 전달 규격 추가
- AIAgentServer 백엔드(/suggest-code)에 natural 옵션 매핑 및 프롬프트 분기(자연스러운 영문 단어/문장 번역 vs SNAKE_CASE 코드) 처리 구현
- fronts AI 추천 컴포넌트(`AiCodeSuggester.vue`)에서 API 응답의 result 배열 첫 번째 요소를 정상적으로 추출하여 UI 및 바인딩에 반영하도록 보정 (JSON 객체가 통째로 표출되는 현상 방지)
- fronts 공통코드 그룹 관리 등록 모달(`group-form.vue`) 내 중복 하드코딩된 폼 스키마를 제거하고, data.ts에서 분리 선언된 groupFormSchema 데이터를 직접 매핑하여 사용하도록 구조 리팩토링 완료





