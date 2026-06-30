# 구현 계획서: 영정사진용 투명 png 근조리본 및 장식 이미지 등록 관리 화면 생성

- **작성일시**: 2026-06-30 20:40
- **예상 소요 시간**: 10분

---

## 1. 문제 요약
영정사진 표현 및 DID 디바이스에 사용할 투명 PNG 근조리본 및 기타 장식 이미지 리소스를 등록하고 수정/삭제할 수 있는 관리 기능 및 화면이 부재하여, 프론트엔드 어플리케이션 내에 전용 관리 메뉴 화면을 신설합니다.

---

## 2. 디자인 요약
- **화면 및 컴포넌트 추가**:
  - `src/views/building/decoration/index.vue`
    - 이미지 리소스 목록을 표출하는 데이터 그리드(VXE-Table) 화면.
    - 투명 PNG 이미지가 눈에 띄도록 백그라운드에 체크무늬 격자 패턴 스타일(SVG 패턴)을 주입한 미리보기 슬롯을 구성합니다.
  - `src/views/building/decoration/modules/decoration-upload-modal.vue`
    - 이미지 리소스를 추가 및 수정할 수 있는 모달 팝업.
    - `accept="image/png"` 속성을 활용해 투명도가 포함된 PNG 파일 업로드를 제한/권장합니다.
- **API 연동**:
  - 기존 백엔드 `MediaSource` 서비스의 `IMAGE` 타입을 재활용하여 리소스를 조회(`getMediaSources('IMAGE')`)하고 저장/삭제하도록 호환 구조를 맞춥니다.
- **동적 라우팅 대응**:
  - Vben Admin의 동적 메뉴 스캐닝에 부합하게 `views/building/decoration/index.vue` 컴포넌트 구조로 배치하여, 관리자 페이지 메뉴 설정에서 `/building/decoration/index` 키로 메뉴를 연동할 수 있도록 합니다.

---

## 3. 구현 계획
- **Step 1: 장식 관리 메인 목록 뷰 생성**
  - `decoration/index.vue` 파일 작성.
- **Step 2: 장식 업로드 모달 뷰 생성**
  - `decoration/modules/decoration-upload-modal.vue` 파일 작성.
