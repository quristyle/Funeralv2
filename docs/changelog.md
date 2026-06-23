# Changelog

## v1.0.2 (2026-06-23)
- feat: 사용자 프로필 관리 화면 전체 기능 백엔드 API 연동
  - 백엔드 `AuthServer`의 `UserEndpoints`에 프로필 수정, 암호 변경, 보안/알림 스위치 개별 변경용 API 라우트 구축
  - `UserService`에 프로필 변경(`UpdateProfileAsync`), 암호 검증 및 변경(`ChangePasswordAsync`), 설정 갱신(`UpdateSettingAsync`) 비즈니스 로직 연동
  - `UserInfoDto` 확장 및 `GetUserInfoAsync` 수정으로 자기소개, 전화, 이메일 및 스위치 상태를 데이터베이스에서 일괄 결합하여 응답하도록 보강
  - 프론트엔드 `api/core/user.ts`에 신규 API 매핑
  - `base-setting.vue`, `password-setting.vue`, `security-setting.vue`, `notification-setting.vue` 컴포넌트들을 각각 백엔드 API와 연결하여 로드, 제출, 실시간 스위치 변경 기능 구현

## v1.0.1 (2026-06-23)
- fix: 로그아웃 401 Unauthorized 및 Pinia $reset 에러 수정
  - `api/core/auth.ts`에서 `logoutApi`가 인증 토큰 헤더 없이 전송되는 `baseRequestClient` 대신 `requestClient`를 사용하도록 변경하여 실제 백엔드 로그아웃이 401 에러 없이 정상 작동하도록 개선
  - `packages/stores/src/setup.ts`의 `resetAllStores` 함수에서 setup 스토어의 `$reset` 메서드 부재 시 발생하는 크래시를 `try-catch`로 방어하고, `clearCache` 폴백 초기화 기능을 수행하도록 가드 처리

## v1.0.0 (2026-06-23)
- feat: 역할 관리(CRUD) 및 권한 설정 화면 통합
  - 기존 `views/system/role/list.vue` 파일에 있던 역할 생성(onCreate), 수정(onEdit), 삭제(onDelete) 기능을 `views/system/role/index.vue` 화면의 좌측 역할 목록 그리드로 통합
  - 좌측 역할 목록 카드 헤더에 "역할 생성" 버튼 탑재 및 그리드에 "수정", "삭제" 액션 컬럼 추가
  - 불필요해진 `views/system/role/list.vue` 파일 삭제
  - `/system/role` 라우트의 컴포넌트 연결을 `index.vue`로 변경
