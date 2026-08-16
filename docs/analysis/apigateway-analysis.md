# ApiGateway 분석 (Funeralv2)

> 분석 범위: `/home/quri/Funeralv2/ApiGateway/` 디렉터리
> 분석 일자: 2026-08-14

---

## 1. 개요 (기술스택, 버전, 역할)

| 항목 | 내용 |
|---|---|
| 게이트웨이 기술 | **YARP (Yet Another Reverse Proxy)** — `Yarp.ReverseProxy` **2.3.0** (`ApiGateway.csproj:12`) |
| 런타임 | **.NET 8.0** (`ApiGateway.csproj:4`, `net8.0`) |
| 프로젝트 유형 | `Microsoft.NET.Sdk.Web` (ASP.NET Core Minimal Hosting, `Program.cs`) |
| 인증 라이브러리 | `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.0 (`ApiGateway.csproj:10`) |
| 부가 라이브러리 | `Spectre.Console` 0.55.2 — 기동 배너/콘솔 출력용 (`Program.cs:181-218`) |
| 라우팅 구성 방식 | `appsettings.json`의 `ReverseProxy` 섹션을 `LoadFromConfig()`로 로드 (`Program.cs:61-62`) |
| 게이트웨이 포트 | HTTP `5265`, HTTPS(dev) `7167` (`Properties/launchSettings.json:16,25`) |

**역할**: 프런트엔드(Vue3, `http://localhost:5555`)와 Flutter 플레이어/키오스크 기기가 단일 진입점으로 게이트웨이에 접속하고, 게이트웨이는 4개의 백엔드 마이크로서비스로 요청을 리버스 프록시한다. 게이트웨이는 **JWT 1차 검증**을 수행하고, 검증된 클레임을 내부 전용 헤더(`X-User-*`)로 변환해 백엔드로 전달한다. funeralv2Api의 SignalR Hub로의 WebSocket 패스스루도 담당한다.

---

## 2. 라우팅 구성 (경로→서비스 매핑, 포트)

### 2.1 클러스터(백엔드 서비스) → 포트

| 클러스터 | 서비스 | 주소 | 설정 위치 |
|---|---|---|---|
| `auth-cluster` | AuthServer | `http://localhost:5264` | `appsettings.json:187` |
| `funeral-cluster` | funeralv2Api (SignalR 포함) | `http://localhost:5320` | `appsettings.json:193` |
| `file-cluster` | FileServer | `http://localhost:5350` | `appsettings.json:197` |
| `ai-cluster` | AIAgentServer | `http://localhost:5029` | `appsettings.json:201` |

### 2.2 경로 → 서비스 매핑 (라우트)

| 경로(Match Path) | Method | 클러스터/서비스 | 인증 | Order | 접두어 제거 | 위치 |
|---|---|---|---|---|---|---|
| `/api/auth/{**}` | ALL | auth (5264) | **Anonymous** | - | `/api/auth` | `appsettings.json:16` |
| `/api/funeral/building/device/code/{code}` | GET | funeral (5320) | **Anonymous** | 1 | `/api/funeral` | `:26` |
| `/api/funeral/building/deceased/deviceCode/{deviceCode}` | GET | funeral (5320) | **Anonymous** | 1 | `/api/funeral` | `:38` |
| `/api/funeral/building/deceased/guide/deviceCode/{deviceCode}` | GET | funeral (5320) | **Anonymous** | 1 | `/api/funeral` | `:50` |
| `/api/funeral/building/deceased/kiosk/deviceCode/{deviceCode}` | GET | funeral (5320) | **Anonymous** | 1 | `/api/funeral` | `:62` |
| `/api/funeral/building/source/{id}` | GET | funeral (5320) | **Anonymous** | 1 | `/api/funeral` | `:74` |
| `/api/funeral/hubs/device/{**}` (SignalR) | ALL | funeral (5320) | **Anonymous** | 1 | `/api/funeral` | `:86` |
| `/api/funeral/{**}` | ALL | funeral (5320) | **JWT 필수** | 2 | `/api/funeral` | `:97` |
| `/api/file/download/{**}` | ALL | file (5350) | **Anonymous** | 1 | `/api/file` | `:107` |
| `/api/file/thumbnail/{**}` | ALL | file (5350) | **Anonymous** | 1 | `/api/file` | `:118` |
| `/api/file/medium/{**}` | ALL | file (5350) | **Anonymous** | 1 | `/api/file` | `:129` |
| `/api/file/large/{**}` | ALL | file (5350) | **Anonymous** | 1 | `/api/file` | `:140` |
| `/api/file/resize/{**}` | ALL | file (5350) | **Anonymous** | 1 | `/api/file` | `:151` |
| `/api/file/{**}` | ALL | file (5350) | **JWT 필수** | 2 | `/api/file` | `:162` |
| `/api/ai/{**}` | ALL | ai (5029) | **Anonymous** | - | `/api/ai` | `:173` |

