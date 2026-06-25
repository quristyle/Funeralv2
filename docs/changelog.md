# Changelog

## v1.3.0 (2026-06-25)
- feat: 호실 관리에서 불필요한 호실코드(`code`) 필드 제거
  - 백엔드 `Room` 엔티티, `RoomDto`/`RoomCreateDto`/`RoomUpdateDto` DTO에서 `Code` 속성 제거
  - `RoomService` CRUD 매핑 및 로그 구문에서 `Code` 참조 완전 삭제
  - EF Core 마이그레이션 `RemoveCodeFromRoom` 생성 및 DB 반영(`ALTER TABLE smfr.rooms DROP COLUMN code`)
  - 프론트엔드 `BuildingApi.Room` 인터페이스에서 `code` 속성 제거
  - `room/index.vue` formModel 초기값, 그리드 컬럼, 모달 Form.Item에서 호실코드 항목 완전 제거

## v1.2.9 (2026-06-25)
- feat: 층 관리 그리드 작업컬럼 아이콘 변경 및 호실 관리 상단필터 BizSelect 계층 연동
  - `floor/index.vue` 그리드 작업컬럼 수정/삭제 버튼을 아이콘(`lucide:edit`, `lucide:trash-2`) + Tooltip 방식으로 개편 (건물관리와 동일한 UX 통일)
  - `room/index.vue` 상단 필터를 회사 → 건물 → 층 계층형 BizSelect로 전면 개편
    - 기존 `Select` + `getFloors()` 직접 호출 방식 → `BizSelect` 공통 컴포넌트로 전환
    - 회사 변경 시 건물 ID 초기화, 건물 변경 시 층 ID 초기화 watch 바인딩 적용
    - 모달 폼 내 배정 층 선택도 BizSelect(type="floor", buildingId 파라미터 종속)로 전환
    - 수정/삭제 작업 버튼 아이콘화 처리 (Tooltip 포함)
  - `BizSelect.vue` 파라미터 가드 확장: `floor` type에 `buildingId` 미선택 시 API 요청 차단 로직 추가
  - AuthServer EF Core 마이그레이션(`AddFloorBizSelectConfig`): `biz_select_configs` 테이블에 `building` 및 `floor` 타입 메타정보 시드 데이터 INSERT 완료 (PostgreSQL 실 DB 반영)

## v1.2.7 (2026-06-25)
- feat: 층(Floor) 및 호실(Room) 백엔드 CRUD 기능 구현 및 DB 마이그레이션
  - `funeralv2Api` 프로젝트 내에 `Floor` 및 `Room` 엔티티 정의, DbSet 추가 (`AppDbContext`)
  - `dotnet ef` CLI 도구를 통해 `AddFloorAndRoom` 마이그레이션 생성 및 로컬 DB 테이블(`smfr.floors`, `smfr.rooms`) 생성 완료
  - 층 및 호실 관리를 위한 서비스(`IFloorService`/`FloorService`, `IRoomService`/`RoomService`) 작성 및 DI 컨테이너에 등록
  - `/building/floor`, `/building/room` Minimal API 엔드포인트 구현 및 공통 API 응답 필터 적용
  - 프론트엔드 API 클라이언트(`src/api/building/index.ts`) 내 층 및 호실 관련 URL 경로에 `/funeral` 접두사 추가하여 게이트웨이 프록시 라우팅 연동

## v1.2.6 (2026-06-25)
- feat: 건물 관리 모달 팝업의 회사 선택, AI 코드 추천 및 Daum 우편번호 주소 연동
  - 백엔드 `Building` 엔티티 및 DTO(`BuildingDto`, `BuildingCreateDto`, `BuildingUpdateDto`)에 우편번호(`ZipCode`), 상세주소(`AddressDetail`) 속성 연동
  - `BuildingService` 내부의 엔티티-DTO 간 양방향 매핑 처리부 갱신
  - `dotnet ef`를 통해 `AddBuildingAddressDetails` 신규 마이그레이션 생성 및 PostgreSQL 적용 완료
  - 프론트엔드 `Building` API 스키마 인터페이스에 `zipCode` 및 `addressDetail` 추가
  - 건물 관리 화면([index.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/building/info/index.vue))의 팝업 폼 내부 개선:
    - 소속 회사 변경이 가능하도록 상단에 `BizSelect` (type="company") 추가
    - 건물명 작성 시 즉각적으로 AI 추천 코드를 제공받을 수 있도록 `AiCodeSuggester` 연동
    - 주소 체계를 우편번호, 주소, 상세주소로 분리하고, Daum Postcode 연계 컴포넌트인 `AddressSearchInput` 연동 및 바인딩 완료

