# 구현 계획서 - 건물 관리 폼 개선 및 AI 추천 코드, 주소 검색 연동
- **estimated-worktime**: 60m

---

## 1) 문제 요약 (Problem Summary)
- 건물 관리 모달 팝업 내에서 회사를 직접 변경/선택할 수 있어야 하며, 건물명 입력 시 AI가 코드를 자동 제안해 주는 사용성 개선이 필요함.
- 기존 단일 문자열 주소 체계를 `우편번호 (zipCode)`, `기본 주소 (address)`, `상세 주소 (addressDetail)`로 세분화하여 관리하도록 확장함.

---

## 2) 설계 요약 (Design Summary)
### 백엔드 (funeralv2Api)
- **엔티티 (`Building.cs`)**:
  - `ZipCode` (Nullable, string), `AddressDetail` (Nullable, string) 컬럼 추가.
- **DTOs (`BuildingDtos.cs`)**:
  - `BuildingDto`, `BuildingCreateDto`, `BuildingUpdateDto`에 `ZipCode`, `AddressDetail` 속성 연동.
- **서비스 (`BuildingService.cs`)**:
  - 생성 및 수정 맵핑 로직에 `ZipCode` 및 `AddressDetail` 반영.
- **DB 마이그레이션**:
  - `dotnet ef migrations add AddBuildingAddressDetails` 생성 후 데이터베이스 반영.

### 프론트엔드 (funeralv2)
- **API 클라이언트 (`src/api/building/index.ts`)**:
  - `BuildingApi.Building` 인터페이스에 `zipCode`, `addressDetail` 프로퍼티 추가.
- **뷰 (`src/views/building/info/index.vue`)**:
  - **소속 회사**: 모달 폼 첫 번째 아이템에 `BizSelect` (type="company") 추가.
  - **AI 코드 추천**: 건물명 작성 시 그 아래에 `AiCodeSuggester`를 배치하여, 코드 자동 완성 연동.
  - **주소 세분화**:
    - `AddressSearchInput` 컴포넌트 임포트.
    - 우편번호 찾기 완료 시 우편번호(`zipCode`)와 기본주소(`address`)를 폼 모델에 맵핑.
    - 상세주소는 수동 입력을 위한 `Input.TextArea` 또는 일반 `Input`으로 분리 바인딩.

---

## 3) 구현 계획 (Implementation Plan)

### Task 1: 백엔드 엔티티 및 DTO, 서비스 수정
- [x] `Entities/Building.cs` 에 `ZipCode`, `AddressDetail` 추가.
- [x] `DTOs/BuildingDtos.cs` 에 `ZipCode`, `AddressDetail` 속성 추가.
- [x] `Services/BuildingService.cs` 에서 맵핑 처리부 수정 및 빌드 검증.

### Task 2: DB 마이그레이션 생성 및 적용
- [x] `dotnet ef migrations add AddBuildingAddressDetails` 명령 실행.
- [x] `dotnet ef database update` 명령 실행으로 PostgreSQL 테이블 컬럼 추가 완료.

### Task 3: 프론트엔드 API 및 뷰 화면 개선
- [x] `src/api/building/index.ts` 내 `Building` 인터페이스 수정.
- [x] `src/views/building/info/index.vue` 스크립트 수정 (`formModel` 초기값 및 바인딩 핸들러 추가).
- [x] `src/views/building/info/index.vue` 템플릿 수정 (`BizSelect`, `AiCodeSuggester`, `AddressSearchInput` 배치).

---

## 4) 검증 및 테스트 계획 (Testing Plan)
- **백엔드 빌드 검증**:
  - C# 빌드가 정상 통과되는지 확인.
- **화면 연동 테스트**:
  - 팝업 열기 시 회사 선택 셀렉트박스가 활성화되고 기존 소속 회사가 올바르게 선택되어 있는지 확인.
  - 건물명 입력 즉시 하단에 AI 추천 코드 박스가 나타나고, 선택 시 건물코드란에 자동 바인딩되는지 테스트.
  - 우편번호 찾기 버튼 클릭 시 Daum 우편번호 검색 팝업이 노출되며, 완료 시 우편번호 및 주소 필드에 올바른 값이 자동 적용되는지 확인.
  - 저장 후 그리드 및 DB에 세분화된 주소(우편번호, 주소, 상세주소)가 정상 저장되는지 데이터 정합성 검사.
