# 구현 계획서: 현황 대시보드 내 장비별 멀티미디어 즉시 변경 드롭다운 연동

- **작성일시**: 2026-06-30 14:27
- **예상 소요 시간**: 20분

---

## 1. 문제 요약
빈소 현황 대시보드([status/index.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/building/status/index.vue))의 모든 카드(건물 공용, 층 공용, 호실 카드)에 소속된 장비에서 재생 중인 동영상/음원을 한눈에 파악하고, 별도의 상세 창을 열지 않고 드롭다운 메뉴 선택을 통해 그 자리에서 즉시 수정(Enabled = true 포함)할 수 있도록 UI/UX를 개선합니다.

---

## 2. 디자인 요약
- **백엔드 DTO 및 조회 쿼리 고도화**:
  - `DeviceDto`에 멀티미디어 컬럼(`VideoId`, `MusicId`, `IsVideoEnabled`, `IsMusicEnabled`, `VideoName`, `MusicName`)을 확장하고 `DeviceService.cs`에서 `DeviceAttributes` 및 `MediaSources`와 Eager Join하여 한 번에 채워 보냅니다.
- **프론트엔드 API 타입 업데이트**:
  - `BuildingApi.Device` 인터페이스에 멀티미디어 메타데이터 속성 추가.
- **멀티미디어 전체 옵션 로딩 및 캐싱**:
  - `useStatusData` 에서 비디오/음악 `BizSelectConfig` 메타데이터 설정을 읽어 공용 목록(`videos`, `musics`)을 최초 1회만 조회·캐싱합니다.
- **장비별 드롭다운 UI 구현**:
  - 건물 공용, 층 공용, 호실 카드 내의 각 장비 렌더링 블록 아래에 `🎬 영상: {이름}`, `🎵 음원: {이름}` 영역을 제공합니다.
  - Ant Design Vue의 `<Dropdown>` 과 `<Menu>` 컴포넌트를 입혀 클릭 시 즉시 갱신 API(`upsertDeviceAttribute`)를 연동하고 상태를 리로딩합니다.

---

## 3. 구현 계획
- **Step 1: 백엔드 수정 (DTO 확장 및 서비스 JOIN 구현) - 완료**
- **Step 2: 프론트엔드 API 및 컴포저블 수정 - 완료**
- **Step 3: `building-section.vue` 컴포넌트 수정**
  - `Dropdown`, `Menu` 임포트 추가, `emit` 이벤트 선언.
  - 건물 공용/층 공용 장비 레이아웃을 드롭다운 연동형으로 변경.
- **Step 4: `room-card.vue` 컴포넌트 수정**
  - `Dropdown`, `Menu` 임포트 추가, `emit` 이벤트 선언, `videos`/`musics` props 수용.
  - 호실 내 장비 리스트 레이아웃을 세로형 드롭다운 패널 구조로 개편.