### 2.3 인증/헤더/CORS/WebSocket 처리

- **JWT 검증** (`Program.cs:25-42`): HS256 대칭키(`Jwt:Key`), Issuer/Audience 검증, `ClockSkew=Zero`.
- **Deny-by-default 정책** (`Program.cs:44-47`): `FallbackPolicy = DefaultPolicy` — 명시적으로 `Anonymous`가 아닌 모든 라우트는 인증 필수.
- **헤더 위조 방지 트랜스폼** (`Program.cs:63-97`): 외부에서 들어온 `X-User-Id/Role/Company-Id`를 **무조건 제거**한 뒤, 게이트웨이가 검증한 JWT 클레임으로 재생성해 백엔드에 전달.
- **CORS** (`Program.cs:49-58`): `AllowFrontend` 정책, `http://localhost:5555` 단일 오리진만 허용, `AllowAnyHeader/AnyMethod/AllowCredentials`.
- **WebSocket/SignalR**: YARP은 기본적으로 WebSocket 업그레이드를 패스스루하므로 `signalr-hub-anonymous-route`(`/api/funeral/hubs/device/**`)가 SignalR을 중계. 별도 타임아웃/활성 연결 설정은 없음.
- **에러 처리** (`Program.cs:103-145`): 502/504 응답과 404(MapFallback)에 대해 일관된 JSON 에러 봉투(`success/code/message/traceId/path`) 반환.

---

## 3. 강점

1. **Deny-by-default 인가 정책** (`Program.cs:46`): `FallbackPolicy`를 기본 정책으로 설정하여, 라우트에 `Anonymous`를 명시하지 않으면 자동으로 인증이 강제된다. 신규 라우트 추가 시 보안 누락 위험이 낮다.
2. **헤더 스푸핑 방지** (`Program.cs:68-93`): 외부 요청의 `X-User-*` 헤더를 제거 후 검증된 클레임으로 재작성하는 패턴은 게이트웨이 아키텍처의 모범 사례이다. 백엔드는 이 헤더를 신뢰할 수 있다.
3. **중앙 집중식 JWT 검증**: 각 마이크로서비스가 JWT 검증을 중복 구현하지 않고 게이트웨이에서 1차 검증한다.
4. **일관된 에러 응답 봉투** (`Program.cs:110-121, 132-144`): 502/504/404에 대해 `traceId`, `path`, 타임스탬프를 포함한 표준 JSON을 반환하여 프런트 처리가 용이하다.
5. **라우트 우선순위 설계** (`Order` 사용): 익명 예외 경로(`Order:1`)를 광범위 catch-all(`Order:2`)보다 먼저 매칭하도록 명확히 구성했다.
6. **명확한 관측용 기동 배너** (`Program.cs:156-219`): 서버명/환경/PID/포트를 시각적으로 표시.

---

## 4. 개선점

### 4.1 우선순위: 높음 🔴

