# 구현 계획서: 빈소 현황 화면 내 층별 호실 그룹화 UI 개선

- **작성일시**: 2026-06-30 13:27
- **예상 소요 시간**: 10분

---

## 1. 문제 요약
빈소 현황 대시보드 화면([index.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/building/status/index.vue))의 건물별 섹션 컴포넌트([building-section.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/building/status/modules/building-section.vue))에서, 단순 평탄 나열되던 호실 카드들을 층(`Floor`) 단위 섹션으로 분류하고 그 안에 종속적으로 배치되도록 UI 계층 구조를 개편합니다.

---

## 2. 디자인 요약
- **데이터 구조화**:
  - `rooms` 데이터를 `computed` 연산 속성을 이용해 `floorId` 기준으로 그룹화된 층 목록(`groupedFloors`) 배열로 가공합니다.
  - `Map` 자료구조를 사용하여 백엔드가 전달한 기존 층 정렬(순서)이 흐트러지지 않고 그대로 유지되게 정렬합니다.
- **UI/UX 개선**:
  - 건물 본문 내에 층별 하브 카드 박스를 감싸고, 각 층의 타이틀(`floorName`) 및 호실 개수를 표시합니다.
  - 해당 층 구역 안에 `RoomCard` 들이 4열 반응형 그리드 형태로 배치되게 중첩 루프 레이아웃을 구성합니다.

---

## 3. 구현 계획
- **Step 1: 컴포넌트 스크립트 수정**
  - `building-section.vue` 의 script 영역에 `computed` 및 `FloorGroup` 타입 추가 및 `groupedFloors` 구현.
- **Step 2: 컴포넌트 템플릿 수정**
  - `groupedFloors` 중첩 루프로 호실 카드 그리드 재배치.
