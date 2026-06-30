# 구현 계획서: 장비 목록 그리드 내 소속 위치 컬럼 추가 및 백엔드 연동

- **작성일시**: 2026-06-30 13:13
- **예상 소요 시간**: 15분

---

## 1. 문제 요약
장비 목록 그리드 화면에서 각 장비가 소속된 실제 물리 위치 경로(건물/층/호실 조합)를 '짧은 명칭' 기준의 한눈에 알아보기 쉬운 단일 문자열로 노출해달라는 요구사항을 반영합니다.

---

## 2. 디자인 요약
- **위치 문자열 조합 규칙**:
  1. 건물까지만 소속: `건물 짧은명칭` (예: `본관`)
  2. 층까지 소속: `건물 짧은명칭` + `층 명칭` (예: `본관 2층`)
  3. 호실까지 소속: `건물 짧은명칭` + `호실 짧은명칭` (예: `본관 201호`)
- **백엔드 매핑**:
  - `DeviceDto` 클래스에 `BuildingShortName`, `FloorShortName`, `RoomShortName` 필드를 추가합니다.
  - `DeviceService.cs` 목록 조회 및 상세 조회 쿼리에 `Include` 문을 이용해 건물, 층, 호실 데이터를 함께 Load(Join)하여 DTO에 매핑합니다.
- **프론트엔드 반영**:
  - `BuildingApi.Device` 인터페이스에 짧은 명칭 프로퍼티를 추가합니다.
  - `use-device-grid.ts` 그리드 컬럼에 `locationPath`를 추가하고 `formatter` 함수에서 규칙대로 경로를 조합하여 출력합니다.

---

## 3. 구현 계획
- **Step 1: 백엔드 DTO 및 서비스 수정**
  - `DeviceDtos.cs` 및 `DeviceService.cs` 에서 위치 테이블 JOIN 조회 및 DTO 매핑 적용.
- **Step 2: 프론트엔드 API 및 그리드 컬럼 추가**
  - `building/index.ts` 타입 정의에 위치 짧은명칭 추가.
  - `use-device-grid.ts` 컬럼 정의에 `locationPath` 및 `formatter` 구현 적용.