#### [H-1] JWT 시크릿 하드코딩 및 저장소 커밋
- **문제**: 대칭키가 `appsettings.json:10`에 평문(`"a-very-secret-key-that-is-long-enough-for-security"`)으로 저장되어 저장소에 커밋됨. 게다가 `Program.cs:26`에 동일한 문자열이 fallback 기본값으로 하드코딩됨.
- **근거**: `appsettings.json:9-13`, `Program.cs:26`.
- **영향**: 이 키를 아는 사람은 임의 사용자/역할/CompanyId로 위조 토큰을 발급할 수 있어 전체 인증 체계가 무력화된다. HS256 대칭키는 AuthServer와 게이트웨이가 공유하므로 유출 시 파급이 크다.
- **개선방안**: 키를 환경변수/시크릿 매니저(User-Secrets, 배포 시 환경변수, Vault/AWS Secrets Manager)로 이전하고 저장소에서 제거(이미 커밋되었으므로 **키 로테이션 필수**). `Program.cs:26`의 fallback 기본값을 제거하고 키 미설정 시 기동 실패(fail-fast)하도록 변경. 중장기적으로 비대칭키(RS256) 도입 검토.

#### [H-2] TLS 미적용 (게이트웨이→백엔드, HTTPS 리다이렉트 부재)
- **문제**: 모든 클러스터 주소가 `http://localhost`(`appsettings.json:187,193,197,201`). `UseHttpsRedirection`/HSTS 미사용. 검증된 `X-User-*` 신뢰 헤더가 평문으로 전송됨.
- **근거**: `appsettings.json:184-205`, `Program.cs`(HTTPS 관련 미들웨어 없음).
- **영향**: 동일 호스트가 아닌 배포 환경에서 내부 트래픽 도청/변조로 신뢰 헤더 위조 가능.
- **개선방안**: 백엔드 간 통신을 HTTPS(또는 mTLS)로 전환하거나 신뢰 네트워크 격리. 외부 노출 게이트웨이에 HTTPS 강제 및 HSTS 적용.

#### [H-3] 레이트 리밋 전무
- **문제**: 어떤 라우트에도 rate limiting이 없음. .NET 8 내장 `RateLimiter` 미사용.
- **근거**: `Program.cs` 전체에 `AddRateLimiter`/`UseRateLimiter` 없음.
- **영향**: (1) `/api/auth`(로그인) 무차별 대입 공격, (2) `/api/ai`(비용 발생 AI 호출) 익명 남용, (3) `deviceCode` 기반 익명 조회 경로의 열거(enumeration) 공격에 무방비.
- **개선방안**: .NET 8 `AddRateLimiter`로 라우트/IP/사용자별 정책 추가. 특히 auth·ai·익명 조회 경로에 우선 적용.

#### [H-4] AI 및 파일 다운로드 라우트가 전면 익명
- **문제**: `/api/ai/{**}` 전체가 `Anonymous`(`appsettings.json:175`). 파일의 download/thumbnail/medium/large/resize 경로도 모두 `Anonymous`(`:107-161`).
- **근거**: `appsettings.json:107-161, 173-182`.
- **영향**: 인증 없이 AIAgentServer의 모든 엔드포인트 호출 가능(비용/오남용). 파일 경로는 식별자만 알면 무인증 접근 가능(IDOR 위험). 익명 라우트에서는 `X-User-*` 헤더도 생성되지 않아 백엔드가 호출자 식별/인가 불가.
- **개선방안**: AI 라우트는 인증 필수로 전환하거나 최소한 레이트 리밋+API 키 적용. 파일 다운로드는 서명된 URL/토큰 또는 소유권 검증 도입.

### 4.2 우선순위: 중간 🟡

#### [M-1] 복원력(Resilience) 설정 부재
- **문제**: 클러스터에 타임아웃/재시도/서킷브레이커/헬스체크가 없음. 백엔드 1대 구성이라 장애 시 페일오버 불가.
- **근거**: `appsettings.json:184-205`(클러스터에 `HttpRequest`, `HealthCheck` 설정 없음).
- **개선방안**: YARP 클러스터에 `HttpRequest.ActivityTimeout` 설정, `HealthCheck`(Active/Passive) 구성. .NET 8의 `Microsoft.Extensions.Http.Resilience`(Polly)로 재시도/서킷브레이커 추가. 다중 destination 구성 시 로드밸런싱.

