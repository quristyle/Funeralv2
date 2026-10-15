# 현재 시스템의 SPA(Single Page Application) 전환 시 기술적 문제 요소 및 영향 분석

장례식장 관리 시스템 + JSini 업무 포털(`Funeralv2`)의 프론트엔드를 현재의 **.NET 10 Blazor Interactive Server + Piral.Blazor MFE** 구조에서 **SPA 형태**로 변경할 경우 발생할 가장 치명적인 문제 요소와 시스템적 영향을 기술적으로 분석한 문서다.

---

## 1. 배경 및 현행 시스템 구조의 본질

본 시스템은 과거 Vue3 + vben(TypeScript/Vite/pnpm) 기반의 클라이언트 사이드 SPA 모노레포(`fronts/`, 약 90,000줄)를 운영하다 **2026-09-05에 이를 완전히 걷어내고**, 현재의 **.NET 10 Blazor Server + Piral.Blazor MFE + DevExpress Blazor** 아키텍처로 전면 재구축되었다.

### 현재 구조의 3대 기둥
```
Browser (세션 쿠키만 보유, 토큰 비노출)
  │
  ▼ [SignalR WebSocket 회로 (DOM diff만 전송)]
Blazor 업무 포털 셸 (:5557, BFF)
  ├── Piral.Blazor MFE (8개 모듈 빌드 시점 합성: ProjectReference)
  ├── DevExpress Blazor 26.1.4 (서버 렌더링 UI)
  └── AuthTokenHandler (쿠키 클레임에서 JWT 추출, 내부 게이트웨이 호출 및 토큰 갱신)
        │
        ▼ (내부 REST API)
  API Gateway (:5265) ─── AuthServer / funeralv2Api / AIAgent / HelpDesk 등
```

현 시스템을 SPA(클라이언트 사이드 SPA, 예: Vue/React 또는 Blazor WebAssembly)로 변경할 때 발생하는 문제는 단순한 프레임워크 교체가 아니라, **보안 모델·상태 관리·모듈 합성·권한 통제 전반의 붕괴**를 의미한다.

---

## 2. 가장 문제가 되는 핵심 요소

### 2.1 인증 및 보안 아키텍처의 붕괴 (BFF 패러다임 파괴와 XSS 위협)

**가장 치명적인 문제는 보안 모델의 퇴보와 인증 흐름의 파탄이다.**

1. **토큰의 브라우저 노출 및 XSS 취약점 부활**
   - **현재 상태**: 순수 BFF(Backend for Frontend) 패턴으로, 브라우저에는 오직 암호화된 `DataProtection` 세션 쿠키만 존재한다. API Gateway용 JWT Access Token과 Refresh Token은 브라우저(JavaScript/클라이언트)로 절대 내려가지 않는다.
   - **SPA 변경 시**: 클라이언트 브라우저가 직접 API Gateway를 호출해야 하므로, JWT 토큰이 브라우저 메모리나 `localStorage`에 노출된다. 이는 과거 Vue 시절 겪었던 XSS 기반 토큰 탈취 위험에 다시 노출됨을 의미한다.
2. **동시성 토큰 갱신 통제 상실 (`AuthTokenHandler`)**
   - **현재 상태**: 포털 셸 내부의 `AuthTokenHandler`가 `SemaphoreSlim` 락과 `SentToken` 옵션을 통해, 한 회로에서 수많은 API가 동시에 401을 받더라도 단 1회의 갱신 요청만 게이트웨이로 나가도록 서버 메모리 수준에서 완벽히 동기화한다.
   - **SPA 변경 시**: 클라이언트 측 JS 인터셉터로 이를 재구현해야 하며, 브라우저 다중 탭, 다중 비동기 요청 간의 토큰 갱신 충돌(Race Condition) 및 세션 끊김 현상이 재발한다.
3. **인증 중계 체계의 해체**
   - 셸이 서버 사이드에서 담당하던 **소셜 로그인 리다이렉트 흐름(`MapSocialLoginFlow`)**, **패스키(WebAuthn) 도전값 중계 및 익명 검증**, **첨부 파일 다운로드 보안 프록시(`FileDownload`)** 기능이 갈 곳을 잃으며, API Gateway나 별도 백엔드에 전면 재설계가 강제된다.

---

### 2.2 DevExpress Blazor 기반 거대 UI 생태계의 전면 재작성 (매몰 비용)

**조직과 개발 비용 측면에서 가장 큰 재앙이 되는 요소다.**

