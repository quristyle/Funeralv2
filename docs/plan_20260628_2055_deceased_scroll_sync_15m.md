# 구현 계획서 (Implementation Plan)

- **작성일시**: 2026-06-28 20:55
- **태스크명**: 고인 종합 정보 폼 모달 좌측 네비게이션과 우측 스크롤 동기화
- **예상 소요 시간**: 15분

---

## 1. 문제 요약 (Problem Summary)
`deceased-form-modal.vue`의 우측 섹션을 스크롤할 때 좌측 스티키 바로가기 버튼의 활성화 상태가 현재 보여지는 섹션으로 동기화되지 않는 문제가 발생했습니다. 원인은 모달이 마운트되어 열리기 전에 `IntersectionObserver`가 설정되어 스크롤 컨테이너를 감지하지 못하기 때문입니다.

## 2. 설계 요약 (Design Summary)
- **목적**: 우측 스크롤 위치에 맞춰 좌측 네비게이션 버튼의 활성 상태(`activeSection`)를 자동으로 업데이트
- **주요 변경 사항**:
  - 모달이 화면에 렌더링된 시점(`deceasedModalApi.open()` 호출 직후 `nextTick`)에 `IntersectionObserver`를 설정하도록 수정
  - 좌측 네비게이션 버튼 클릭으로 인한 스크롤 이동 중에 상태가 튀는 현상을 막기 위해 `isScrollingByClick` 플래그 관리
  - 메모리 누수 방지를 위해 observer 생성 전 기존 관찰 인스턴스 정리(`observer.disconnect()`)

## 3. 구현 계획 (Implementation Plan)
1. **의존성 추가**: vue 패키지에서 `nextTick`을 임포트합니다.
2. **상태 관리 변수 추가**: 클릭 스크롤 감지 예외를 위한 `isScrollingByClick` 반응형 변수를 정의합니다.
3. **IntersectionObserver 갱신**: `setupObserver` 호출 시 기존 관찰자를 초기화하고 안전하게 DOM 요소를 매핑합니다.
4. **앵커 이동 로직 조정**: `scrollToSection` 실행 시 플래그를 설정하여 스크롤 스파이 트리거를 방지합니다.
5. **모달 오픈 시점 연동**: `open` 함수 최하단에 `nextTick`을 적용하여 모달 렌더링 완료 후 관찰자를 등록합니다.
