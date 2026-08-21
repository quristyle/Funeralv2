# Funeralv2 시스템 전체 개요 및 프로젝트 관계

> 작성일: 2026-08-14
> 대상 저장소: `/home/quri/Funeralv2`

## 1. 시스템 개요

Funeralv2는 **장례식장 운영 관리 및 디스플레이(사이니지) 시스템**이다. 관리자용 웹 콘솔, 장례식장 내 화면에 고인/빈소 정보를 실시간 표출하는 플레이어, 그리고 이를 뒷받침하는 .NET 마이크로서비스 백엔드로 구성된다.

전체는 4개 프로젝트(폴더)로 구성된다. 사용자가 지칭한 이름과 실제 폴더명은 다음과 같이 대응한다.

| 사용자 지칭 | 실제 폴더 | 기술 스택 | 역할 |
|---|---|---|---|
| `aipgateway` | `ApiGateway/` | .NET (YARP Reverse Proxy) | API 게이트웨이 / 단일 진입점 |
| `msa` | `microservices/` | .NET + EF Core + PostgreSQL | 마이크로서비스 백엔드 (Auth, 업무 API, AI, 파일) |
| `front` | `fronts/` | Vue 3 + Vben Admin (pnpm/turbo 모노레포) | 관리자 웹 프론트엔드 |
| `funeral_player` | `funeralv2_player/` | Flutter (Dart) | 장례식장 사이니지 플레이어 |

> 참고: 루트에는 이 외에도 `funeralv2/`(backend-mock), `AI.md`(코딩 표준), `docs/`(운영/빌드 문서), 실행 스크립트(`backend_run_ubuntu.sh` 등)가 있다.

## 2. 아키텍처 관계도

```
                    ┌─────────────────────────────┐
                    │  관리자 (브라우저)           │
                    │  fronts (Vue3, vite :dev)    │
                    └──────────────┬──────────────┘
                                   │  /api/* (vite proxy → :5265)
                                   ▼
   ┌───────────────┐      ┌──────────────────────────────┐
   │ funeralv2_    │      │  ApiGateway (YARP)            │
   │ player        │─────▶│  http://localhost:5265        │
   │ (Flutter,     │ SignalR + REST                       │
   │  라즈베리파이)│      │  경로 기반 라우팅 + JWT 인증  │
   └───────────────┘      └───┬─────────┬─────────┬───────┘
                              │         │         │        │
              ┌───────────────┘   ┌─────┘    ┌────┘    ┌───┘
              ▼                    ▼          ▼         ▼
     ┌────────────────┐  ┌────────────────┐ ┌────────┐ ┌──────────────┐ ┌──────────────┐
     │ AuthServer     │  │ funeralv2Api   │ │FileSvr │ │ AIAgentServer│ │ HelpDeskSvr  │
     │ :5264          │  │ :5320          │ │ :5350  │ │ :5029        │ │ :5400        │
     │ JWT 발급/인증  │  │ 업무 API +     │ │파일    │ │ AI 에이전트  │ │ 헬프데스크   │
     │                │  │ DeviceHub(RT)  │ │업/다운 │ │              │ │ 요청/WBS/일정│
     └───────┬────────┘  └───────┬────────┘ └───┬────┘ └──────┬───────┘ └──────┬───────┘
             └───────────────────┴──────────────┴─────────────┴────────────────┘
                                   │
                          PostgreSQL (EF Core)
              microservices/Common/*  (Shared.DTOs / Domain / Infrastructure)
```

## 3. 라우팅 및 포트 매핑 (ApiGateway 기준)

게이트웨이(YARP)는 `ApiGateway/appsettings.json`에 선언된 경로 규칙으로 요청을 각 서비스(클러스터)로 전달한다.