1. **수만 줄 규모의 복잡한 업무 컴포넌트 폐기**
   - 장례식장 현황판, 빈소 관리, 접수 대시보드, 운송관리, 헬프데스크 등 8개 모듈 전반에 걸쳐 고도화된 그리드(`DxGrid`), 폼 레이아웃(`DxFormLayout`), 드래그 팝업(`DxPopup`, `CommPopup`), 탭, 차트 등이 C# DevExpress Blazor 26.1.4에 깊숙이 결합되어 있다.
   - **JS/TS SPA로 변경 시**: 이 모든 화면을 Element Plus, Ant Design, MUI 등으로 100% 처음부터 다시 작성해야 한다. 90,000줄의 Vue 코드를 C#으로 옮겼던 작업을 다시 역방향으로 수행해야 하므로 막대한 인적·시간적 비용이 소모된다.
2. **Blazor WebAssembly(WASM) SPA 선택 시의 번들 크기 및 Cold Start 참사**
   - C# 코드를 유지하기 위해 Blazor WASM으로 전환하더라도, DevExpress Blazor 어셈블리와 .NET Wasm 런타임(`dotnet.wasm`), 8개 모듈 DLL 전체를 브라우저가 최초 1회 모두 다운로드해야 한다.
   - 번들 크기만 수십 MB에 달하며, 최초 화면 렌더링(Cold Start)에 **5초~15초 이상 소요**되어 실제 현장(장례식장 상담, 접수 창구 등)의 사용자 경험이 심각하게 저하된다.

---

### 2.3 마이크로프론트엔드(MFE) 아키텍처 및 빌드 시점 합성 체계의 해체

**현재 시스템은 8개 업무 모듈이 C# 어셈블리 수준에서 격리·합성되는 우아한 단일 프로세스 MFE 구조를 갖추고 있다.**

1. **빌드 시점 합성(`ProjectReference`)과 DI 격리의 상실**
   - 현재는 `PortalModuleRegistry`가 출력 폴더의 `JSini.Web.*.dll`을 탐색하고 `Piral.Blazor` 컨테이너를 통해 각 모듈의 DI를 격리하며, 셸에 단일 프로세스로 탑재된다.
   - SPA로 전환 시: Webpack Module Federation이나 Vite MFE, pnpm 모노레포와 같은 복잡한 프론트엔드 모듈 연동 체계를 다시 도입해야 하며, 빌드 도구 설정의 복잡성, 공통 라이브러리 버전 충돌, 모듈 간 CSS 오염 등의 문제가 부활한다.
2. **단일 도커 이미지 배포 파이프라인 무력화**
   - 현재는 `funeralv2-portal` 단 하나의 컨테이너 이미지로 8개 모듈이 빌드·배포된다. SPA로 쪼개질 경우 Nginx 정적 파일 서빙, 각 모듈별 CDN/정적 배포 등 인프라 파이프라인이 전면 수정되어야 한다.

---

### 2.4 라우팅 소유권 역전(`RouteKey`)과 컴파일 타임 검증 체계의 상실

**Vue 시절 겪었던 가장 고질적인 버그 체계로 회귀하게 된다.**

| 구분 | 과거 Vue SPA 시절 | 현재 Blazor Server | SPA로 변경 시 예상 문제 |
|---|---|---|---|
| **라우트 정의** | DB `scom.system_menus.component` | C# 코드의 `@page` | 클라이언트 라우터와 DB 간의 분리 재발 |
| **연결 고리** | Vue 파일 상대 경로 (문자열) | 고유 열쇠 `[RouteKey("...")]` | 문자열 경로 매핑으로 회귀, 오타 취약 |
| **불일치 발견** | 런타임 브라우저 `console.warn` | **컴파일 시점 + 65건의 아키텍처 단위 테스트** | 컴파일 타임 정합성 보장 불가 |

1. **DB 메뉴와 화면 간의 불일치 위험**
   - 과거 Vue 시절에는 DB의 `component` 문자열이 파일 경로와 조금만 어긋나도 화면이 404가 나거나 런타임 오류가 발생했다.
   - 현재는 `RouteInventory`와 단위 테스트(`RouteKeyTests`)가 화면마다 유일한 키를 빌드 타임에 검사하고 매핑한다.
   - SPA로 회귀하면 코드와 DB 메뉴 간의 컴파일 시점 검증이 불가능해져, 메뉴는 있는데 화면이 안 열리거나 권한이 끊기는 사고가 재발한다.

---

### 2.5 화면 접근 통제(`ScreenAccess`) 및 라우터 게이트(`ScreenGate`) 무력화

1. **상세 화면의 부모 메뉴 권한 추적 상실**
   - 현재 시스템은 `[ScreenAccess("admin.system.account")]`처럼 메뉴에 없는 상세 화면이 부모 메뉴의 권한을 상속받도록 선언되어 있다.
   - `Routes.razor`의 `ScreenGate`가 라우터 바로 앞에서 컴포넌트 타입(`RouteData.PageType`)을 직접 확인하여 관리되지 않은 주소는 기본 차단(`UNMANAGED`)하고 AuthServer로 즉시 감사 로그를 적재한다.
