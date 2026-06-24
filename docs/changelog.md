# Changelog

## v1.0.9 (2026-06-24)
- fix: 메뉴 컴포넌트 비동기 마운트 시점 바인딩 값 유실 해결 (nextTick)
  - `component` 입력란이 드로워 오픈 시 비동기 렌더링(의존성 표출)되는 특성으로 인해 발생하는 값 증발 문제 수정
  - `onOpenChange` 내부에서 폼 값 세팅 시 `nextTick(() => { formApi.setValues(...) })`을 추가 적용하여, 입력 폼 컴포넌트의 DOM 렌더 틱이 완전히 끝난 직후 정확한 가공 경로 데이터가 바인딩되도록 2중 동기화 안전장치 구축

## v1.0.8 (2026-06-24)
- fix: 메뉴 컴포넌트 경로 로딩 시 값 미표출 버그 근본 해결 및 랩핑 레이아웃 개선
  - `AutoComplete` 자식 슬롯 복제 과정에서 발생하는 Vue 데이터 흐름 누락 문제를 해결하기 위해, 슬롯 주입을 걷어내고 `AutoComplete` 자체를 좌우 회색 애드온 박스로 감싸는 패스스루 레이아웃으로 변경
  - `AutoComplete`에 데이터 모델(`props.value`)을 직접 전달하여 로드 시 경로 데이터가 인풋 가운데 영역에 100% 정상 노출되도록 보장
  - 좌우 테두리 스타일(`border-radius: 0px`) 제어를 통해 애드온과 매끄러운 단일 컨트롤 형태로 표현되도록 디테일 개선

## v1.0.7 (2026-06-24)
- fix: 메뉴 컴포넌트 입력 값 렌더링 누락 및 반응형 바인딩 버그 수정
  - `CustomComponentInput` 내부에 로컬 반응형 상태(`innerValue` ref)와 `props.value`에 대한 감시 훅(`watch`)을 추가하여 수정 데이터 로드 시 입력 박스 내 값 미노출 현상 해결
  - 자식 `Input` 컴포넌트에 `value` 바인딩 및 `onInput` 양방향 파이프라인을 온전히 맵핑하여 타이핑과 수동 입력의 동기화 상태 확보

## v1.0.6 (2026-06-24)
- refactor: 메뉴 컴포넌트 입력란에 ant-input-group-addon 마크업 및 스타일 적용
  - `AutoComplete` 컴포넌트의 자식 슬롯에 애드온(`addonBefore: '#/views'`, `addonAfter: '.vue'`)이 구성된 `Input` 컴포넌트를 주입하는 `CustomComponentInput` 구현 적용
  - 이를 통해 Ant Design Vue 본연의 회색 애드온 박스가 정렬 형태로 인풋 전후에 표출되도록 개선

## v1.0.5 (2026-06-24)
- refactor: 메뉴 컴포넌트 경로 접사(`#/views`, `.vue`) 자동 결합 및 파싱 처리
  - 컴포넌트(`component`) 입력란 앞뒤에 데코레이터 텍스트(`addonBefore: '#/views'`, `addonAfter: '.vue'`) 배치
  - 데이터 로드 시점(`onOpenChange`): `#/views/xxxx.vue` 포맷의 데이터에서 앞뒤 접사를 제거한 순수 경로명만 입력란에 매핑
  - 데이터 저장 시점(`onSubmit`): 입력란에 작성된 경로명의 앞뒤에 `#/views` 및 `.vue`가 누락된 경우 자동으로 결합하여 서버 전송

## v1.0.4 (2026-06-24)
- refactor: 메뉴 관리 폼(form.vue) 레이아웃 및 컨트롤 개선
  - "컴포넌트"(`component`) 및 "제목"(`meta.title`) 입력란을 2열 레이아웃에서 한 줄 전체(`col-span-2`)를 사용하도록 변경
  - 제목 입력 필드와 번역 텍스트를 감싼 커스텀 인풋 컴포넌트(`CustomTitleInput`)를 구현하여 번역된 문자열이 입력창 바로 아래 라인에 완벽하게 밀착되어 노출되도록 보강
  - "정렬 순서"(`meta.order`) 항목을 하단(`status` 필드 뒤)으로 재배치하고, `InputNumber` 컨트롤이 전체 가로 폭(`w-full`)을 100% 채우도록 개선
  - "상태"(`status`) 필드의 표현을 기존 라디오 그룹에서 직관적인 스위치(`Switch`) 컴포넌트로 전환하고, 활성/비활성 텍스트 맵핑 및 1/0 상태값 바인딩 적용

## v1.0.3 (2026-06-24)
- feat: 회사-사용자 매핑 관리 기능 구현
  - 백엔드 `ICompanyService` 및 `CompanyService`에 회사 소속 사용자 조회, 회사 미지정 사용자 조회, 사용자 회사 할당 및 해제 로직 구현
  - 백엔드 `CompanyEndpoints`에 `/system/companies/{companyId}/users`, `/system/companies/eligible-users`, `/system/companies/users/remove` 엔드포인트 연동
  - 프론트엔드 `api/system/company.ts`에 사용자 매핑 API 함수 추가
  - 프론트엔드 `views/system/company-user/index.vue` 화면 추가 (좌측: 회사 목록, 우측: 회사별 사용자 조회/해제 및 추가 지정 모달)
  - `router/routes/modules/system.ts` 라우팅에 `/system/company-user` 추가

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