| 게이트웨이 경로 | 대상 클러스터 | 대상 서비스 | 포트 | 인증 |
|---|---|---|---|---|
| `/api/auth/**` | auth-cluster | AuthServer | 5264 | Anonymous(로그인 등) |
| `/api/funeral/hubs/device/**` | funeral-cluster | funeralv2Api (SignalR `DeviceHub`) | 5320 | Anonymous |
| `/api/funeral/building/device/code/{code}` 등 device/deceased/kiosk 조회 | funeral-cluster | funeralv2Api | 5320 | Anonymous(플레이어용) |
| `/api/funeral/**` (그 외) | funeral-cluster | funeralv2Api | 5320 | JWT 필요 |
| `/api/file/download/**`, `/thumbnail/**` | file-cluster | FileServer | 5350 | Anonymous |
| `/api/file/**` (그 외) | file-cluster | FileServer | 5350 | JWT 필요 |
| `/api/ai/**` | ai-cluster | AIAgentServer | 5029 | JWT 필요 |
| `/api/helpdesk/**` | helpdesk-cluster | HelpDeskServer | 5400 | Anonymous(서비스가 자체 검증) |

- `/api/helpdesk/**` 는 프리픽스를 떼고 다시 `/api` 를 붙여 전달한다(`/api/helpdesk/users/login` → HelpDeskServer 의 `/api/users/login`). HelpDeskServer 는 자체 로그인 토큰(`helpdesk-api`)과 게이트웨이 토큰(`funeralv2-auth`)을 모두 수용한다.
- 게이트웨이 자체 포트: **5265** (`ApiGateway/Properties/launchSettings.json`)
- 프론트엔드 개발 프록시: `fronts/apps/jsini-portal/vite.config.ts:19` → `http://127.0.0.1:5265`

## 4. 프로젝트 간 연동 관계

### 4.1 프론트엔드 ↔ 백엔드
- `fronts`(관리자 웹)는 모든 요청을 `/api/*` 접두어로 게이트웨이(:5265)에 보낸다.
- 실시간 빈소 현황판은 SignalR로 연결: `fronts/apps/jsini-portal/src/views/building/status/index.vue:37` → `/api/funeral/hubs/device`.
- 인증은 AuthServer가 발급한 JWT를 게이트웨이가 검증한다.

### 4.2 플레이어(사이니지) ↔ 백엔드
- `funeralv2_player`(Flutter)는 게이트웨이 base URL + `/api/funeral/hubs/device`로 SignalR 접속한다: `funeralv2_player/lib/services/signalr/signalr_service.dart:224`.
- 디바이스 코드 기반 익명 조회(`/api/funeral/building/device/code/{code}`, `.../deceased/kiosk/...` 등)로 표출할 고인/빈소 데이터를 받는다. → 게이트웨이에서 이 경로들은 `AuthorizationPolicy: Anonymous`로 열려 있음.
- 배포 환경은 **라즈베리파이 / 데스크톱 키오스크**(참고: `docs/raspberry실행환경.md`, `funeralv2_player`의 `window_manager` 사용).

### 4.3 서비스 간 공유
- `microservices/Common/`의 3개 공유 라이브러리(`JSini.Shared.DTOs`, `.Shared.Domain`, `.Shared.Infrastructure`)를 각 서비스가 참조하여 DTO/도메인/인프라(Repository 패턴 등)를 공유한다.
- 코딩 표준(`AI.md`): Repository 패턴 의무화, 무거운 집계는 PostgreSQL Materialized View + BackgroundService 리프레시.

## 5. 데이터 흐름 예시 (사이니지 표출)

1. 관리자가 웹(`fronts`)에서 고인/빈소/미디어를 등록 → 게이트웨이 → `funeralv2Api` → PostgreSQL 저장.
2. 변경 발생 시 `funeralv2Api`의 `DeviceHub`가 SignalR로 연결된 클라이언트에 브로드캐스트.
3. 장례식장의 `funeralv2_player`가 이벤트를 수신하여 화면을 갱신하고, 필요한 미디어는 `FileServer`에서 다운로드.

## 6. 개별 분석 문서

각 프로젝트의 상세 분석 및 개선점은 아래 문서를 참조한다.

- [ApiGateway 분석](./apigateway-analysis.md)
- [Microservices 분석](./microservices-analysis.md)
- [Frontend(Vue3/Vben) 분석](./frontend-analysis.md)
- [Player(Flutter) 분석](./player-analysis.md)

## 7. 시스템 통합 우선순위 (교차 이슈)

4개 분석에서 반복적으로 드러난, 여러 프로젝트에 걸친 최우선 이슈를 통합 정리한다. 상세 근거는 각 개별 문서 참조.

