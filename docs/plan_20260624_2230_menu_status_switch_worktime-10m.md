# 구현 계획서 (Implementation Plan)
- **작성일시**: 2026-06-24 22:30
- **작업명**: 메뉴 상태(활성/비활성) 컨트롤을 스위치(Switch)로 개선
- **예상 소요 시간**: 10분

## 1) Problem Summary (문제 요약)
- 메뉴의 활성/비활성 표현(라디오 그룹)을 직관적이고 미려한 스위치(Switch) 컨트롤로 변경해야 함.
- 백엔드 데이터베이스 상에서 `status` 필드가 정수형(1 또는 0)을 취하고 있으므로, Ant Design Vue `Switch` 컴포넌트의 `checkedValue` 및 `unCheckedValue` 프로퍼티를 활용하여 형변환 트러블 없이 1/0 상태값을 양방향 매핑함.

## Design Summary (설계 요약)
- **상태 항목 (`status`)**:
  - `component` 값을 `'RadioGroup'`에서 `'Switch'`로 변경.
  - `componentProps`에 다음 프로퍼티 지정:
    - `checkedChildren`: `$t('common.enabled')` (활성)
    - `unCheckedChildren`: `$t('common.disabled')` (비활성)
    - `checkedValue`: `1` (체크 시 저장될 값)
    - `unCheckedValue`: `0` (언체크 시 저장될 값)

---

## 2) Implementation Plan (구현 계획)
- **Step 1**: `form.vue` 파일의 `schema` 배열 내 `status` 필드 구조 수정
- **Step 2**: 프론트엔드 정적 타입 검증(`pnpm run typecheck`) 수행을 통한 최종 무결성 확인

---

## 3) Testing Plan (테스트 계획)
- **동작 분석**: 메뉴 수정/등록 드로워가 열릴 때, 스위치 형태로 활성/비활성이 표출되며 온/오프 상태가 1/0 값으로 정상 제출되는지 확인.