2. **클라이언트 사이드 네비게이션 가드의 보안 한계**
   - SPA의 JavaScript 네비게이션 가드는 브라우저 내부 메모리에서 동작하므로, 클라이언트 조작이나 번들 역공학에 취약하다. 컴포넌트 클래스 메타데이터를 기반으로 라우팅 직전에 차단하는 강력한 서버 게이트 방식을 동일하게 모사하기 어렵다.

---

### 2.6 상태 관리 패러다임 변화 (SignalR 회로 vs REST API + Client Store)

1. **서버 사이드 상태 유지의 장점 상실**
   - Blazor Server는 컴포넌트 인스턴스와 데이터 모델이 서버 프로세스 메모리에 상주하므로, 클라이언트 상태 관리 라이브러리(Pinia, Redux) 없이도 C# 변수/필드만으로 직관적인 상태 관리가 가능하다.
   - 실시간 장비 온라인 상태, 푸시 알림 수신, 빠른 지시 서랍(`AiAskPanel`) 등이 WebSocket 회로를 통해 즉각적으로 UI에 반영된다.
2. **REST API 및 클라이언트 Store의 폭증**
   - SPA로 전환 시 화면의 사소한 상태 변경, 임시 데이터 유지, 탭 전환 상태 등을 모두 클라이언트 Store에 저장해야 하며, 서버와 동기화하기 위한 수많은 자잘한 REST API 엔드포인트를 백엔드에 신설해야 한다.

---

### 2.7 오류 추적 및 진단 시스템(`TraceNumber`)의 단절

1. **현재 구조**
   - 셸에서 예외가 발생하면 `TraceNumber.Generate()`로 추적 번호를 즉시 발급하고, 비동기 백그라운드 큐(`PortalErrorReporter`)를 통해 AuthServer의 `scom.portal_error_logs`에 적재한다. 사용자가 오류 화면의 번호를 알려주면 운영자가 즉시 원인을 찾을 수 있다.
2. **SPA 변경 시 문제**
   - 브라우저 단에서 발생하는 Unhandled Exception은 클라이언트 네트워크 단절, 브라우저 확장 프로그램 간섭, AdBlocker 등에 의해 수집 서버로 도달하지 못하고 유실될 가능성이 매우 높다. 신뢰성 있는 전사 에러 추적 체계 구축을 위해 Sentry 등 외부 솔루션 도입이 필연적이다.

---

## 3. 시나리오별 비교 요약

| 평가 항목 | 현행 Blazor Server MFE | Blazor WASM SPA 전환 시 | Vue/React 등 JS SPA 전환 시 |
|---|---|---|---|
| **보안 (BFF / 토큰)** | 최상 (브라우저 토큰 0, 세션 쿠키 전용) | 중 (토큰 브라우저 노출 또는 별도 BFF 필요) | 취약 (토큰 노출, XSS 위험, 갱신 충돌) |
| **UI 재작성 비용** | 자산 100% 활용 중 | 중 (일부 WASM 호환성 수정 필요) | **극심 (약 9만 줄 전면 재작성)** |
| **초기 로딩 속도** | 빠름 (경량 HTML + 즉시 회로 연결) | **극심한 지연 (WASM/DLL 다운로드 수십 MB)** | 양호 (코드 스플리팅 적용 시) |
| **MFE 및 배포 구조** | 단순 (어셈블리 합성, 단일 이미지) | 복잡 (WASM 정적 파일 분산 서빙) | **극심하게 복잡 (Module Federation, pnpm)** |
| **라우팅 / 권한 정합성** | 완벽 (컴파일 타임 테스트 및 [RouteKey]) | 양호 (.NET 메타데이터 유지 가능) | **취약 (과거 Vue 시절의 메뉴 불일치 재발)** |
| **개발 생산성** | 풀스택 C# 단일 언어 및 타입 공유 | 풀스택 C# 유지 가능 | C# / TS 언어 파편화 및 DTO 이중 관리 |

---

## 4. 최종 결론

현재 시스템을 SPA 형태로 변경할 때 가장 문제가 될 핵심 요소를 한 문장으로 정의하면:

> **"토큰을 브라우저에 노출하지 않는 현재의 BFF 보안 모델이 무너지며, 이미 검증된 수만 줄의 DevExpress Blazor 기반 C# 컴포넌트 자산과 컴파일 타임 라우팅/권한 검증(`RouteKey`/`ScreenAccess`) 체계 전체를 폐기하거나 감당하기 힘든 런타임 성능 저하(WASM Cold Start)를 떠안아야 한다는 점"**이다.

따라서 특수한 오프라인 동작 요구사항이나 극단적인 서버 리소스 절감이 필요한 경우가 아니라면, 현행 Blazor Server MFE 아키텍처를 SPA로 되돌리는 것은 **기술적 부채의 재발(과거 2026-09-05 이전의 문제점들로 회귀)과 천문학적인 비용 낭비**를 초래할 뿐 실익이 없다.
