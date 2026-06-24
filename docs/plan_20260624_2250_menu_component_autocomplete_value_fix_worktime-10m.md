# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:50
- **작업명**: 메뉴 컴포넌트 자동완성 값 렌더링 누락 근본 해결
- **예상 소요 시간**: 10분

## 1) Problem Summary (문제 요약)
- `CustomComponentInput` 내부의 자식 슬롯 `Input` 주입 시, Ant Design Vue의 `AutoComplete` 컴포넌트가 슬롯 복제 과정에서 부모로부터 주입되는 `value` 바인딩을 소실시켜 폼 로드 시 값이 비어 보이는 현상 발생.
- `AutoComplete`를 단독 컴포넌트로 렌더링하되, 이를 감싸는 외부 컨테이너 마크업을 `ant-input-group` 클래스로 구성하여 네이티브 애드온 디자인을 완벽히 재현하고 슬롯 복제 버그를 원천 차단함.

## 2) Design Summary (설계 요약)
- **슬롯 주입 구조 제거**:
  - `AutoComplete` 내부의 `default` 슬롯(`h(Input, ...)`) 구문을 제거함.
- **외부 애드온 랩핑 구조 적용**:
  - `AutoComplete` 컴포넌트 양측에 `span.ant-input-group-addon`을 배치하여 전후 애드온을 입힘.
  - `AutoComplete` 좌우 모서리의 border-radius를 0으로 고정하여 회색 애드온 박스와 시각적으로 한 몸처럼 보이도록 매끄럽게 연결.
  - `value: props.value`를 `AutoComplete`에 직접 넘겨 초기 데이터가 인풋 가운데 영역에 안전하게 그려지도록 처리함.

---

## 3) Implementation Plan (구현 계획)
- **Step 1**: `form.vue` 의 `CustomComponentInput` 컴포넌트 정의를 단순한 외경 랩핑 마크업 형태로 수정
- **Step 2**: 프론트엔드 정적 타입 검증(`pnpm run typecheck`) 수행

---

## 4) Testing Plan (테스트 계획)
- **동작 분석**: 수정 후 팝업을 열 때 보관 중인 컴포넌트 경로(/system/company-user/index 등)가 텍스트 박스 한가운데 정상 노출되며, 좌우 애드온 박스가 깨짐 없이 잘 매칭되는지 확인.
