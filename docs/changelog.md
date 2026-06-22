# Changelog

## [Unreleased] - 2026-06-22
### Fixed
- 메뉴명/경로 존재 검사 시 백엔드 응답 `result` 가 단일 원소 배열(`[false]`/`[true]`) 구조로 감싸져 반환되어 자바스크립트 Truthy 조건(배열 자체는 항상 참)에 의해 오진되던 문제를 배열 인덱스 `[0]`의 값을 직접 엄격 검사하도록 수정 완료
- 메뉴 관리 이름/경로 중복 체크 시, API 응답 객체(ApiResponse)의 래핑된 `result` 데이터(Boolean)를 꺼내지 않아 느슨한(Truthy) 객체 조건 평가로 인해 언제나 "존재하는 명칭" 에러가 발생하던 버그 수정 (`isMenuNameExists`/`isMenuPathExists`에서 `.result`를 언래핑하여 실제 Boolean을 리턴하도록 수정)
- `scom.biz_select_configs` 테이블에서 `dept` 설정의 `result_path`가 `null`이더라도, 백엔드 응답이 `ApiResponse` 공통 형태일 때 자동으로 `result`에서 목록을 추출하도록 Fallback 로직을 `BizSelect.vue`에 반영하여 상위 부서 목록이 출력되지 않던 문제 해결 완료
- `BizSelect.vue` 에서 `onMounted`와 `watch`가 동시 트리거되어 API가 중복(2회) 호출되던 현상을 `watch`에 `immediate: true`를 적용하고 파라미터 값 변화(JSON 직렬화 비교)가 없을 시 조기 리턴하도록 변경하여 1회만 호출되도록 최적화 완료
- Pinia `useBizSelectStore`에서 API 응답 객체(ApiResponse)의 래핑된 `result` 데이터(배열)를 안전하게 꺼내어 `configs` 상태값으로 저장하도록 바인딩 오류 수정 (`TypeError: configs.value.find is not a function` 해결)
- `BizSelect.vue` 에서 `requestClient.request` 호출 시 Axios 인스턴스 파라미터 직렬화 오류(`paramsSerializer` 관련 TypeError)를 해결하기 위해 `requestClient.get`/`post` 로 명시적 분기 호출하도록 우회 수정 완료

### Added
- **DB 메타데이터 기반 dynamic BizSelect 구조 전면 적용**
  - DB `scom.biz_select_configs` 테이블 및 초기 데이터 주입 마이그레이션 적용
  - 백엔드 `BizSelectConfig` 엔티티 및 `IBizSelectConfigService`, `BizSelectConfigService` CRUD 구현
  - 백엔드 `/system/biz-select/configs` 및 CRUD Endpoints 매핑 완료 (`SystemEndpoints.cs`)
  - fronts `biz-select-config` Pinia 스토어 구현하여 최초 구동(`bootstrap.ts` 프리로드) 및 호출 시 메모리 전역 캐싱하여 네트워크 트래픽 최소화 기능 탑재
  - `BizSelect.vue` 하드코딩된 API 분기를 제거하고, 캐싱된 메타데이터를 사용하여 HTTP GET/POST 동적 호출 및 트리 평탄화(FLATTEN), 라벨/밸류 매핑을 동적으로 처리하도록 리팩토링
  - 메타데이터 관리용 어드민 화면(`views/system/biz-select-config/` - `list.vue`, `data.ts`, `modules/form.vue`) 개발 및 `/system/biz-select-config` 라우터 연동
- 부서관리 화면 상단에 회사 선택 combobox 필터 추가
- 부서관리 테이블 그리드에 '회사명' 컬럼 추가 (`companyName`)
- 부서 등록 및 수정 폼 스키마(`useSchema`)에 '소속 회사' 선택 필드로 `BizSelect` 컴포넌트 교체 적용 완료, `nextTick`을 활용한 모달 데이터 바인딩 타이밍 안정화 (`form.vue`) 및 Vue 3 반응형 성능 경고 차단을 위한 `markRaw` 처리 완료 (`data.ts`)
- 부서 등록 및 수정 폼 스키마(`useSchema`) 내의 '상위 부서' 선택 필드를 `BizSelect` 컴포넌트로 교체 적용, '소속 회사' 변경에 반응하여 동적으로 해당 회사의 부서 목록을 리로드하도록 의존성(`dependencies.componentProps`) 연동 및 `markRaw` 처리 완료 (`data.ts`)
- 백엔드 `DepartmentDto` 및 `CreateDepartmentDto`에 `CompanyId`, `CompanyName` 프로퍼티 추가
- AI 프롬프트 지침서(`3.AI.md`)에 백엔드 API 응답 구조 및 `requestClient` 내부의 `data` 언래핑 처리 주의사항 추가
- 공통 DictSelect 및 BizSelect 컴포넌트 추가
  - `DictSelect.vue`: 공통코드(Dict) 전용 Select 콤보박스
  - `BizSelect.vue`: 비즈니스 데이터(Biz - 회사, 부서 목록 등) 전용 Select 콤보박스 및 `autoSelectFirst` 지원
  - `BizSelect.vue` 에 '전체' 항목을 맨 앞에 표시하는 `showAll` Prop 옵션 기능 추가
  - `DictSelect.vue` 와 `BizSelect.vue` 에 Vben Form 커스텀 컴포넌트 데이터 연동을 위한 `modelValue` 및 `update:modelValue` 양방향 바인딩 호환성 지원 보완
- 부서관리 화면(`list.vue`)에 새롭게 설계된 `BizSelect` 컴포넌트 시범 적용 및 기존에 존재하던 불필요한 회사 조회 로직 청소 (회사선택 콤보박스에 `show-all` 전체 옵션 활성화 완료)

### Changed
- 백엔드 `/auth/system/dept/list` API가 `companyId` 쿼리 매개변수를 수신하여 해당 회사의 부서만 필터링하도록 기능 개선 (`DepartmentService.GetDeptListAsync` 및 `SystemEndpoints`)
- 프론트엔드 `getDeptList` API 헬퍼 함수가 `companyId` 매개변수를 전송할 수 있도록 수정
- 부서관리 화면(`list.vue`)에서 생성 및 하위 부서 추가 팝업 호출 시 현재 선택된 회사 ID가 기본값으로 셋팅되도록 로직 개선

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