### 🔴 P0 — 보안 치명 (즉시 조치 권장)

1. **비밀번호 평문 저장·비교**: 로그인이 해시 없이 `account.Password != request.Password`로 검증됨 (`microservices/AuthServer/Services/AuthService.cs:39`). → BCrypt/Argon2 해싱 도입.
2. **리소스 서비스 전면 무인증**: funeralv2Api·FileServer·AIAgentServer가 JWT 패키지를 참조하면서도 인증 미들웨어를 연결하지 않아, 게이트웨이를 우회해 서비스 포트에 직접 접근하면 장비·고인 CRUD, 파일 업로드가 무방비. → 각 서비스에 `UseAuthentication`/`RequireAuthorization` 적용(심층방어).
3. **시크릿 하드코딩 커밋**: JWT 서명 키(게이트웨이·3개 서비스 동일 문자열), VAPID 개인키, 프론트 localStorage 암호화 키(`VITE_APP_STORE_SECURE_KEY`)가 저장소에 평문 커밋. → 시크릿 매니저/환경변수 이관 + 키 로테이션 + git 이력 정리.
4. **레이트 리미팅 전무**: 게이트웨이·서비스 어디에도 없음 → 로그인 브루트포스, AI(과금) 남용, 디바이스코드 열거 무방비.

### 🟠 P1 — 아키텍처/복원력

5. **하드코딩된 서비스 주소·평문 HTTP**: 게이트웨이 destination이 `http://localhost:*` 고정, 클러스터 간 TLS·HSTS 부재 → `X-User-*` 신뢰 헤더가 평문 전송. 환경별 설정 분리/서비스 디스커버리/내부 TLS 필요.
6. **복원력 부재**: 게이트웨이 재시도/타임아웃/서킷브레이커·헬스체크 없음, 서비스에 `new HttpClient()` 남용(소켓 고갈), 플레이어 SignalR에 서버 타임아웃/워치독 없음 → always-on 사이니지가 "연결됨" 상태로 이벤트 유실.
7. **AI.md 핵심 표준 미준수**: Repository 패턴 의무화에도 전 서비스가 `AppDbContext` 직접 주입, Materialized View 표준 미적용.
8. **테스트·관측성 0건**: 백엔드 테스트/헬스체크 없음, 상관관계 ID 로깅 없음, 플레이어의 유일한 테스트는 컴파일 불가.

### 🟡 P2 — 품질/유지보수

9. **프론트 개발 잔재 상주**: 프로덕션 devtools 활성화, `console.*` 72곳, 매 네비게이션 로그, `any`/`as any` 357곳, 비즈니스 뷰 `$t()` 미사용(한국어 하드코딩).
10. **응답 언래핑 규약 불일치**: 프론트에 `?.result ?? res` 방어 패턴 22곳 → API 응답 봉투 규약 통일 필요.
11. **플레이어 캐시 무제한 증가**: TTL/용량 상한/동일파일 갱신 없음 → SD카드 고갈·stale 미디어 표출.
12. **죽은 코드**: 프론트 스텁 뷰 15개 + `-custom` 실구현 17개 병존.

### 공통 테마 요약

| 테마 | 게이트웨이 | 마이크로서비스 | 프론트 | 플레이어 |
|---|:---:|:---:|:---:|:---:|
| 시크릿 하드코딩 | ● | ● | ● | ○ |
| 인증/인가 공백 | ○(익명경로) | ●(무인증) | ●(SignalR/AI) | ●(무인증) |
| 복원력(재시도/타임아웃) | ● | ● | – | ●(SignalR) |
| 관측성/로깅 | ● | ● | ●(과다) | ● |
| 테스트 부재 | ● | ● | ● | ● |
| 코딩표준 미준수 | – | ●(Repo/MV) | ●(TS/i18n) | – |

## 8. 개별 분석 문서 (재게시)

- [ApiGateway 분석](./apigateway-analysis.md)
- [Microservices 분석](./microservices-analysis.md)
- [Frontend(Vue3/Vben) 분석](./frontend-analysis.md)
- [Player(Flutter) 분석](./player-analysis.md)
