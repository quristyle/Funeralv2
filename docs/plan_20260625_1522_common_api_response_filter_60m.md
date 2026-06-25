# 구현 계획서 - 공통 API 응답 Endpoint Filter 도입
- **estimated-worktime**: 60m

---

## 1) 문제 요약 (Problem Summary)
- 각 마이크로서비스의 Minimal API에서 핸들러마다 수동으로 `Results.Ok(ApiResponse<T>.Ok(data))`를 작성해야 하므로 보일러플레이트 코드가 반복되고 있음.
- 공통 인프라(`Funeralv2.Shared.Infrastructure`)에 공통 `IEndpointFilter`를 구현하여 핸들러에서 원본 비즈니스 데이터를 직접 리턴해도 자동 래핑되어 일관된 `ApiResponse<T>` JSON 스펙으로 반환되도록 흐름을 공통화함.

---

## 2) 설계 요약 (Design Summary)
### 공통 인프라 (`Funeralv2.Shared.Infrastructure`)
- **`ApiResponseFilter.cs` 생성**:
  - `IEndpointFilter`를 상속받아 구현.
  - 엔드포인트 실행 결과를 받아와 검증:
    - 결과가 이미 `ApiResponse<T>` 구조인 경우 패스스루(Pass-through).
    - 결과가 `IResult` 계열이거나 `void`/`Task`인 경우 패스스루 혹은 비어있는 성공 응답 반환.
    - 그 외의 비즈니스 데이터 객체(`T`)는 `ApiResponse<object>.Ok(result)`로 자동 래핑하여 `Results.Ok(...)` 타입으로 반환.
- **확장 메서드 (`ApiResponseFilterExtensions.cs`)**:
  - `RouteHandlerBuilder` 및 `RouteGroupBuilder`에 공통 필터를 손쉽게 추가할 수 있는 확장 메서드 `AddApiResponseWrapper()` 구현.

### 마이크로서비스 연동
- **`funeralv2Api` 엔드포인트 리팩토링**:
  - `ExampleEndpoints.cs`, `BuildingEndpoints.cs`에 필터 체인(`AddApiResponseWrapper()`)을 그룹 단위로 적용.
  - 핸들러 내부의 수동 `Results.Ok(ApiResponse<T>.Ok(result))` 호출을 걷어내고, 원본 데이터 객체(`result`)를 직접 반환하게 수정.
- **`AuthServer` 엔드포인트 리팩토링**:
  - 핵심 엔드포인트에 필터를 적용하여 공통화하고 수동 래핑 코드 일부 간소화. (전체 범위 중 일부 핵심부 위주로 변경 검증)

---

## 3) 구현 계획 (Implementation Plan)

### Task 1: 공통 필터 및 확장 메서드 구현
- [x] `microservices/Common/Funeralv2.Shared.Infrastructure` 프로젝트에 `Filters/ApiResponseFilter.cs` 작성.
- [x] `Filters/ApiResponseFilterExtensions.cs` 작성.

### Task 2: `funeralv2Api`에 필터 적용 및 엔드포인트 리팩토링
- [x] `ExampleEndpoints.cs`에 필터 추가 및 리팩토링.
- [x] `BuildingEndpoints.cs`에 필터 추가 및 리팩토링.
- [x] `funeralv2Api` 프로젝트 정상 빌드 검증.

### Task 3: `AuthServer`에 필터 적용 및 엔드포인트 리팩토링 (검증용 일부 리팩토링)
- [x] `AuthServer` 내 `CompanyEndpoints.cs` 등 핵심 엔드포인트에 필터 적용 및 수동 래핑 코드 간소화.
- [x] 전체 마이크로서비스 프로젝트 빌드 검증 (`dotnet build`).

---

## 4) 검증 및 테스트 계획 (Testing Plan)
- **빌드 테스트**:
  - 프로젝트 전체 컴파일이 오류 없이 통과하는지 검증.
- **동작 방식 검증**:
  - API 필터가 기존 `ApiResponse` 직렬화 스키마(특히 `result`, `page` 배열 변환 등)를 망가뜨리지 않고 정확하게 동작하는지 단위 시뮬레이션 및 데이터 구조 체크.
