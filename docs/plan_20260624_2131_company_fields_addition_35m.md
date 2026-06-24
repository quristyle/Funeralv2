# 구현 계획서 - 회사 관리 항목 필드 추가 (짧은명칭, 주소, 승인일)

- **작성일시**: 2026-06-24 21:31
- **예상 작업 시간**: 35분 (worktime-35m)

---

## 1) Problem Summary
- 회사 관리 화면([list.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/system/company/list.vue))에서 회사의 짧은명칭, 주소, 승인일을 관리하는 기능이 없음.
- 주소를 우편번호, 기본주소, 상세주소로 세분화하여 추가 저장 및 관리하기 위해 데이터베이스 엔티티, DTO, 프론트엔드 UI 스키마를 모두 확장할 필요가 있음.

---

## 2) Design Summary
- **데이터베이스 및 백엔드**:
  - `Company` 엔티티([Company.cs](file:///C:/Funeralv2/microservices/AuthServer/Entities/Company.cs))에 `ShortName`, `ZipCode`, `Address`, `AddressDetail`, `ApprovalDate` 필드 매핑 및 추가.
  - `CompanyDto` 및 `CompanyCreateDto`([CompanyDto.cs](file:///C:/Funeralv2/microservices/AuthServer/DTOs/CompanyDto.cs))에 동일 필드 매핑 추가.
  - `CompanyService`에서는 `Mapster`를 통해 자동 매핑 처리가 수행되므로 엔티티와 DTO 속성만 정의하면 자동 갱신됨.
- **프론트엔드**:
  - `SystemCompanyApi` 인터페이스([company.ts](file:///C:/Funeralv2/fronts/apps/funeralv2/src/api/system/company.ts))에 추가된 5가지 필드 적용.
  - **회사 테이블 컬럼**([data.ts](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/system/company/data.ts)): '짧은명칭', '주소' (우편번호 + 주소 + 상세주소 포맷팅 출력), '승인일' (formatDate) 컬럼 추가.
  - **입력 폼 스키마**: '짧은명칭'(Input), '우편번호'(Input), '주소'(Input), '상세주소'(Input), '승인일'(DatePicker) 구성.
  - In-cell 에디터 업데이트([list.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/system/company/list.vue)): 인라인 수정 시 추가된 필드들도 비구조화 할당하여 `updateCompany` 호출에 전송하도록 수정.

---

## 3) Implementation Plan

### Task 1: 백엔드 모델 및 DTO 정의 추가
- **대상 파일**:
  - `microservices/AuthServer/Entities/Company.cs`
  - `microservices/AuthServer/DTOs/CompanyDto.cs`
- **구현 내용**:
  - `Company.cs` 에 짧은명칭(`short_name`), 우편번호(`zip_code`), 주소(`address`), 상세주소(`address_detail`), 승인일(`approval_date`)에 맵핑되는 EF Core 프로퍼티를 추가.
  - `CompanyDto.cs` 내 `CompanyDto` 및 `CompanyCreateDto` 에 해당 프로퍼티들을 타입 정의에 반영.

### Task 2: 프론트엔드 API 타입 추가
- **대상 파일**:
  - `fronts/apps/funeralv2/src/api/system/company.ts`
- **구현 내용**:
  - `SystemCompany` 인터페이스 및 `CreateParams`에 신규 속성 5종 추가.

### Task 3: 테이블 및 폼 스키마 구성 수정
- **대상 파일**:
  - `fronts/apps/funeralv2/src/views/system/company/data.ts`
  - `fronts/apps/funeralv2/src/views/system/company/list.vue`
- **구현 내용**:
  - `data.ts` 의 `useColumns`에 '짧은명칭', '주소', '승인일' 컬럼 추가. (주소 출력 시 우편번호 및 주소 정보 포맷팅 렌더러 적용).
  - `data.ts` 의 `formSchema`에 주소 입력 필드군(우편번호, 주소, 상세주소), 짧은명칭, 승인일(DatePicker) 추가.
  - `list.vue` 의 `onEditClosed` 함수 내에서 변경 데이터를 전송하기 위한 비구조화 파라미터 구조 업데이트.

---

## 4) Testing Plan
- **단위 테스트 및 통합 확인**:
  - 회사 생성/수정 모달에 짧은명칭, 주소 3종, 승인일이 정상적으로 출력되고 입력 가능한지 확인.
  - 저장 완료 후 데이터가 DB에 정상 저장되고, 회사 목록 테이블에 주소가 `[우편번호] 주소 상세주소` 형태로 포맷팅되어 나타나는지 확인.
  - 인라인 테이블 편집 기능을 이용하여 셀 수정 시 수정본 데이터가 깨짐 없이 업데이트되는지 검증.