#### [M-2] 하드코딩된 목적지 URL/포트 (서비스 디스커버리 부재)
- **문제**: 모든 백엔드 주소가 `localhost` 고정. `appsettings.Development.json`은 로깅만 오버라이드하고 서비스 주소를 재정의하지 않음.
- **근거**: `appsettings.json:184-205`, `appsettings.Development.json:1-8`.
- **영향**: 컨테이너/다중 호스트/스테이징·프로덕션 배포 시 코드/설정 수정 필요. 환경별 분리 불가.
- **개선방안**: 목적지 주소를 환경변수/환경별 appsettings로 외부화. 컨테이너 환경이면 DNS 기반 서비스명 사용, 오케스트레이터 사용 시 서비스 디스커버리 연동.

#### [M-3] 관측성(Observability) 미흡 — 상관관계 ID/구조화 로깅/트레이싱 부재
- **문제**: `traceId`(`context.TraceIdentifier`)는 에러 응답에만 쓰이고 백엔드로 전파되지 않음(`X-Request-ID`/`X-Correlation-ID` 미전달). 구조화 요청 로깅·분산 추적(OpenTelemetry) 없음.
- **근거**: `Program.cs:117,139`(traceId는 응답 전용), 트랜스폼에 상관관계 헤더 추가 없음(`:63-97`).
- **영향**: 게이트웨이↔마이크로서비스 간 요청 추적 불가로 장애 분석이 어렵다.
- **개선방안**: 트랜스폼에서 `X-Correlation-ID`를 생성/전파. `UseHttpLogging` 또는 Serilog 구조화 로깅, OpenTelemetry(트레이스/메트릭) 도입.

#### [M-4] SignalR/WebSocket 장수명 연결 설정 부재
- **문제**: SignalR 허브 라우트는 존재하나 WebSocket 활성 타임아웃(`ActivityTimeout`, 기본 100초) 등 장수명 연결 관련 클러스터 옵션이 없음.
- **근거**: `appsettings.json:86-96`(라우트만), 클러스터에 관련 옵션 없음(`:190-194`).
- **개선방안**: funeral-cluster에 WebSocket에 적합한 `HttpRequest.ActivityTimeout`(또는 무제한) 및 keep-alive 설정을 명시하여 유휴 SignalR 연결이 끊기지 않도록 검증.

### 4.3 우선순위: 낮음 🟢

#### [L-1] 전역 500MB 요청 본문 제한이 모든 라우트에 적용
- **문제**: Kestrel `MaxRequestBodySize`·Multipart 제한을 전역 500MB로 상향(`Program.cs:11-22`). 대용량 업로드는 FileServer 경로에만 필요하나 전 라우트에 적용됨.
- **근거**: `Program.cs:12-22`.
- **영향**: auth/ai 등 소용량 경로까지 대용량 페이로드를 허용해 DoS 표면 확대.
- **개선방안**: 업로드 경로(`/api/file/**`)에만 크기 상향 적용, 나머지는 보수적 기본값 유지.

#### [L-2] 에러 미들웨어가 503 등 미처리
- **문제**: 커스텀 에러 봉투가 502/504만 처리(`Program.cs:106-107`). 503(Service Unavailable) 등은 원문 그대로 노출.
- **개선방안**: 5xx 계열 전반으로 확장하고, 예외 처리 미들웨어(`UseExceptionHandler`)로 미검증 예외도 표준화.

#### [L-3] `appsettings.Development.json` 미구성으로 개발 환경도 커밋된 시크릿 사용
- **문제**: 개발 프로파일이 `Development`(`launchSettings.json:18,26`)인데 Development 설정에 Jwt/ReverseProxy 오버라이드가 없어 커밋된 프로덕션급 시크릿을 그대로 사용.
- **근거**: `appsettings.Development.json:1-8`.
- **개선방안**: 개발용 시크릿은 User-Secrets로 분리, 환경별 설정 명확화.

