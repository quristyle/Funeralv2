# Funeralv2 프론트엔드 분석

> 분석 대상: `fronts/apps/jsini-portal` (Vue 3 + Vben Admin 기반 장례식장 관리/표출 시스템)
> 분석일: 2026-08-14
> 범위: `fronts/` 디렉터리, 특히 `apps/jsini-portal`의 비즈니스 코드. 업스트림 Vben 프레임워크(`packages/*`, `internal/*`)는 참고만 함.

---

## 1. 개요

### 프레임워크 / 기술 스택
- **베이스 프레임워크**: Vben Admin monorepo v5.7.0 (pnpm workspace + turbo). 앱은 `@vben/*`, `@vben-core/*` 워크스페이스 패키지를 소비하는 형태.
- **런타임 스택**: Vue 3 (`<script setup lang="ts">`), Vue Router 4, Pinia, Ant Design Vue, vxe-table, `@tanstack/vue-query`, dayjs, cropperjs, fabric(캔버스 편집), `vue-daum-postcode`(주소검색).
- **실시간**: `@microsoft/signalr` ^10.0.0.
- **응답 파싱**: `json-bigint` — 백엔드가 BigInt ID(Snowflake 추정)를 문자열로 내려주는 것을 안전 파싱.
- **빌드**: Vite (`@vben/vite-config` 공용 프리셋), 프로덕션은 gzip 압축 + PWA + hash 라우터 + dist.zip 아카이브.

### 앱 위치 / 진입점
- 앱 루트: `fronts/apps/jsini-portal`
- 진입: `src/main.ts` → `src/bootstrap.ts` (createApp, Pinia/i18n/router/vue-query/motion/tippy 초기화)
- 패키지명: `@vben/jsini-portal` (`package.json`)
- 경로 별칭: `#/*` → `./src/*`

### 백엔드 연동 개요
- 프론트는 상대경로 `/api`(`VITE_GLOB_API_URL=/api`)로만 호출하고, 실제 라우팅은 .NET **ApiGateway**가 담당.
- 개발 시 Vite proxy가 `/api` → `http://127.0.0.1:5265`로 전달하며 `ws:true`로 SignalR 웹소켓도 프록시 (`vite.config.mts`).
- 인증: AuthServer JWT. accessToken은 프론트 스토어에, refreshToken은 httpOnly 쿠키(`withCredentials:true`) 방식으로 추정.

---

## 2. 앱 구조 및 주요 기능 페이지

### 라우팅 방식 (중요)
- **정적 라우트는 거의 없음**. `src/router/routes/index.ts`에서 `dynamicRoutes`, `staticRoutes`가 모두 빈 배열로 하드코딩되어 있고 `modules/*.ts`(dashboard/demos/examples/system/vben)는 glob 등록이 주석 처리됨 → **업스트림 데모 라우트는 실제로 로드되지 않음**.
- 실제 메뉴/라우트는 **백엔드 메뉴 API 기반 동적 생성**. `src/router/access.ts`의 `generateAccess()`가 `getAllMenusApi()`로 메뉴 트리를 받고, `import.meta.glob('../views/**/*.vue')`로 만든 `pageMap`과 매칭해 라우트를 조립. 백엔드 `component` 문자열이 실제 파일과 매칭.
- 가드: `src/router/guard.ts` — accessToken 유무 검사 → 미인증 시 로그인 리다이렉트, 최초 진입 시 동적 메뉴 생성.

### 실제 비즈니스 기능 페이지 (`src/views`)
업스트림 잔재(`_core`, `dashboard`, `demos`, `examples`)를 제외한 funeralv2 고유 도메인:

