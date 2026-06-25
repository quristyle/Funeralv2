# 프론트엔드 공통 파일 업로드 컴포넌트 추가 및 아바타 연동 설계 및 구현 계획

이 문서는 프론트엔드 monorepo에 공통 파일 업로드 컴포넌트(`FileUpload`)를 구현하고, 사용자 정보 화면(`profile.vue` 및 `index.vue`)에 이를 연동하여 아바타 이미지를 실시간으로 변경할 수 있도록 적용하기 위한 개발 계획서입니다.

---

## 1. 개요 및 요구사항
- **목적**:
  - 단일 이미지, 다중 이미지, 일반 파일 등 유연한 업로드를 지원하는 재사용성 높은 `FileUpload` 컴포넌트 개발.
  - 사용자 프로필 관리 화면의 아바타 이미지를 직접 클릭하여 업로드 완료 시 DB에 URL이 반영되는 흐름 적용.
- **주요 기능**:
  - **FileUpload 컴포넌트**:
    - 업로드 모드: `avatar`(아바타 서클 스타일), `image`(사진 목록), `file`(드래그 앤 드롭 영역 및 일반 리스트).
    - 로컬 스토리지 피니아 상태(`core-access`)를 직접 파싱하여 API Gateway 호출 시 Bearer JWT 헤더 수동 매핑.
    - XHR 전송 진척도(`progress`)를 마이크로 애니메이션과 수치로 표출하여 WOW 요소 충족.
  - **아바타 실시간 변경**:
    - `profile.vue` 에 `VbenAvatar` 대신 `FileUpload` (avatar 모드) 탑재.
    - 파일 업로드 성공 후 발생한 다운로드 URL을 상단 부모(`_core/profile/index.vue`)로 `change-avatar` 이벤트를 날림.
    - 부모는 `updateProfileApi`를 호출하여 사용자 프로필 DB를 업데이트하고 `userStore` 정보를 갱신하여 화면을 즉시 동기화.

---

## 2. 패키지 및 컴포넌트 설계
- **패키지 위치**: `@vben/common-ui` 패키지의 `components` 영역에 배치하여 monorepo 내의 모든 실행 앱이 호출 가능하도록 바인딩.
  - 컴포넌트: [file-upload.vue](file:///C:/Funeralv2/fronts/packages/effects/common-ui/src/components/file-upload/file-upload.vue)
  - 엔트리 추가: [components/index.ts](file:///C:/Funeralv2/fronts/packages/effects/common-ui/src/components/index.ts)
- **API 및 데이터 흐름**:
  - 업로드 요청: `POST /api/file/upload` (Form-Data)
  - 프로필 저장 요청: `POST /auth/user/profile` (body: `{ avatar: string }`)

---

## 3. 구현 세부 태스크 (Implementation Plan)

### [태스크 1] 백엔드 DTO 및 서비스 개선 (완료)
- `UpdateProfileDto` 에 `Avatar` 수신 필드 추가 및 `UserService`의 `GetUserInfoAsync`, `UpdateProfileAsync` 프로필 상세 테이블(`AccountProfileDetails`) 연계 로직 추가.

### [태스크 2] 프론트엔드 공통 `FileUpload` 컴포넌트 개발 (완료)
- `Drag-and-Drop` 감지 상태 및 업로드 진행률 애니메이션 추가.
- 아바타 업로드 완료 시 `/api/file/download/{id}` 주소를 바인딩하도록 유도.

### [태스크 3] 프로필 레이아웃(`profile.vue`) 및 진입 화면(`index.vue`) 갱신 (완료)
- `profile.vue`에서 아바타 변경 시 이벤트를 내보내도록 수정.
- `index.vue` 에서 `updateProfileApi` 호출 및 `userStore` 사용자 데이터 동기화 추가.

### [태스크 4] 모노레포 타입체크 검사 (진행 중)
- `pnpm check:type` 명령어로 빌드 및 타입 검사 진행.

---

## 4. 예외 및 실패 처리
- **인증 토큰 누락**: 로컬 스토리지에 토큰이 없거나 만료된 상태에서 업로드 시, Gateway 401 오류를 캐치하여 업로드 상태를 에러로 전환 및 사용자에게 알림.
- **업로드 서버 오프라인**: 파일 업로드 서비스가 내려가 있는 상황 등 네트워크 오류 시 적색 에러 아이콘 및 에러 문구 표시.