#### [L-4] `AllowedHosts: "*"` 및 CORS 오리진 하드코딩
- **문제**: `AllowedHosts`가 와일드카드(`appsettings.json:8`). CORS 허용 오리진이 `http://localhost:5555`로 코드에 하드코딩(`Program.cs:53`)되어 프로덕션 프런트 도메인·키오스크 오리진을 반영 못함.
- **개선방안**: `AllowedHosts`를 배포 도메인으로 제한. CORS 오리진을 설정으로 외부화하여 환경별 지정.

---

## 5. 보안 점검

| 점검 항목 | 상태 | 근거 |
|---|---|---|
| JWT 서명 검증 활성화 | ✅ 양호 | `Program.cs:34-35` |
| Issuer/Audience 검증 | ✅ 양호 | `Program.cs:37-39` |
| ClockSkew 최소화 | ✅ 양호 (`Zero`) | `Program.cs:40` |
| Deny-by-default 인가 | ✅ 양호 | `Program.cs:46` |
| 신뢰 헤더 스푸핑 방지 | ✅ 우수 | `Program.cs:68-93` |
| 시크릿 관리 | 🔴 위험 (평문 커밋 + 하드코딩 fallback) | `appsettings.json:10`, `Program.cs:26` |
| 전송 구간 암호화(TLS) | 🔴 미흡 (백엔드 http, 리다이렉트 없음) | `appsettings.json:187-201` |
| 레이트 리밋 | 🔴 없음 | `Program.cs` 전반 |
| 익명 노출 표면 | 🟡 과다 (ai 전면, 파일 다운로드 전면 익명) | `appsettings.json:107-161,175` |
| CORS 범위 | 🟡 하드코딩 단일 오리진 | `Program.cs:53` |
| 대칭키(HS256) 공유 방식 | 🟡 키 유출 시 파급 큼 | `Program.cs:26-35` |
| CORS AllowCredentials + 특정 오리진 | ✅ 적절(와일드카드 아님) | `Program.cs:53-56` |

**요약**: 게이트웨이 아키텍처의 핵심 보안 패턴(헤더 스푸핑 방지, deny-by-default, 중앙 JWT 검증)은 잘 구현되어 있으나, **시크릿 관리, TLS, 레이트 리밋** 3개 축이 미비하여 실제 프로덕션 노출 시 위험도가 높다.

---

## 6. 종합 권고

**즉시 조치(높음)**
1. JWT 대칭키를 시크릿 매니저/환경변수로 이전, 저장소에서 제거 후 **키 로테이션**. `Program.cs:26` fallback 제거하고 fail-fast 처리. → [H-1]
2. `/api/auth`, `/api/ai`, 익명 `deviceCode` 조회 경로에 .NET 8 `RateLimiter` 도입. → [H-3]
3. `/api/ai`·파일 다운로드 경로의 인증/인가 정책 재검토(서명 URL, API 키 등). → [H-4]
4. 외부 노출 구간 HTTPS 강제, 내부 구간 TLS/네트워크 격리. → [H-2]

**단기 개선(중간)**
5. YARP 클러스터에 타임아웃·헬스체크·재시도/서킷브레이커(Polly) 구성. → [M-1]
6. 백엔드 주소를 환경변수/환경별 설정으로 외부화(서비스 디스커버리 검토). → [M-2]
7. `X-Correlation-ID` 전파 + 구조화 로깅 + OpenTelemetry 도입. → [M-3]
8. SignalR 라우트의 WebSocket 타임아웃/keep-alive 명시. → [M-4]

**중장기 정비(낮음)**
9. 요청 본문 크기 제한을 업로드 경로로 한정, 에러 미들웨어를 5xx 전반+예외 처리로 확장, `AllowedHosts`/CORS 오리진 설정 외부화. → [L-1~L-4]

전반적으로 **아키텍처 설계 의도(단일 진입점·중앙 인증·신뢰 헤더 전파)는 견고**하나, 운영 성숙도(시크릿·TLS·복원력·관측성·레이트 리밋) 측면의 보강이 프로덕션 전환 전 필수이다.
