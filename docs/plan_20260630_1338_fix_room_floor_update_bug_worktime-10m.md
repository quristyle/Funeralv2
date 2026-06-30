# 구현 계획서: 호실 수정 시 배정 층 업데이트 반영 버그 수정

- **작성일시**: 2026-06-30 13:38
- **예상 소요 시간**: 10분

---

## 1. 문제 요약
호실 정보 관리 화면([room/index.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/building/room/index.vue))의 호실 수정 모달에서 층을 변경하고 저장해도 실제 데이터베이스에 변경 사항이 저장되지 않는 문제를 해결합니다.

---

## 2. 디자인 요약
- **원인 분석**:
  - 프론트엔드는 `formModel`에 변경된 `floorId`를 정상적으로 담아 `updateRoom` API를 호출하고 있으나, 백엔드 `RoomService.cs`의 `UpdateRoomAsync` 메서드에서 DTO의 `FloorId`와 `BuildingId` 값을 Entity에 매핑해 주지 않고 저장(Skip)하는 버그가 존재했습니다.
- **수정 방향**:
  - `RoomService.cs` 내의 `UpdateRoomAsync` 수정 블록에서 DTO로부터 받은 `BuildingId` 및 `FloorId` 값을 Entity에 주입하여 변경 사항이 DB에 저장되도록 수정합니다.

---

## 3. 구현 계획
- **Step 1: 백엔드 서비스 비즈니스 로직 수정**
  - `RoomService.cs` 내 `UpdateRoomAsync` 메서드에서 `BuildingId` 및 `FloorId` 매핑 추가.
