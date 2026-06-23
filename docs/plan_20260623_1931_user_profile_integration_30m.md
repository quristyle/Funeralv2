# Implementation Plan - 사용자 프로필 정보 및 설정 백엔드 연동

---

## 1. 개요 (Problem Summary)
- 현재 사용자의 프로필 관리 화면(`_core/profile/index.vue`) 및 하위 설정 컴포넌트(기본 설정, 보안 설정, 비번 변경, 새 메시지 알림)는 프론트엔드 UI만 노출되고 백엔드 API와의 실질적인 연동이 구현되어 있지 않거나 모킹 상태로 방치되어 있었음.
- 로그인한 사용자의 정보를 실제 데이터베이스로부터 호출하여 채우고, 폼 작성 및 토글 동작이 일어날 때 백엔드 API를 호출해 즉각 저장 및 동기화할 수 있도록 프론트엔드와 백엔드를 모두 개선함.

## 2. 설계 요약 (Design Summary)
- **목적**: 프로필 설정의 전반적인 기능을 로컬 목업에서 실제 백엔드 마이크로서비스(`AuthServer`) 연동 구조로 교체.
- **입력**: 이름, 자기소개 정보 및 비밀번호 수정 정보, 각종 활성화 토글 값.
- **출력**: API 통신 성공/실패 토스트 및 사용자 프로필 데이터 갱신.
- **예외 처리**: 비밀번호 입력 불일치 가드, 수정 실패 및 권한 부족 에러 대응.
- **주요 모듈**:
  - `AuthServer` 백엔드: `UserEndpoints.cs`에 신규 엔드포인트(프로필 수정, 암호 변경, 설정 변경) 매핑, `UserService.cs`에 비즈니스 로직 및 `UserInfoDto` 확장 속성 바인딩.
  - `funeralv2` 프론트엔드: `api/core/user.ts`에 신규 API 매핑 및 `base-setting.vue`, `password-setting.vue`, `security-setting.vue`, `notification-setting.vue` 연동.

## 3. 구현 계획 (Implementation Plan)
1. **백엔드 DTO 및 서비스 구축**:
   - `UserInfoDto`에 추가 필드(Introduction, Phone, Email 및 보안/알림 설정 속성) 정의.
   - `UpdateProfileDto`, `ChangePasswordDto`, `UpdateSettingDto` 정의.
   - `UserService.cs`에서 `GetUserInfoAsync` 호출 시 `ProfileDetails`를 `Include`하여 값들을 취합하고, `UpdateProfileAsync`, `ChangePasswordAsync`, `UpdateSettingAsync`를 구현.
2. **백엔드 엔드포인트 매핑**:
   - `UserEndpoints.cs`에 POST `/user/profile`, POST `/user/change-password`, POST `/user/settings` 신규 라우트 매핑.
3. **프론트엔드 API 및 컴포넌트 연동**:
   - `user.ts` API 파일에 `updateProfileApi`, `changePasswordApi`, `updateSettingApi` 정의.
   - 각 Vue 3 SFC 화면에서 마운트 시 `getUserInfoApi`를 찔러 바인딩하고, 제출/토글 시 연관 API를 태워 백엔드와 양방향 연동 완수.

## 4. 테스트 계획 (Testing Summary)
- **기본 프로필**: 데이터가 바르게 로드되고 이름/자기소개 수정 시 저장 후 재로드 및 갱신 여부 검증.
- **비밀번호 변경**: 이전 비밀번호 평문 비교 검증 및 정상 일치 시 암호 업데이트 성공 여부 검증.
- **보안/알림 스위치**: 각 항목 활성화 여부가 로드되고, 토글 시 `updateSettingApi`를 통해 `account_profile_details` 테이블에 실시간 반영 여부 검증.
