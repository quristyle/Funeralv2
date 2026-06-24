# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:00
- **작업명**: 회사-사용자 매핑 관리 기능 구현
- **예상 소요 시간**: 60분

## 1) Problem Summary (문제 요약)
- 회사에 사용자를 연결(매핑)하는 신규 기능 및 관리 화면을 개발해야 함.
- 비즈니스 규칙에 따라 사용자는 단 하나의 회사에만 등록될 수 있고, 회사는 여러 사용자를 소유할 수 있음 (1:N 관계).
- `accounts.company_id` 필드를 업데이트하는 API 및 프론트엔드 매핑 인터페이스를 구현함.

## 2) Design Summary (설계 요약)
### 목적
회사별로 소속된 사용자 목록을 관리하고, 소속이 없는(CompanyId가 null인) 사용자를 회사의 소속으로 다중 선택하여 신규 할당 및 해제할 수 있는 기능을 제공한다.

### API 명세
- `GET /system/companies/{companyId}/users`
  - **설명**: 특정 회사에 소속된 사용자 목록 조회
  - **반환**: `ApiResponse<List<AccountDto>>`
- `GET /system/companies/eligible-users`
  - **설명**: 소속 회사가 없는 사용자(CompanyId == null) 목록 조회 (추가 모달용)
  - **반환**: `ApiResponse<List<AccountDto>>`
- `POST /system/companies/{companyId}/users`
  - **설명**: 지정된 사용자 목록의 소속 회사를 변경 (할당)
  - **요청 바디**: `List<string> userIds`
  - **반환**: `ApiResponse<bool>`
- `POST /system/companies/users/remove`
  - **설명**: 지정된 사용자 목록의 소속 회사를 해제 (CompanyId = null)
  - **요청 바디**: `List<string> userIds`
  - **반환**: `ApiResponse<bool>`

### 프론트엔드 UI 설계 (views/system/company-user/index.vue)
- **좌측**: 회사 목록 그리드 (VxeTable 사용)
- **우측**: 
  - 회사를 선택하지 않은 상태: "좌측 목록에서 회사를 선택해 주세요." 안내 메시지 노출
  - 회사를 선택한 상태: 선택된 회사의 소속 사용자 목록 테이블, 상단에 '사용자 추가 지정' 버튼, 각 행별 '해제' 버튼 제공
- **모달**: '사용자 추가 지정' 클릭 시, 소속이 없는 사용자 목록을 Table(체크박스 다중 선택 지원)로 표시하고, 선택 완료 시 일괄 등록 API 호출

---

## 3) Assumption-Based Interim Solution (가정 기반 임시 솔루션)
- **Assumption (가정)**: 사용자는 한 회사에만 등록될 수 있으므로, 이미 다른 회사에 소속되어 있는 사용자를 다른 회사로 강제 이동시키는 것은 권장되지 않으며, 추가 모달에서는 **소속 회사가 없는 사용자(CompanyId == null)**만 노출되어 선택할 수 있도록 구현한다.
- **Risk (위험)**: 기존에 오등록된 사용자를 다른 회사로 재배정하려 할 때, 바로 다른 회사로 이동시키지 못하고 먼저 기존 회사에서 해제한 후 다시 추가해야 하는 번거로움이 있을 수 있음.
- **Fallback (대체 설계)**: 만약 요구사항상 강제 이전이 빈번해야 한다면, 추가 모달에서 전체 사용자 목록을 보여주고 선택 시 "해당 사용자는 이미 다른 회사에 소속되어 있습니다. 이동하시겠습니까?" 경고 후 이관 처리하도록 API 및 화면을 보강할 수 있음. (우선 안전하고 일반적인 1차 가정을 기본으로 적용함)

---

## 4) Implementation Plan (구현 계획)
- **Step 1**: 백엔드 `ICompanyService` 및 `CompanyService`에 사용자 매핑 관련 메서드 구현
- **Step 2**: 백엔드 `CompanyEndpoints.cs`에 신규 REST API 엔드포인트 연동
- **Step 3**: 백엔드 빌드 테스트 (`dotnet build`)
- **Step 4**: 프론트엔드 API 클라이언트 `fronts/apps/funeralv2/src/api/system/company.ts`에 신규 API 연동 함수 추가
- **Step 5**: 프론트엔드 뷰 컴포넌트 `fronts/apps/funeralv2/src/views/system/company-user/index.vue` 작성 및 관련 모듈 분리
- **Step 6**: 프론트엔드 라우터에 신규 화면 추가 및 사이드바 메뉴 정상 로드 여부 검증
- **Step 7**: 프론트엔드 빌드 테스트 (`pnpm --filter funeralv2 run build`)

---

## 5) Testing Plan (테스트 계획)
- **단위/통합 테스트**:
  - 회사 소속 사용자 조회 API가 소속된 계정만 정확히 필터링하는지 검증.
  - 소속이 없는 사용자 조회 API가 `CompanyId == null`인 계정들만 올바르게 리턴하는지 검증.
  - 사용자 지정 API 호출 후 DB 상에서 대상 `Account`들의 `company_id`가 지정한 회사 ID로 업데이트되었는지 검증.
  - 사용자 해제 API 호출 후 DB 상에서 대상 `Account`들의 `company_id`가 null이 되는지 검증.
