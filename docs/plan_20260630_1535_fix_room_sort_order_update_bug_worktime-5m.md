# 구현 계획서: 호실 정보 수정 시 정렬 순서 업데이트 반영 버그 수정

- **작성일시**: 2026-06-30 15:35
- **예상 소요 시간**: 5분

---

## 1. 문제 요약
호실 정보 관리 화면([room/index.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/building/room/index.vue))의 호실 수정 모달에서 정렬 순서(`SortOrder`) 항목의 값을 수정하고 저장해도 데이터베이스에 반영되지 않는 문제를 해결합니다.

---

## 2. 디자인 요약
- **원인 분석**:
  - 백엔드 `RoomService.cs`의 `UpdateRoomAsync` 메서드(수정 처리부)에서 DTO의 `SortOrder` 값을 Room 엔티티 객체에 매핑해 주지 않고 누락하여 저장을 수행하고 있었습니다.
- **수정 방향**:
  - `RoomService.cs` 내의 `UpdateRoomAsync` 수정 블록에서 DTO로부터 받은 `SortOrder` 값을 엔티티에 주입하여 정렬 값 변경 사항이 DB에 저장되도록 수정합니다.

---

## 3. 구현 계획
- **Step 1: 백엔드 서비스 비즈니스 로직 수정**
  - `RoomService.cs` 내 `UpdateRoomAsync` 메서드에서 `SortOrder` 매핑 추가.
