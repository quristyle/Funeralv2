# 작업 계획서: SVG 기반 마인드맵 조직도 화면 및 백엔드 이동 API 구현

- **작성일**: 2026년 6월 24일 23시 25분
- **예상 소요 시간**: 40분

## 1. 문제 요약
- 회사의 부서 조직과 소속 사용자 구성을 마인드맵 형태로 시각화하고, 노드(부서, 사용자)를 마우스 드래그 앤 드롭으로 이동시킬 수 있는 신규 조직도 화면 개발이 필요함.
- 화면 내에서 마우스 휠 확대/축소(Zoom) 및 드래그 화면 이동(Pan)을 매끄럽게 지원하여 프리미엄한 비주얼 UX를 제공함.
- 부서 이동 및 사용자의 부서 간 이동을 처리하는 백엔드 API를 추가 연동함.

## 2. 해결 설계 및 구현 계획
1. **백엔드 고도화 (`AuthServer`)**:
   - `IDepartmentService` 및 `DepartmentService`에 부서 위치 이동(`MoveDeptAsync`) 및 사용자 부서 이동(`MoveUserDeptAsync`) 인터페이스 구현.
   - `SystemEndpoints.cs` 내에 `POST /system/dept/{id}/move` 및 `POST /system/dept/user/move` API 엔드포인트 연동.
2. **프론트엔드 API 클라이언트 변경**:
   - `src/api/system/dept.ts`에 `moveDept` 및 `moveUserDept` 함수 추가.
3. **프론트엔드 SVG 캔버스 조직도 화면 개발 (`views/system/company-user/org-chart.vue`)**:
   - SVG 뷰박스(`viewBox`)와 변환 매트릭스(또는 `transform="translate(x, y) scale(s)"`)를 활용한 Zoom & Pan 캔버스 구축.
   - 계층 트리 구조 데이터(회사 ➔ 부서 ➔ 사용자)를 가로 또는 세로 마인드맵 레이아웃(노드 위치 계산 알고리즘)으로 구조화하여 렌더링.
   - HTML5 Drag and Drop API 또는 mousemove 이벤트를 활용하여 사용자가 부서 노드 또는 사용자 노드를 드래그하여 다른 부서 노드 위에 드롭 시 즉시 부서/사용자 이동 API를 날리고 화면을 리프레시하도록 처리.
4. **라우터 연동**:
   - `src/router/routes/modules/system.ts` 에 조직도 메뉴 추가.
