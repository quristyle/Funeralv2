# 구현 계획서: 호실 관리 목록 그리드 내 층 컬럼 추가

- **작성일시**: 2026-06-30 15:33
- **예상 소요 시간**: 5분

---

## 1. 문제 요약
호실 정보 관리 화면([room/index.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/building/room/index.vue))의 목록 그리드 테이블에 호실이 속한 `층` 정보를 표시하는 컬럼이 누락되어 있어 이를 보완합니다.

---

## 2. 디자인 요약
- **그리드 컬럼 추가**:
  - `Grid` 컴포넌트의 컬럼 정의 맨 처음에 `{ field: 'floorName', title: '층', minWidth: 100 }` 컬럼을 신설하여, 각 호실이 속한 층 정보를 직관적으로 출력할 수 있게 배치합니다.

---

## 3. 구현 계획
- **Step 1: 그리드 컬럼 수정**
  - `room/index.vue` 의 `Grid` columns 정의 부분에 `floorName` 컬럼 정보 삽입.
