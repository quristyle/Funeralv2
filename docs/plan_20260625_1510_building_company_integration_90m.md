# 구현 계획서 - 건물 관리 및 회사 연동 개발
- **estimated-worktime**: 90m

---

## 1) 문제 요약 (Problem Summary)
- 건물(Building) 정보가 특정 회사(Company)에 소속되도록 기능을 확장해야 함.
- 회사는 `AuthServer`에서, 건물은 `funeralv2Api`에서 관리하므로, `funeralv2Api` DB에 `Building` 엔티티를 추가하고 `CompanyId` 필드를 포함하도록 설계함.
- 프론트엔드 건물 관리 화면 상단에 `BizSelect`를 연동하여 회사를 선택하게 하고, 그리드에는 해당 회사 소속 건물 목록만 필터링하여 노출 및 생성/수정하도록 구현함.

---

## 2) 설계 요약 (Design Summary)
### 백엔드 (funeralv2Api)
- **엔티티 (`Building.cs`)**:
  - `BaseEntity<string>` 상속 (UUID ID 및 공통 필드 상속).
  - 필드: `CompanyId` (Required, string), `Name` (Required, string), `Code` (Required, string), `Address` (Nullable, string), `Remark` (Nullable, string).
- **DbContext (`AppDbContext.cs`)**:
  - `DbSet<Building> Buildings { get; set; }` 추가.
- **DTOs (`BuildingDtos.cs`)**:
  - `BuildingDto`, `BuildingCreateDto`, `BuildingUpdateDto` 작성.
- **서비스 (`IBuildingService.cs`, `BuildingService.cs`)**:
  - 회사 ID 기준 목록 조회, 상세 조회, 생성, 수정, 삭제 비즈니스 로직 작성.
- **엔드포인트 (`BuildingEndpoints.cs`)**:
  - Minimal API 형태로 라우팅 등록 (`/building/info` 접두사 사용).
  - 프론트엔드의 기존 API 경로 (`/building/info/list`, `/building/info`, `/building/info/{id}`)와 규격을 맞춤.
- **DB 마이그레이션**:
  - `dotnet ef migrations add AddBuilding` 및 `dotnet ef database update` 실행.

### 프론트엔드 (funeralv2)
- **API 클라이언트 (`src/api/building/index.ts`)**:
  - `getBuildings`에 `companyId` 파라미터를 추가하여 전달하도록 수정.
  - `createBuilding`, `updateBuilding` 등의 요청 DTO 규격 수정 (`companyId` 필드 추가).
- **뷰 (`src/views/building/info/index.vue`)**:
  - 상단 툴바에 `BizSelect` (type="company") 연동.
  - 선택된 `companyId`가 변경될 경우 그리드를 다시 로드함.
  - 신규 등록 모달을 열었을 때 자동으로 상단에서 선택된 `companyId`가 입력되도록 설정.

---

## 3) 구현 계획 (Implementation Plan)

### Task 1: 백엔드 모델 및 DB 구성
- [x] `funeralv2Api` 프로젝트에 `Entities/Building.cs` 작성.
- [x] `Data/AppDbContext.cs` 에 `DbSet<Building>` 추가.
- [x] `appsettings.Local.json` 의 연결 정보를 기반으로 DB 마이그레이션 생성 및 업데이트 실행.
  - 명령어: `dotnet ef migrations add AddBuilding --project microservices/funeralv2Api`
  - 명령어: `dotnet ef database update --project microservices/funeralv2Api`

### Task 2: 백엔드 DTO 및 서비스 구현
- [x] `DTOs/BuildingDtos.cs` 추가 (Create, Update, Response DTO).
- [x] `Services/IBuildingService.cs` 및 `Services/BuildingService.cs` 작성 및 `Program.cs`에 DI 등록.

### Task 3: 백엔드 엔드포인트 구현 및 등록
- [x] `Endpoints/BuildingEndpoints.cs` 생성 및 Minimal API 매핑 정의.
- [x] `Program.cs`에 `app.MapBuildingEndpoints()` 호출 등록.

### Task 4: 프론트엔드 API 클라이언트 수정
- [x] `src/api/building/index.ts` 파일의 `Building` 인터페이스 및 API 함수 수정 (`companyId` 매개변수 지원).

### Task 5: 프론트엔드 건물 관리 화면 개선
- [x] `src/views/building/info/index.vue` 수정.
  - 상단에 `BizSelect` 추가하여 `companyId` 바인딩.
  - 회사 선택 변경에 따른 그리드 갱신 로직 추가.
  - 신규 건물 등록 시 상단 선택 회사 ID 전달.
  - 그리드 렌더링에 필요한 검증 및 Null Safety 준수.

---

## 4) 검증 및 테스트 계획 (Testing Plan)
- **백엔드 기능 검증**:
  - Swagger를 통해 `GET /building/info/list?companyId={id}` 필터링 기능 테스트.
  - 건물 생성, 수정, 삭제 API 정상 작동 확인.
- **프론트엔드 연동 검증**:
  - 화면 상단에서 회사 선택 시 해당 회사의 건물 목록만 그리드에 올바르게 노출되는지 확인.
  - 회사를 선택하지 않은 상태에서는 신규 등록이 불가능하거나 안내 메시지를 표시하는지 검증.
  - 신규 등록 및 수정 시 데이터가 정상적으로 저장되고 그리드가 갱신되는지 확인.