## v1.2.5 (2026-06-25)
- feat: API Gateway 미정의 경로 호출 및 타임아웃 에러 응답 규격 포맷 통일
  - `ApiGateway/Program.cs` 내에 `MapFallback`을 추가하여 게이트웨이에 매핑되지 않은 경로 호출 시 `ApiResponse` 규격과 일관된 포맷(`E404` 코드 및 "요청하신 경로를 찾을 수 없습니다." 메시지)의 JSON 에러 응답을 직접 반환하도록 구현
  - 502 Bad Gateway 및 504 Gateway Timeout 오류 발생 시 반환하는 응답 구조 역시 `ApiResponse` 규격에 맞는 필드 구성(`success`, `code`, `message`, `timestamp`, `traceId`, `path`, `realmessage`)으로 통일되도록 에러 포맷 개선

## v1.2.4 (2026-06-25)
- fix: 건물 정보 API 호출 경로에 `/funeral` 접두사 추가 연동
  - API Gateway(`funeral-service-route`)의 프록시 라우팅 규칙(`/api/funeral/` -> `funeralv2Api`)에 맞추어, 프론트엔드의 건물(Building) API 호출 경로를 `/funeral/building/info`로 수정하여 백엔드로 정상 라우팅되도록 수정

## v1.2.3 (2026-06-25)
- feat: 모든 MSA API 성공 응답 규격 포맷팅의 공통화를 위한 Endpoint Filter 및 확장 기능 구현
  - 공통 인프라(`Funeralv2.Shared.Infrastructure`)에 `ApiResponseFilter` 엔드포인트 필터 구현
    - 핸들러 실행 결과를 인터셉트하여 `null`인 경우, `ApiResponse<object>.Ok(null)`로 자동 변환
    - 반환값이 일반 비즈니스 데이터 객체인 경우 `ApiResponse<object>.Ok(data)`로 래핑하여 `Results.Ok(...)` 형식으로 자동 변환 처리
    - 이미 `ApiResponse<T>` 규격이거나 `ApiResponse<T>`가 들어간 `IResult` 등은 이중 래핑 방지를 위한 예외 패스스루 처리
  - 필터 등록 간소화를 위한 `RouteHandlerBuilder` 및 `RouteGroupBuilder`용 `AddApiResponseWrapper()` 확장 메서드 구현
  - `funeralv2Api` 및 `AuthServer` 의 엔드포인트(`ExampleEndpoints.cs`, `BuildingEndpoints.cs`, `CompanyEndpoints.cs`)에 공통 필터를 탑재하고, 개별 핸들러 내부의 수동 래핑 코드를 걷어내는 리팩토링 진행

## v1.2.2 (2026-06-25)
- feat: 건물 관리의 회사(Company) 소속 연동 개발
  - 백엔드(funeralv2Api)에 `Building` 엔티티 정의 (`BaseEntity<string>` 상속) 및 DB DbSet 등록 (`AppDbContext`)
  - `dotnet ef` CLI 도구를 통해 `AddBuilding` 마이그레이션 생성 및 PostgreSQL 로컬 DB 업데이트 적용 (`appsettings.Local.json` 기준)
  - 건물 생성, 수정, 삭제 및 회사 필터링 조회를 지원하는 `IBuildingService`, `BuildingService` 작성 및 DI 등록
  - `/building/info` Minimal API 엔드포인트 구현 및 로깅(`ILogger<BuildingService>`)을 통한 임계 경로의 Observability 확보
  - 프론트엔드 API 클라이언트(`src/api/building/index.ts`)에 `companyId` 쿼리 파라미터 연동 및 DTO 스키마 갱신
  - 건물 정보 관리 뷰(`src/views/building/info/index.vue`) 상단에 `BizSelect` (type="company") 연동하여 선택된 회사 기준 건물 데이터 그리드 표출 및 신규 생성 시 해당 회사 ID 귀속 처리 구현