- **building/** (핵심 도메인 — 건물/빈소/장비/미디어 관리)
  - `info` 건물, `floor` 층, `room` 빈소/객실, `device` 장비(DID/키오스크/현판)
  - `deceased` 고인 관리, `decoration` 장식, `background` 배경, `audio` 음원, `video` 영상
  - `status` **빈소 실시간 현황 대시보드 (SignalR 연동, 앱의 하이라이트 화면)**
- **status/** 표출용 화면 — `funeral-status`, `funeral-info`, `deceased-status`, `simple`, `mobile`
- **system/** 관리 — `company`/`company-user`, `dept`, `account`, `role`, `menu`, `common-code`(공통코드), `i18n`(다국어 관리), `biz-select-config`(메타 기반 셀렉트 설정)
- **auth/** 권한 매핑 — `role-menu`, `role-user`, `user-role`, `menu-role`
- **info/** — `notice` 공지, `deceased-search` 고인검색, `room-history` 객실이력, `my-info`, `preview`
- **help/** — `faq`, `qna`, `inquiry`, `archive`
- **stat/** — `billing` 정산통계, `room-usage` 객실 사용률
- **setting/** — `environment` 환경설정
- **ai/chat** — AI 어시스턴트 채팅(SSE 스트리밍)

> ⚠️ **`-custom` / 스텁 이중 구조**: 다수 메뉴가 `xxx/index.vue`(19줄짜리 "임시 화면입니다" 플레이스홀더)와 `xxx-custom/index.vue`(실제 구현, 100~150줄)로 쌍을 이룸. 스텁 플레이스홀더가 **15개**, `-custom` 실제 구현 디렉터리가 **17개** 존재. 자세한 내용은 개선점 참조.

### API 레이어 구조 (`src/api`)
- `request.ts` — 공용 요청 클라이언트 팩토리. `core/`(auth/user/menu/timezone), 도메인별 `building/`, `system/`, `status/`, `stat/`, `info/`, `help/`, `setting/`, `ai/` 모듈.
- 각 API 모듈이 `namespace XxxApi { interface ... }`로 타입을 함께 정의 (예: `BuildingApi.Building/Floor/Room/Device/...`) — 도메인 타입 정의는 비교적 충실.

### 상태 스토어 (`src/store`)
- `auth.ts` — 로그인/로그아웃/유저정보 (Pinia setup store). 대부분 인증 상태는 업스트림 `@vben/stores`의 `useAccessStore`/`useUserStore`에 위임.
- `biz-select-config.ts` — DB 메타데이터 기반 셀렉트 설정을 부트스트랩 시 프리로드/캐싱.

---

## 3. 백엔드 연동 상세

### 3.1 API 요청 레이어 (`src/api/request.ts`)
- `RequestClient`(업스트림 `@vben/request`, axios 래퍼) 인스턴스 3종:
  - `requestClient` — 단건 (`dataField:'data'`)
  - `requestListClient` — 목록 (`dataField:'data.result'`)
  - `baseRequestClient` — 인터셉터 없는 원시 클라이언트(토큰 갱신용)
- **요청 인터셉터**: `Authorization: Bearer <accessToken>`, `Accept-Language` 헤더 주입 (`request.ts:78-86`).
- **응답 규약**: `code === 'S000'`을 성공으로 보고 `data` 필드 추출. 비-S000은 콘솔 경고 + `errorMessageResponseInterceptor`로 `message.error` 표출.
- **BigInt 처리**: `transformResponse`에서 `JSONBigInt({storeAsString:true})`로 파싱 (`request.ts:32-46`) — 좋은 처리.

### 3.2 인증 / 토큰
- 로그인: `api/core/auth.ts` `loginApi` → `POST /auth/login` (`withCredentials:true`). 응답에서 `response?.result?.[0]?.accessToken` 추출 (`auth.ts:33`).
- 토큰 저장: accessToken은 `useAccessStore`(Pinia, `@vben/stores`)에 저장되고 네임스페이스 접두사로 localStorage에 암호화 유지. refreshToken은 서버 httpOnly 쿠키.
- 갱신: `authenticateResponseInterceptor`(`request.ts:118-126`)가 401 시 `doRefreshToken()`(`POST /auth/refresh`) 호출, 실패 시 `doReAuthenticate()`로 로그아웃/로그인 만료 모달.
- 로그아웃: `store/auth.ts`의 `logout()` — `isLoggingOut` 플래그로 무한 루프 방지 처리됨(양호).

### 3.3 SignalR 실시간 (`src/views/building/status/index.vue`)
- **앱 전체에서 SignalR 사용처는 이 파일 단 1곳.**
- Hub URL: `'/api/funeral/hubs/device'` (하드코딩, `index.vue:37`).
- `withAutomaticReconnect`로 재시도 지연(0/2s/5s/10s) 커스텀 (`index.vue:39-46`).
- `DeviceStatusChanged` 이벤트 수신 → `updateDeviceStatusState()`로 로컬 상태 갱신.
- `onUnmounted`에서 `stop()` + null 처리 (정리 양호).

---

## 4. 강점

1. **동적 라우팅/권한 시스템 위임**: 메뉴·권한·라우트를 백엔드 API로 완전히 위임하고, 존재하지 않는 컴포넌트/중복 메뉴명을 `sanitizeMenus`로 방어(`router/access.ts:38-63`). 라우트 중복 에러를 사전 차단하는 점은 견고.
2. **BigInt 안전 처리**: Snowflake형 ID를 `json-bigint`로 문자열 파싱하여 JS 정밀도 손실 방지(`request.ts:32-46`).
3. **도메인 타입 정의 충실**: `BuildingApi` 등 네임스페이스에 엔티티 인터페이스를 상세히 선언(`api/building/index.ts`). 도메인 모델 가독성 우수.
4. **인증 인프라 재사용**: 토큰 갱신/재인증/에러 메시지 인터셉터를 업스트림 표준 패턴대로 구성. 로그아웃 재진입 방지 플래그 등 엣지케이스 처리.
5. **메타데이터 기반 셀렉트 설정**: `biz-select-config` 스토어가 부트스트랩 시 셀렉트 옵션 소스를 DB에서 프리로드/캐싱 → 하드코딩 없이 유연.
6. **SignalR 정리 처리**: 재연결 백오프 커스텀 + 언마운트 시 연결 해제.

---

## 5. 개선점 (funeralv2 비즈니스 코드 중심)

### 🔴 우선순위 높음

#### 5.1 SignalR 인증 토큰 미전달 + 재연결/생명주기 핸들러 부재
- **문제**: `initSignalR()`이 `HubConnectionBuilder().withUrl(hubUrl)`만 호출하고 `accessTokenFactory`를 넘기지 않음. JWT가 필요한 Hub라면 인증 실패, 혹은 쿠키에만 의존하게 됨. 또한 `onreconnecting`/`onreconnected`/`onclose` 콜백이 없어 재연결 중 UI 상태 표시나 재구독/재조회가 불가.
- **근거**: `src/views/building/status/index.vue:37-58`
- **개선**: `withUrl(hubUrl, { accessTokenFactory: () => useAccessStore().accessToken })` 적용. `onreconnected` 시 `onSearch()`로 상태 재동기화, `onclose` 시 사용자 알림. Hub URL은 env(`VITE_GLOB_API_URL`) 기반으로 조립.

#### 5.2 AI 스트리밍이 요청 클라이언트를 우회하고 인증 헤더 없음
- **문제**: `streamChatMessage()`가 원시 `fetch('/api/ai/chat/stream')`를 쓰며 `Authorization` 헤더를 넣지 않음. 요청 인터셉터(토큰/언어/에러처리)를 전부 우회. 백엔드가 JWT를 요구하면 401.
- **근거**: `src/api/ai/chat.ts:30-40`
- **개선**: `headers`에 `Authorization: Bearer ${accessStore.accessToken}` 주입. base URL을 env로 통일. 에러 응답 본문 파싱/사용자 메시지 처리 추가.

#### 5.3 응답 언래핑 규약 불일치 — `(res as any)?.result ?? res` 방어 코드 22곳
- **문제**: `request.ts`는 `dataField`로 `data`(또는 `data.result`)를 자동 추출하도록 설정돼 있는데, 실제 호출부에서는 `const raw = (res as any)?.result ?? res;` 패턴이 **22곳** 반복됨. 즉 클라이언트가 `result`까지 벗겨주는지에 대해 코드가 확신하지 못하고 매번 양쪽을 방어. 응답 계약이 코드 전반에서 통일돼 있지 않다는 신호.
- **근거**: `src/views/building/status/index.vue:81-95`, `composables/use-status-data.ts:60,74,83`, `store/biz-select-config.ts:29` 외 다수(`grep "as any)?.result ?? res"` → 22건)
- **개선**: 목록/단건 응답 규약을 한 곳(요청 클라이언트/얇은 API 함수)에서 확정하고 그 지점에서만 언래핑. 뷰/컴포저블은 이미 벗겨진 타입 데이터만 받도록 하여 방어 코드 제거.

#### 5.4 프로덕션 코드에 디버그 로그/개발 잔재 다수
- **문제**:
  - 라우터 가드에 `console.log('routerrouterrouter...', router)` 및 **모든 네비게이션마다** 매칭 컴포넌트 경로/`to.matched` 전체를 콘솔 출력하는 로직이 상시 활성(`guard.ts:20`, `guard.ts:39-95`). 성능·정보노출·콘솔 오염.
  - 비즈니스 코드 전반 `console.log/warn/error` **72곳** (예: `views/building/status/index.vue`의 `getDeviceAttribute` 관련 5+개 로그).
  - `handleUpdateDeviceMedia`가 `attr: any`, `savePayload` 등 `any` 남발.
- **근거**: `src/router/guard.ts:20,39-95`; `src/views/building/status/index.vue:66-95`
- **개선**: 디버그 로깅 제거 또는 `import.meta.env.DEV` 가드. 로거 유틸 도입. lint 규칙으로 `no-console` 경고.

#### 5.5 프로덕션 Vue Devtools 활성화
- **문제**: `vite.config.mts`에서 `__VUE_PROD_DEVTOOLS__: true`로 프로덕션 빌드에도 devtools가 열림. 내부 상태 노출 + 번들 증가.
- **근거**: `fronts/apps/jsini-portal/vite.config.mts` (`define.__VUE_PROD_DEVTOOLS__: true`), `.env.development VITE_DEVTOOLS=true`
- **개선**: 개발 모드에서만 켜지도록 `import.meta.env.DEV` 조건화.

### 🟡 우선순위 중간

#### 5.6 스텁 플레이스홀더 / `-custom` 이중 뷰 구조 (죽은 코드 & 혼란)
- **문제**: `views/info/notice/index.vue`(19줄 "임시 화면입니다" 스텁)와 `views/info/notice-custom/index.vue`(143줄 실구현)처럼, 스텁 15개 / `-custom` 실구현 17개가 병존. 어느 쪽이 실제 라우팅되는지는 백엔드 메뉴 `component` 문자열에 의존 → 유지보수 시 혼란, 미사용 스텁은 번들에 포함(glob 등록 대상).
- **근거**: 스텁 15곳(`grep "임시 화면입니다"`): `views/info/notice/index.vue` 등; 실구현 `views/info/notice-custom/index.vue`
- **개선**: 실구현을 정식 경로(`index.vue`)로 승격하고 백엔드 메뉴 `component`를 갱신, 스텁·`-custom` 접미사 제거. 미완료 화면은 명시적 "준비중" 라우트 하나로 통합.

#### 5.7 i18n이 비즈니스 페이지에서 사실상 미사용
- **문제**: 로케일 인프라(`locales/langs/{ko-KR,en-US}`)와 `$t`가 있지만 **비즈니스 뷰에서 `$t(` 호출 0건**. 화면 텍스트·`message.success/error(...)` 메시지가 전부 한국어 하드코딩(예: building 뷰의 `message.*` 리터럴 121곳). `Accept-Language` 헤더는 보내면서 UI는 다국어화되지 않아 불일치.
- **근거**: `grep "$t(" views/building views/status views/system` → 0건; `views/building/status/index.vue`의 다수 한국어 리터럴(`'설정을 저장하는 중...'`, `'장비 미디어 설정 변경 실패'` 등)
- **개선**: 다국어 요구가 있다면 최소한 시스템/공통 메시지부터 `$t` 키로 이전. 요구가 없다면 en-US 리소스와 언어 전환 UI를 제거해 혼선 방지(의사결정 필요).

#### 5.8 타입 안전성 저하 — `any`/`as any` 357곳
- **문제**: 도메인 타입은 잘 정의돼 있으나 실제 사용부에서 `as any`, `: any`가 **357곳**. 특히 API 응답 언래핑(5.3), 이벤트 payload, `ref<any[]>`, `viewRecord = ref<any>(null)` 등. 코딩 표준(`docs/prompts/coding_agent_vue3_script.md`)의 "암묵적 any 금지"와 배치.
- **근거**: `views/building/status/index.vue`의 `let attr: any`, `composables/use-status-data.ts`의 `roomStatuses = ref<any[]>([])`, `videos = ref<{label; value: any}[]>` 등
- **개선**: API 반환 타입을 `namespace XxxApi` 인터페이스로 명시, 컴포저블/스토어 상태에 제네릭 부여. `vue-tsc --noEmit`(이미 `typecheck` 스크립트 존재)를 CI에 물려 회귀 방지.

#### 5.9 에러 처리 일관성
- **문제**: `try/catch`에서 `console.error` 후 조용히 무시(층 로드 실패 등)하거나(`use-status-data.ts:60`), `catch (eee)`처럼 무의미한 변수명·빈 처리(`status/index.vue:70`). 사용자 피드백/재시도 전략이 화면마다 제각각.
- **근거**: `src/views/building/status/composables/use-status-data.ts:56-63`, `src/views/building/status/index.vue:66-73`
- **개선**: 공통 에러 처리 훅/유틸로 표준화(사용자 알림 + 로깅 + 필요 시 재시도). 인터셉터의 전역 에러 표출과 화면 개별 처리의 역할 분담을 정의.

### 🟢 우선순위 낮음

#### 5.10 SignalR/AI URL 및 base 경로 하드코딩
- Hub URL·SSE URL이 `'/api/...'` 리터럴. `VITE_GLOB_API_URL` 기반으로 조립하면 gateway 프리픽스 변경 대응 용이. (`status/index.vue:37`, `ai/chat.ts:30`)

#### 5.11 로컬스토리지 채팅 세션 관리
- AI 채팅 세션을 `localStorage`에 7일 보존(`ai/chat/index.vue:19-20`). 민감 대화가 평문으로 남을 수 있고 스토어 암호화 규약과 별개로 동작. 저장 대상/보존정책 검토 필요.

#### 5.12 번들/빌드
- `fabric`, `cropperjs`, `vxe-table` 등 무거운 의존성. 표출/편집 화면에서만 쓰인다면 라우트 레벨 lazy import는 되고 있으나, 벤더 청크 분리·`build:analyze`(`.env.analyze` 존재)로 주기 점검 권장.

---

## 6. 보안 점검

| 항목 | 상태 | 근거 / 비고 |
|---|---|---|
| 스토어 암호화 키 커밋 | ⚠️ **노출** | `.env`의 `VITE_APP_STORE_SECURE_KEY=quristyle-funeralv2-260408-key`가 **git 추적됨**(`git ls-files fronts/**/.env*`). `.gitignore`는 `.env.local`만 제외. localStorage 암호화 키가 소스에 박혀 있어 클라이언트 암호화 의미 반감. → 키는 빌드 시크릿으로 분리, `.env` 언트래킹 권장. |
| 하드코딩 API URL | ✅ 양호 | 프론트는 상대경로 `/api`만 사용, 백엔드 절대주소는 dev proxy(`127.0.0.1:5265`)에만 존재. 프로덕션 시크릿 없음. |
| accessToken 저장 위치 | ⚠️ 보통 | JS 접근 가능한 Pinia/localStorage에 저장(XSS 시 탈취 위험). refreshToken은 httpOnly 쿠키로 보이므로 상대적으로 양호. XSS 방어(입력 이스케이프, CSP) 병행 필요. |
| SignalR 인증 | ⚠️ 확인필요 | `accessTokenFactory` 미설정(5.1). Hub가 인증을 요구하면 취약/오동작. |
| AI 스트리밍 인증 | ⚠️ 확인필요 | 원시 `fetch`에 `Authorization` 없음(5.2). |
| 토큰 갱신 흐름 | ✅ 양호 | 401 시 refresh → 실패 시 재인증 인터셉터 구성(`request.ts:118-126`). |
| 콘솔 정보 노출 | ⚠️ | 라우터 가드가 라우트/컴포넌트 매칭 정보를 프로덕션 콘솔에 상시 출력(5.4). |
| 디바이스 속성 하드코딩 기본값 | ℹ️ | `handleUpdateDeviceMedia`에 장비 속성 기본값이 뷰에 하드코딩(`status/index.vue:75-92`). 보안보다는 유지보수 이슈. |

---

## 7. 종합 권고

funeralv2 프론트는 Vben의 동적 메뉴/권한·인증 인프라를 잘 재사용하고 있고, BigInt 처리·도메인 타입 정의 등 기반은 견고하다. 핵심 도메인(건물/빈소/장비/미디어, 실시간 현황 대시보드)은 실제로 구현되어 동작 중이다. 다만 **"동작하는 프로토타입"에서 "운영 코드"로의 정리**가 필요한 단계로 보인다.

**즉시 조치 권장(높음)**
1. SignalR·AI 스트리밍에 인증 토큰 주입 및 URL env화 (5.1, 5.2)
2. 라우터 가드 디버그 로깅 및 전역 `console.*` 제거 + 프로덕션 devtools 비활성화 (5.4, 5.5)
3. `.env` 언트래킹 및 스토어 암호화 키 시크릿 분리 (6장)

**단기(중간)**
4. 응답 언래핑 규약을 API 레이어 한 곳으로 통일해 `(res as any)?.result ?? res` 방어 코드 제거 (5.3)
5. 스텁/`-custom` 이중 뷰 정리 및 실구현 정식 승격 (5.6)
6. `typecheck`(vue-tsc)를 CI 게이트로 걸어 `any` 회귀 차단, 에러 처리 표준화 (5.8, 5.9)

**정책 결정 필요**
7. 다국어 실사용 여부 결정 후 i18n 전면 적용 또는 en-US 리소스 제거 (5.7)
8. AI 채팅 localStorage 보존정책·민감정보 취급 검토 (5.11)
