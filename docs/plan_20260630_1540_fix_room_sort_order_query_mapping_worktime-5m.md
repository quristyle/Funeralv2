# 구현 계획서: 호실 목록/상세 조회 시 정렬 순서 매핑 누락 오류 수정

- **작성일시**: 2026-06-30 15:40
- **예상 소요 시간**: 5분

---

## 1. 문제 요약
호실 정렬 순서(`SortOrder`) 수정 시 데이터베이스에는 정상적으로 `3` 등으로 저장되고 있으나, 목록 재조회 혹은 단일 조회 시 DTO 응답 내 `sortOrder` 값이 계속 `0`으로 전송되는 매핑 누락 문제를 해결합니다.

---

## 2. 디자인 요약
- **원인 분석**:
  - `RoomService.cs` 의 목록 조회 메서드(`GetRoomsAsync`) 및 단일 상세 조회 메서드(`GetRoomByIdAsync`)에서 DB 엔티티 데이터를 `RoomDto` 로 변환하여 리턴하는 매핑 코드 내에 `SortOrder` 컬럼 정보가 누락되어 있었습니다. 이로 인해 정수형 기본값인 `0`이 할당되어 클라이언트로 날아가고 있었습니다.
- **수정 방향**:
  - `GetRoomsAsync` 와 `GetRoomByIdAsync` 의 `RoomDto` 인스턴스 생성 블록에 `SortOrder = r.SortOrder` 매핑을 명시적으로 추가합니다.

---

## 3. 구현 계획
- **Step 1: 조회 API DTO 맵퍼 수정**
  - `RoomService.cs` 내 `GetRoomsAsync` 및 `GetRoomByIdAsync` 메서드의 DTO 매핑 구문 내 `SortOrder` 매핑 추가.
