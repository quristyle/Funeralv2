# 구현 계획서 - 층(Floor) 및 호실(Room) 백엔드 개발 및 DB 마이그레이션
- **estimated-worktime**: 90m

---

## 1) 문제 요약 (Problem Summary)
- 프론트엔드의 층 관리 및 호실 관리 화면이 작동할 수 있도록 `funeralv2Api` 백엔드에 층(Floor)과 호실(Room) 엔티티, 서비스, API 엔드포인트를 구현함.
- `dotnet ef`를 통해 PostgreSQL DB 마이그레이션을 추가하여 `smfr.floors` 및 `smfr.rooms` 테이블을 생성하고, 프론트엔드 API 경로가 게이트웨이의 `/api/funeral` 프록시 라우팅을 타도록 수정함.

---

## 2) 설계 요약 (Design Summary)
### 백엔드 (funeralv2Api)
- **엔티티 설계**:
  - **`Floor.cs`**:
    - `BaseEntity<string>` 상속.
    - 필드: `BuildingId` (string, Required), `Name` (string, Required), `Code` (string, Required), `SortOrder` (int, Required), `Remark` (string?, Nullable).
    - 관계: `Building` 외래키 및 네비게이션 프로퍼티 설정.
  - **`Room.cs`**:
    - `BaseEntity<string>` 상속.
    - 필드: `BuildingId` (string, Required), `FloorId` (string, Required), `Name` (string, Required), `Code` (string, Required), `RoomType` (string, Required), `Status` (string, Required, ACTIVE/INACTIVE), `Remark` (string?, Nullable).
    - 관계: `Floor` 외래키 및 네비게이션 프로퍼티 설정.
- **DbContext (`AppDbContext.cs`)**:
  - `DbSet<Floor> Floors`, `DbSet<Room> Rooms` 추가.
- **DTOs (`FloorDtos.cs`, `RoomDtos.cs`)**:
  - 조회 응답 DTO 및 생성/수정용 DTO 구성. (조회 응답에는 `BuildingName`, `FloorName` 네비게이션 조회 결과 포함)
- **서비스 (`FloorService.cs`, `RoomService.cs`)**:
  - 층: 건물 ID별 목록 조회, 생성, 수정, 삭제 비즈니스 로직 및 로깅.
  - 호실: 층 ID별 목록 조회, 생성, 수정, 삭제 비즈니스 로직 및 로깅.
- **엔드포인트 (`FloorEndpoints.cs`, `RoomEndpoints.cs`)**:
  - Minimal API 라우팅 등록 및 공통 API 응답 필터(`AddApiResponseWrapper()`) 적용.

### 프론트엔드 (funeralv2)
- **API 클라이언트 (`src/api/building/index.ts`)**:
  - 층 및 호실 관련 모든 API의 엔드포인트 접두사를 `/funeral` 로 갱신하여 게이트웨이 매핑 보장.

---

## 3) 구현 계획 (Implementation Plan)

### Task 1: 백엔드 엔티티 및 DB 컨텍스트 추가
- [x] `Entities/Floor.cs` 생성.
- [x] `Entities/Room.cs` 생성.
- [x] `Data/AppDbContext.cs` 에 `DbSet<Floor>`, `DbSet<Room>` 추가.

### Task 2: 백엔드 DTO 및 서비스 구현
- [x] `DTOs/FloorDtos.cs` 및 `DTOs/RoomDtos.cs` 작성.
- [x] `Services/IFloorService.cs`, `Services/FloorService.cs` 작성.
- [x] `Services/IRoomService.cs`, `Services/RoomService.cs` 작성.
- [x] `Program.cs` 에 `FloorService` 및 `RoomService` DI 등록.

### Task 3: API 엔드포인트 구현 및 등록
- [x] `Endpoints/FloorEndpoints.cs` 생성.
- [x] `Endpoints/RoomEndpoints.cs` 생성.
- [x] `Program.cs` 에 `app.MapFloorEndpoints()` 및 `app.MapRoomEndpoints()` 등록.

### Task 4: DB 마이그레이션 생성 및 적용
- [x] `dotnet ef migrations add AddFloorAndRoom` 명령 실행.
- [x] `dotnet ef database update` 명령 실행.

### Task 5: 프론트엔드 API 경로 수정
- [x] `src/api/building/index.ts` 파일의 층/호실 API 경로에 `/funeral` 추가 및 빌드 검증.

---

## 4) 검증 및 테스트 계획 (Testing Plan)
- **백엔드 빌드 검증**:
  - 컴파일 오류 검사.
- **API 기능 동작 확인**:
  - Swagger를 통한 층 목록 조회, 생성, 수정, 삭제 검증.
  - Swagger를 통한 호실 목록 조회, 생성, 수정, 삭제 검증.