## v1.2.1 (2026-06-25)
- feat: 이미지 크기별 규격화 보관 및 WebP 지연 생성(Lazy Generation) 기능 구현
  - 이미지 규격화: `Thumbnail` (150x150), `Medium` (600x600), `Large` (1200x1200) 규격으로 별도 보관하도록 개선
  - 지연 생성 (Lazy Generation): 조회 요청 시점에 파일 유무를 검사하여 없을 때만 원본을 리사이징하여 각 규격 폴더에 `WebP` 포맷으로 인코딩 및 캐싱
  - 공간 절약: 생성본은 고효율 `WebP` 포맷으로 자동 변환해 보관하고, 최초 연산 이후에는 스토리지에서 직접 조회하므로 CPU 연산 차단 및 최적의 입출력 성능 확보
  - 파일 삭제 시 정합성 유지: 원본 이미지 및 각 규격별로 지연 생성된 모든 캐시 파일(`[id].webp`, `[id]_*`)을 일괄 물리 삭제 처리
  - API 엔드포인트 `/medium/{id}` 및 `/large/{id}` 추가 연동 및 게이트웨이 / 예외 리다이렉션 연계 탑재
  - `FileServer.http` 명세 내 규격별 테스트 추가

## v1.2.0 (2026-06-25)
- feat: 파일 관리를 위한 파일 마이크로서비스 추가
  - 신규 마이크로서비스 프로젝트 `microservices/FileServer` 추가
  - 파일 업로드(`POST /upload`), 원본 다운로드(`GET /download/{id}`), 썸네일 다운로드(`GET /thumbnail/{id}`), 이미지 크기별 다운로드(`GET /resize/{id}`), 파일 메타조회(`GET /metadata/{id}`), 파일 삭제(`DELETE /{id}`) API 기능 구현
  - `SixLabors.ImageSharp` 2.1.9 라이브러리를 활용한 크로스플랫폼 이미지 썸네일 생성 및 가로/세로 리사이징, 캐싱 기능 구현
  - `DbContext.Database.EnsureCreated()`를 활용해 구동 시 `smfr.file_metadata` 테이블 자동 생성 기능 구현
  - API Gateway(`ApiGateway/appsettings.json`)에 `/api/file` 라우트 및 `file-cluster` 목적지(`http://localhost:5350`) 설정 추가
  - `dev.bat`, `backend_run_ubuntu.sh`, `backend_run_mac.sh` 구동 및 빌드 스크립트에 `FileServer` 프로젝트 등록
  - API 개발 사양 및 수동 테스트를 위한 `FileServer.http` 스펙 정의 파일 추가

## v1.1.0 (2026-06-24)
- feat: 조직도 화면 진입 시 첫 번째 회사 자동 선택, 레이아웃 센터링 개선 및 인터랙션 강화
  - `org-chart.vue` 화면 진입 시 `getCompanyList` API를 직접 호출하여 첫 번째 회사를 자동으로 감지하고 `selectedCompanyId`에 바인딩
  - 이를 통해 화면 로드 즉시 첫 번째 회사의 조직도 데이터가 바로 조회되어 렌더링되도록 개선
  - 세로조직도(위에서 아래)일 때 최상위 Root(회사 노드)가 화면의 가로 가운데에 정확히 오도록 `panX` 자동 정렬 로직 추가
  - 가로조직도(왼쪽에서 오른쪽)일 때 최상위 Root가 화면의 세로 가운데에 오도록 `panY` 정렬 로직 추가
  - 상단 툴바 카드에 로딩 인디케이터가 포함된 수동 "새로고침" 버튼을 배치하여 언제든 조직 정보 최신화 가능하도록 보완
  - 세로조직도 상태에서 부모 노드의 보정 이전의 좌표가 연결선에 잘못 매핑되어 회사/부서 박스의 왼쪽 끝에서 선이 출발하던 좌표 동기화 버그 해결 (부모 노드 최종 보정좌표 매핑)
  - 최상위 회사 노드(`COMPANY`) 위로 부서 노드 드롭 시 회사의 직속 부서(최상위 부서)가 될 수 있도록 `onCompanyDrop` 이동 처리 지원
  - 캔버스 배경 드롭 이벤트(`onRootDrop`) 및 드래그 가이드를 전면 제거하여 노드 드롭 시 오직 부서(`DEPT`) 또는 최상위 회사(`COMPANY`) 노드 위로만 드롭되도록 제한하고, 사용자는 부서로만 이동 가능하도록 경고 제약 장착

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
